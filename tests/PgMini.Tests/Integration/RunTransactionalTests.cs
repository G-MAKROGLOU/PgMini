using PgMini.Helpers;
using PgMini.Models;

namespace PgMini.Tests.Integration;

[Collection("Postgres")]
public class RunTransactionalTests(PostgresFixture db) : IAsyncLifetime
{
    public Task InitializeAsync() => db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RunTransactional_AllStepsSucceed_Commits()
    {
        var tx = new Transaction
        {
            TransactionGroupName = "InsertAndVerify",
            Queries =
            [
                new()
                {
                    Runnable = db.Client.AutonomousTxExecuteStep(
                        "INSERT INTO test_simple (name, value) VALUES ('tx_insert', 77)"),
                    PostExecute = TxHelper.NoOpExecute()
                },
                new()
                {
                    Runnable = db.Client.AutonomousTxReadStep<SimpleRow>(
                        "SELECT id, name, value FROM test_simple WHERE name = 'tx_insert'"),
                }
            ]
        };

        var result = await db.Client.RunTransactional<List<SimpleRow>>(tx);

        result.Should().NotBeNull();
        result!.Should().HaveCount(1);
        result![0].Name.Should().Be("tx_insert");
        result[0].Value.Should().Be(77);
    }

    [Fact]
    public async Task RunTransactional_StepFails_RollsBack()
    {
        var tx = new Transaction
        {
            TransactionGroupName = "RollbackOnError",
            Queries =
            [
                new()
                {
                    Runnable = db.Client.AutonomousTxExecuteStep(
                        "INSERT INTO test_simple (name, value) VALUES ('should_not_exist', 1)"),
                    PostExecute = TxHelper.NoOpExecute()
                },
                new()
                {
                    // Intentionally broken SQL — triggers rollback
                    Runnable = db.Client.AutonomousTxExecuteStep("THIS IS NOT SQL"),
                }
            ]
        };

        var act = async () => await db.Client.RunTransactional<object>(tx);
        await act.Should().ThrowAsync<Exception>();

        // The first step's insert must have been rolled back
        var exists = await db.Client.ExistsAsync(
            "SELECT EXISTS(SELECT 1 FROM test_simple WHERE name = 'should_not_exist')");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task RunTransactional_ForwardedQuery_UsedByNextStep()
    {
        // Step 1: insert and return the new id via RETURNING
        // Step 2: read the inserted row using the forwarded id
        var tx = new Transaction
        {
            TransactionGroupName = "ForwardId",
            Queries =
            [
                new()
                {
                    Runnable = db.Client.AutonomousTxExecuteScalarStep<int>(
                        "INSERT INTO test_simple (name, value) VALUES ('forwarded', 55) RETURNING id"),
                    PostExecuteScalar = result =>
                    {
                        var id = (int)result;
                        return Task.FromResult<(string?, List<QueryParams>?)>((
                            "SELECT id, name, value FROM test_simple WHERE id = @id",
                            [QueryParams.Of("id", id)]
                        ));
                    }
                },
                new()
                {
                    Runnable = db.Client.TxReadStep<SimpleRow>()
                }
            ]
        };

        var result = await db.Client.RunTransactional<List<SimpleRow>>(tx);

        result.Should().HaveCount(1);
        result![0].Name.Should().Be("forwarded");
        result[0].Value.Should().Be(55);
    }

    [Fact]
    public async Task RunTransactional_MultipleInserts_AllCommitted()
    {
        var tx = new Transaction
        {
            TransactionGroupName = "MultiInsert",
            Queries =
            [
                new()
                {
                    Runnable = db.Client.AutonomousTxExecuteStep(
                        "INSERT INTO test_simple (name, value) VALUES ('tx1', 1)"),
                    PostExecute = TxHelper.NoOpExecute()
                },
                new()
                {
                    Runnable = db.Client.AutonomousTxExecuteStep(
                        "INSERT INTO test_simple (name, value) VALUES ('tx2', 2)"),
                    PostExecute = TxHelper.NoOpExecute()
                },
                new()
                {
                    Runnable = db.Client.AutonomousTxReadStep<SimpleRow>(
                        "SELECT id, name, value FROM test_simple WHERE name LIKE 'tx%' ORDER BY name")
                }
            ]
        };

        var result = await db.Client.RunTransactional<List<SimpleRow>>(tx);

        result.Should().HaveCount(2);
        result![0].Name.Should().Be("tx1");
        result[1].Name.Should().Be("tx2");
    }
}

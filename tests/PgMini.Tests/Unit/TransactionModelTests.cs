using PgMini.Delegates;
using PgMini.Helpers;
using PgMini.Models;

namespace PgMini.Tests.Unit;

public class TransactionModelTests
{
    [Fact]
    public void Transaction_CanBeConstructed_WithRequiredProperties()
    {
        TxStep step = (_, _, _) => Task.FromResult<object?>(null);

        var tx = new Transaction
        {
            TransactionGroupName = "TestGroup",
            Queries =
            [
                new TransactionalQuery { Runnable = step }
            ]
        };

        tx.TransactionGroupName.Should().Be("TestGroup");
        tx.Queries.Should().HaveCount(1);
    }

    [Fact]
    public void QueryParams_Of_FactoryMethod_SetsKeyAndValue()
    {
        var p = QueryParams.Of("myKey", 42);

        p.Key.Should().Be("myKey");
        p.Value.Should().Be(42);
    }

    [Fact]
    public void QueryParams_Of_NullValue_IsAllowed()
    {
        var p = QueryParams.Of("nullKey", null);

        p.Key.Should().Be("nullKey");
        p.Value.Should().BeNull();
    }

    [Fact]
    public async Task TxHelper_NoOp_ReturnsNullTuple()
    {
        var noOp = TxHelper.NoOp();
        var (query, parms) = await noOp(new object());

        query.Should().BeNull();
        parms.Should().BeNull();
    }

    [Fact]
    public async Task TxHelper_NoOpExecute_ReturnsNullTuple()
    {
        var noOp = TxHelper.NoOpExecute();
        var (query, parms) = await noOp(5);

        query.Should().BeNull();
        parms.Should().BeNull();
    }

    [Fact]
    public void BatchCommand_Record_CanBeConstructed()
    {
        var cmd = new BatchCommand("SELECT 1", [QueryParams.Of("k", "v")]);

        cmd.Query.Should().Be("SELECT 1");
        cmd.Parameters.Should().HaveCount(1);
    }

    [Fact]
    public void BatchCommand_NullParameters_IsAllowed()
    {
        var cmd = new BatchCommand("DELETE FROM foo");

        cmd.Parameters.Should().BeNull();
    }
}

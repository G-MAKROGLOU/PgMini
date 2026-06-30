namespace PgMini.Interfaces;

public interface IDbClientProvider
{
    IDbClient GetClient(string? key = null);
}

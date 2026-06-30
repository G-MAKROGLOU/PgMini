using Microsoft.Extensions.DependencyInjection;
using PgMini.Interfaces;

namespace PgMini;

public class DbClientProvider(IServiceProvider sp) : IDbClientProvider
{
    public IDbClient GetClient(string? key = null) =>
        key is null
            ? sp.GetRequiredService<IDbClient>()
            : sp.GetRequiredKeyedService<IDbClient>(key);
}

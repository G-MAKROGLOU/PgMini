using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using PgMini.Interfaces;

namespace PgMini.Extensions;

/// <summary>Extension methods for registering PgMini with the .NET DI container.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="NpgsqlDataSource"/> and a scoped <see cref="IDbClient"/>
    /// using the provided <paramref name="connectionString"/>.
    /// <para>
    /// Optionally configure the data source builder (e.g. add type mappers, enable Geometry support).
    /// Dynamic JSON is enabled by default.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddPgMini("Host=localhost;Database=mydb;Username=user;Password=pass");
    /// </code>
    /// </example>
    public static IServiceCollection AddPgMini(
        this IServiceCollection services,
        string connectionString,
        Action<NpgsqlDataSourceBuilder>? configureDataSource = null)
    {
        services.AddSingleton(sp =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString).EnableDynamicJson();
            configureDataSource?.Invoke(builder);
            return builder.Build();
        });

        services.AddScoped<IDbClient>(sp =>
            new DbClient(
                sp.GetRequiredService<NpgsqlDataSource>(),
                sp.GetRequiredService<ILogger<DbClient>>()));

        services.TryAddSingleton<IDbClientProvider, DbClientProvider>();

        return services;
    }

    /// <summary>
    /// Registers a keyed singleton <see cref="NpgsqlDataSource"/> and a keyed scoped <see cref="IDbClient"/>
    /// under the given <paramref name="name"/>. Useful for multi-tenant or multi-database scenarios.
    /// <para>Inject with <c>[FromKeyedServices("name")]</c> in .NET 8+.</para>
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddNamedPgMini("portal", portalConnString);
    /// builder.Services.AddNamedPgMini("analytics", analyticsConnString);
    ///
    /// // Usage in a service:
    /// public class MyService([FromKeyedServices("portal")] IDbClient portalDb) { }
    /// </code>
    /// </example>
    public static IServiceCollection AddNamedPgMini(
        this IServiceCollection services,
        string name,
        string connectionString,
        Action<NpgsqlDataSourceBuilder>? configureDataSource = null)
    {
        services.AddKeyedSingleton<NpgsqlDataSource>(name, (_, _) =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString).EnableDynamicJson();
            configureDataSource?.Invoke(builder);
            return builder.Build();
        });

        services.AddKeyedScoped<IDbClient>(name, (sp, key) =>
            new DbClient(
                sp.GetRequiredKeyedService<NpgsqlDataSource>((string)key!),
                sp.GetRequiredService<ILogger<DbClient>>()));

        services.TryAddSingleton<IDbClientProvider, DbClientProvider>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="IDbClientProvider"/> without registering any <see cref="IDbClient"/>.
    /// Use this when clients are registered manually (e.g. with keyed singletons resolved via async factories).
    /// </summary>
    public static IServiceCollection AddDbClientProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<IDbClientProvider, DbClientProvider>();
        return services;
    }
}

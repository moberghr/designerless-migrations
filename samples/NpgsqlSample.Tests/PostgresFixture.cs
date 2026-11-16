using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(NpgsqlSample.Tests.PostgresFixture))]

namespace NpgsqlSample.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>A connection string to a new, empty database on the shared container.</summary>
    public string NewDatabase() =>
        _container.GetConnectionString().Replace("Database=postgres", $"Database=sample_{Guid.NewGuid():N}", StringComparison.Ordinal);

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

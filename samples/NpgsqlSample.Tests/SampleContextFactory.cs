using Microsoft.EntityFrameworkCore;

namespace NpgsqlSample.Tests;

internal static class SampleContextFactory
{
    // Same options as the app's registration in Program.cs.
    public static SampleContext Create(string connectionString) =>
        new(new DbContextOptionsBuilder<SampleContext>()
            .UseNpgsql(connectionString)
            .UseDesignerlessMigrations()
            .Options);
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql.Tests;

public class DesignerlessMigrationsDbContextOptionsBuilderExtensionsTests
{
    [Fact]
    public void UseDesignerlessMigrations_WhenCalledAfterUseNpgsql_ThenReplacesSqlGenerator()
    {
        var options = new DbContextOptionsBuilder()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseDesignerlessMigrations()
            .Options;
        using var context = new DbContext(options);

        context.GetService<IMigrationsSqlGenerator>().ShouldBeOfType<DesignerlessNpgsqlMigrationsSqlGenerator>();
    }

    [Fact]
    public void UseDesignerlessMigrations_WhenGenericBuilder_ThenReturnsSameTypedBuilder()
    {
        var builder = new DbContextOptionsBuilder<SampleContext>().UseNpgsql("Host=localhost;Database=unused");

        var result = builder.UseDesignerlessMigrations();

        result.ShouldBeSameAs(builder);
        using var context = new SampleContext(result.Options);
        context.GetService<IMigrationsSqlGenerator>().ShouldBeOfType<DesignerlessNpgsqlMigrationsSqlGenerator>();
    }

    [Fact]
    public void UseDesignerlessMigrations_WhenUseNpgsqlNotCalledFirst_ThenThrows()
    {
        var builder = new DbContextOptionsBuilder();

        var exception = Should.Throw<InvalidOperationException>(() => builder.UseDesignerlessMigrations());

        exception.Message.ShouldContain("Call UseNpgsql(...) first");
    }

    public class SampleContext(DbContextOptions<SampleContext> options) : DbContext(options);
}

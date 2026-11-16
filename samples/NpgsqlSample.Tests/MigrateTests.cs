using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NpgsqlSample.Tests;

/// <summary>The designerless migrations applied to a real PostgreSQL, as the app does at deploy time.</summary>
public class MigrateTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrate_WhenAppliedToEmptyDatabase_ThenAppliesEveryMigrationAndSeedsData()
    {
        await using var context = SampleContextFactory.Create(postgres.NewDatabase());

        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var applied = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        applied.ShouldBe(context.Database.GetMigrations());
        applied.Count().ShouldBe(3);
        var currencies = await context.Currencies.OrderBy(x => x.Code).Select(x => x.Code).ToListAsync(TestContext.Current.CancellationToken);
        currencies.ShouldBe(["EUR", "JPY"]);
    }

    [Fact]
    public void GenerateScript_WhenMigrationOnlyChangesNullability_ThenDoesNotAlterColumnType()
    {
        using var context = SampleContextFactory.Create("Host=localhost;Database=unused");
        var migrations = context.Database.GetMigrations().ToList();
        var initial = migrations.Single(x => x.EndsWith("_Initial", StringComparison.Ordinal));
        var requireBlogName = migrations.Single(x => x.EndsWith("_RequireBlogName", StringComparison.Ordinal));

        var script = context.GetService<IMigrator>().GenerateScript(fromMigration: initial, toMigration: requireBlogName);

        script.ShouldContain("ALTER COLUMN \"Name\" SET NOT NULL");
        script.ShouldNotContain(" TYPE ");
    }

    [Fact]
    public async Task Migrate_WhenRevertingForwardOnlyMigration_ThenFailsInsteadOfDroppingHistory()
    {
        // No Down() means no silent half-revert: EF refuses instead of deleting the history row and keeping the schema.
        await using var context = SampleContextFactory.Create(postgres.NewDatabase());
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var initial = context.Database.GetMigrations().First();

        var exception = await Should.ThrowAsync<NotSupportedException>(
            () => context.GetService<IMigrator>().MigrateAsync(initial, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("'Down' method for this migration has not been implemented");

        (await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).Count().ShouldBe(3);
    }
}

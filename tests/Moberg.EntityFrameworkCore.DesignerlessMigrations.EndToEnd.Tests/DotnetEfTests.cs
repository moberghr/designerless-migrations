using Npgsql;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.EndToEnd.Tests;

/// <summary>The real dotnet ef against a consumer project that only references the packages.</summary>
public class DotnetEfTests(EndToEndFixture fixture)
{
    [Fact]
    public async Task MigrationsAdd_WhenModelChanged_ThenWritesOneFileWithAttributesAndNoDesigner()
    {
        var workspace = await SampleWorkspace.CreateAsync(fixture);
        workspace.AddTagEntity();

        (await workspace.EfAsync("migrations", "add", "AddTags")).ShouldSucceed();

        var migration = workspace.MigrationFiles("_AddTags").ShouldHaveSingleItem();
        var migrationId = Path.GetFileNameWithoutExtension(migration);
        var code = await File.ReadAllTextAsync(migration, TestContext.Current.CancellationToken);
        code.ShouldContain("[DbContext(typeof(SampleContext))]");
        code.ShouldContain($"[Migration(\"{migrationId}\")]");
        code.ShouldContain("migrationBuilder.CreateTable(");
        workspace.DesignerFiles().ShouldBeEmpty();
        (await File.ReadAllTextAsync(workspace.SnapshotFile, TestContext.Current.CancellationToken)).ShouldContain("NpgsqlSample.Tag");
    }

    [Fact]
    public async Task MigrationsRemove_WhenNotApplied_ThenRestoresRepositoryExactly()
    {
        var workspace = await SampleWorkspace.CreateAsync(fixture);
        workspace.AddTagEntity();
        await workspace.CommitAllAsync("add Tag entity");
        (await workspace.EfAsync("migrations", "add", "AddTags")).ShouldSucceed();

        (await workspace.EfAsync("migrations", "remove")).ShouldSucceed();

        // Clean status: the migration is gone and the snapshot is byte-identical to the committed one (BOM, CRLF).
        (await workspace.GitStatusAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task MigrationsRemove_WhenNoBuildAfterAdd_ThenRefusesAndKeepsFiles()
    {
        var workspace = await SampleWorkspace.CreateAsync(fixture);
        workspace.AddTagEntity();
        await workspace.CommitAllAsync("add Tag entity");
        (await workspace.EfAsync("migrations", "add", "AddTags")).ShouldSucceed();
        var added = workspace.MigrationFiles("_AddTags").ShouldHaveSingleItem();
        var committedMigrations = Directory.GetFiles(workspace.MigrationsDirectory, "2*.cs").Except([added]).ToList();

        // The compiled assembly predates AddTags, so EF's newest migration is SeedCurrencies, which is committed.
        var result = await workspace.EfAsync("migrations", "remove", "--no-build");

        result.ShouldFail();
        result.Output.ShouldContain(Path.GetFileName(added));
        File.Exists(added).ShouldBeTrue();
        committedMigrations.ShouldAllBe(x => File.Exists(x));
    }

    [Fact]
    public async Task MigrationsRemove_WhenAnotherMigrationIsUncommitted_ThenRefusesNamingIt()
    {
        var workspace = await SampleWorkspace.CreateAsync(fixture);
        workspace.AddTagEntity();
        (await workspace.EfAsync("migrations", "add", "AddTags")).ShouldSucceed();
        (await workspace.EfAsync("migrations", "add", "Second")).ShouldSucceed();
        var addTags = workspace.MigrationFiles("_AddTags").ShouldHaveSingleItem();

        var result = await workspace.EfAsync("migrations", "remove");

        result.ShouldFail();
        result.Output.ShouldContain(Path.GetFileName(addTags));
        workspace.MigrationFiles("_Second").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task MigrationsRemove_WhenApplied_ThenRefusesWithoutForceAndRevertsWithForce()
    {
        var workspace = await SampleWorkspace.CreateAsync(fixture);
        workspace.AddTagEntity();
        await workspace.CommitAllAsync("add Tag entity");
        (await workspace.EfAsync("migrations", "add", "AddTags")).ShouldSucceed();
        var migrationId = Path.GetFileNameWithoutExtension(workspace.MigrationFiles("_AddTags").ShouldHaveSingleItem());
        (await workspace.EfAsync("database", "update")).ShouldSucceed();

        var withoutForce = await workspace.EfAsync("migrations", "remove");
        withoutForce.ShouldFail();
        withoutForce.Output.ShouldContain("already been applied");

        (await AppliedMigrationsAsync(workspace)).ShouldContain(migrationId);
        workspace.MigrationFiles("_AddTags").ShouldHaveSingleItem();

        // A fresh migration still has Down(), so --force can revert it, as during local iteration.
        (await workspace.EfAsync("migrations", "remove", "--force")).ShouldSucceed();
        (await AppliedMigrationsAsync(workspace)).ShouldNotContain(migrationId);
        (await TableExistsAsync(workspace, "Tags")).ShouldBeFalse();
        (await workspace.GitStatusAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task MigrationsScript_WhenMigrationOnlyChangesNullability_ThenDoesNotAlterColumnType()
    {
        var workspace = await SampleWorkspace.CreateAsync(fixture);
        var initial = Path.GetFileNameWithoutExtension(workspace.MigrationFiles("_Initial").ShouldHaveSingleItem());
        var requireBlogName = Path.GetFileNameWithoutExtension(workspace.MigrationFiles("_RequireBlogName").ShouldHaveSingleItem());

        var result = await workspace.EfAsync("migrations", "script", initial, requireBlogName);

        result.ShouldSucceed();
        result.Output.ShouldContain("ALTER COLUMN \"Name\" SET NOT NULL");
        result.Output.ShouldNotContain(" TYPE ");
    }

    [Fact]
    public async Task Publish_WhenPublished_ThenExcludesDesignTimeAssembliesAndLoadsAllTypes()
    {
        var workspace = await SampleWorkspace.CreateAsync(fixture);
        var output = Path.Combine(workspace.Root, "publish");

        (await workspace.DotnetAsync("publish", "NpgsqlSample", "-c", "Release", "-o", output)).ShouldSucceed();

        var files = Directory.GetFiles(output).Select(Path.GetFileName).ToList();
        files.ShouldContain("Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql.dll");
        files.ShouldNotContain("Moberg.EntityFrameworkCore.DesignerlessMigrations.dll");
        files.ShouldNotContain("Microsoft.EntityFrameworkCore.Design.dll");
        var deps = await File.ReadAllTextAsync(Path.Combine(output, "NpgsqlSample.deps.json"), TestContext.Current.CancellationToken);
        deps.ShouldNotContain("Microsoft.EntityFrameworkCore.Design");
        deps.ShouldNotContain("\"Moberg.EntityFrameworkCore.DesignerlessMigrations/");

        // The published app loads every one of its types, and applies its designerless migrations.
        var checkTypes = await workspace.DotnetAsync(Path.Combine(output, "NpgsqlSample.dll"), "check-types");
        checkTypes.ShouldSucceed();
        checkTypes.Output.ShouldContain("Loaded");
        (await workspace.DotnetAsync(Path.Combine(output, "NpgsqlSample.dll"), "migrate")).ShouldSucceed();
        (await AppliedMigrationsAsync(workspace)).Count.ShouldBe(3);
    }

    private static async Task<List<string>> AppliedMigrationsAsync(SampleWorkspace workspace)
    {
        await using var connection = new NpgsqlConnection(workspace.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY 1", connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var ids = new List<string>();

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static async Task<bool> TableExistsAsync(SampleWorkspace workspace, string table)
    {
        await using var connection = new NpgsqlConnection(workspace.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL", connection);
        command.Parameters.AddWithValue("name", $"public.\"{table}\"");

        return (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}

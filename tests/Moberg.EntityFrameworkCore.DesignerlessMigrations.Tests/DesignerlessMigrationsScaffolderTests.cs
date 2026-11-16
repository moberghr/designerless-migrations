using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests;

/// <summary>
/// Builds EF's design-time services the way dotnet ef does, with this assembly as the startup assembly. It carries
/// the [DesignTimeServicesReference] from the package's real build props, so these tests also prove that attribute
/// alone makes EF use the designerless scaffolder.
/// </summary>
public sealed partial class DesignerlessMigrationsScaffolderTests : IDisposable
{
    private readonly BloggingContext _context;
    private readonly IServiceScope _scope;
    private readonly string _projectDir = Path.Combine(Path.GetTempPath(), "designerless-scaffolder-tests", Guid.NewGuid().ToString("N"));

    public DesignerlessMigrationsScaffolderTests()
    {
        _context = BloggingContext.Create();

        var assembly = typeof(BloggingContext).Assembly;
#pragma warning disable EF1001 // DesignTimeServicesBuilder is what dotnet ef itself uses; there is no public equivalent.
        var services = new DesignTimeServicesBuilder(assembly, assembly, new OperationReporter(handler: null), [])
            .Build(_context);
#pragma warning restore EF1001
        _scope = services.CreateScope();
        Scaffolder = _scope.ServiceProvider.GetRequiredService<IMigrationsScaffolder>();

        Directory.CreateDirectory(_projectDir);
    }

    private IMigrationsScaffolder Scaffolder { get; }

    private IServiceProvider Services => _scope.ServiceProvider;

    private string ProjectDir => _projectDir;

    public void Dispose()
    {
        _scope.Dispose();
        _context.Dispose();

        if (Directory.Exists(_projectDir))
        {
            Directory.Delete(_projectDir, recursive: true);
        }
    }

    [Fact]
    public void ScaffoldMigration_WhenResolvedFromDesignTimeServices_ThenIsDesignerlessScaffolder()
    {
        Scaffolder.ShouldBeOfType<DesignerlessMigrationsScaffolder>();
    }

    [Fact]
    public void ScaffoldMigration_WhenScaffolded_ThenMigrationCarriesMetadataAttributes()
    {
        var migration = Scaffolder.ScaffoldMigration("ScaffolderTest", "App", "Migrations");

        migration.MigrationId.ShouldEndWith("_ScaffolderTest");
        migration.MigrationCode.ShouldContain("[DbContext(typeof(BloggingContext))]");
        migration.MigrationCode.ShouldContain($"[Migration(\"{migration.MigrationId}\")]");
        migration.MigrationCode.ShouldContain("using Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture;");
        migration.MigrationCode.ShouldContain("using Microsoft.EntityFrameworkCore.Infrastructure;");
        migration.MigrationCode.ShouldContain("public partial class ScaffolderTest : Migration");
    }

    [Fact]
    public void ScaffoldMigration_WhenScaffolded_ThenHasNoMetadataCode()
    {
        var migration = Scaffolder.ScaffoldMigration("ScaffolderTest", "App", "Migrations");

        migration.MetadataCode.ShouldBeEmpty();
        migration.MigrationCode.ShouldNotContain("BuildTargetModel");
        migration.SnapshotCode.ShouldContain("class BloggingContextModelSnapshot");
    }

    [Fact]
    public void Save_WhenSaved_ThenWritesMigrationAndSnapshotWithoutDesignerFile()
    {
        var migration = Scaffolder.ScaffoldMigration("ScaffolderTest", "App", "Migrations");

        var files = Scaffolder.Save(ProjectDir, migration, outputDir: ProjectDir, dryRun: false);

        files.MetadataFile.ShouldBeNull();
        File.ReadAllText(files.MigrationFile!).ShouldBe(migration.MigrationCode);
        File.Exists(files.SnapshotFile).ShouldBeTrue();
        Directory.GetFiles(ProjectDir, "*.Designer.cs", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public void Save_WhenDryRun_ThenWritesNothing()
    {
        var migration = Scaffolder.ScaffoldMigration("ScaffolderTest", "App", "Migrations");

        var files = Scaffolder.Save(ProjectDir, migration, outputDir: ProjectDir, dryRun: true);

        files.MetadataFile.ShouldBeNull();
        Directory.GetFiles(ProjectDir, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }
}

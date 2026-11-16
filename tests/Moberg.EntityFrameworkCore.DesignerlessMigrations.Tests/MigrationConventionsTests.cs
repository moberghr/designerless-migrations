using ConventionFixtures;
using Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests;

public class MigrationConventionsTests
{
    [Fact]
    public void FindViolations_WhenMigrationsFollowConventions_ThenReturnsNone()
    {
        using var context = BloggingContext.Create();

        MigrationConventions.FindViolations(context).ShouldBeEmpty();
    }

    [Fact]
    public void FindViolations_WhenDefaultOptions_ThenReportsEveryBrokenConvention()
    {
        using var context = ConventionsContext.Create();

        var result = MigrationConventions.FindViolations(context);

        result.Select(x => (x.MigrationType.Name, x.Rule)).ShouldBe(
            [
                (nameof(DataWithoutTypes), MigrationConventionRule.DataOperationWithoutColumnTypes),
                (nameof(DataWithoutTypes), MigrationConventionRule.DataOperationWithoutColumnTypes),
                (nameof(DataWithoutTypes), MigrationConventionRule.DataOperationWithoutColumnTypes),
                (nameof(DownDataWithoutTypes), MigrationConventionRule.DeclaresDown),
                (nameof(DownDataWithoutTypes), MigrationConventionRule.DataOperationWithoutColumnTypes),
                (nameof(OnlyDbContextAttributed), MigrationConventionRule.NotDiscovered),
                (nameof(Unattributed), MigrationConventionRule.NotDiscovered),
                (nameof(WithDesigner), MigrationConventionRule.HasDesignerFile),
                (nameof(WithDown), MigrationConventionRule.DeclaresDown),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void FindViolations_WhenDownIsAllowed_ThenDoesNotReportDown()
    {
        using var context = ConventionsContext.Create();

        var result = MigrationConventions.FindViolations(context, new MigrationConventionOptions { AllowDown = true });

        result.ShouldNotContain(x => x.Rule == MigrationConventionRule.DeclaresDown);
        result.ShouldContain(x => x.MigrationType == typeof(DownDataWithoutTypes) && x.Rule == MigrationConventionRule.DataOperationWithoutColumnTypes);
    }

    [Fact]
    public void FindViolations_WhenDataOperationTypesNotRequired_ThenDoesNotRunMigrations()
    {
        using var context = ConventionsContext.Create();

        var result = MigrationConventions.FindViolations(context, new MigrationConventionOptions { RequireDataOperationColumnTypes = false });

        result.ShouldNotContain(x => x.Rule == MigrationConventionRule.DataOperationWithoutColumnTypes);
    }

    [Fact]
    public void FindViolations_WhenMigrationBelongsToAnotherContext_ThenIgnoresIt()
    {
        using var context = ConventionsContext.Create();

        var result = MigrationConventions.FindViolations(context);

        result.ShouldNotContain(x => x.MigrationType == typeof(OtherContextMigration));
        result.ShouldNotContain(x => x.MigrationType == typeof(MigrationBase));
        result.ShouldNotContain(x => x.MigrationType == typeof(Clean));
    }

    [Fact]
    public void FindViolations_WhenViolationReported_ThenMessageSaysHowToFixIt()
    {
        using var context = ConventionsContext.Create();

        var result = MigrationConventions.FindViolations(context);

        result.Single(x => x.MigrationType == typeof(Unattributed)).ToString()
            .ShouldBe("Unattributed: EF would silently skip it. Add [DbContext(typeof(ConventionsContext))] and [Migration(\"<id>\")] to the class.");
        result.Where(x => x.MigrationType == typeof(DataWithoutTypes)).Select(x => x.Message).ShouldBe(
            [
                "DataWithoutTypes.Up: InsertData into 'currencies' needs columnTypes: without a Designer model EF cannot look the types up.",
                "DataWithoutTypes.Up: UpdateData on 'currencies' needs columnTypes: and keyColumnTypes: without a Designer model EF cannot look the types up.",
                "DataWithoutTypes.Up: DeleteData from 'currencies' needs keyColumnTypes: without a Designer model EF cannot look the types up.",
            ]);
    }
}

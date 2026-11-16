using Microsoft.EntityFrameworkCore;
using Moberg.EntityFrameworkCore.DesignerlessMigrations;

namespace NpgsqlSample.Tests;

/// <summary>The PR gate a consumer adds: single-file migrations EF can see, forward-only, data operations typed.</summary>
public class MigrationConventionsTests
{
    [Fact]
    public void Migrations_FollowDesignerlessConventions()
    {
        using var context = SampleContextFactory.Create("Host=localhost;Database=unused");

        MigrationConventions.FindViolations(context).ShouldBeEmpty();
    }

    [Fact]
    public void Model_WhenComparedToSnapshot_ThenHasNoPendingChanges()
    {
        using var context = SampleContextFactory.Create("Host=localhost;Database=unused");

        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }
}

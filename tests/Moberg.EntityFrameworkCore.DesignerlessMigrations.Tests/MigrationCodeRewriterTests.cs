using Moberg.EntityFrameworkCore.DesignerlessMigrations.Scaffolding;
using Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests;

public class MigrationCodeRewriterTests
{
    private const string MigrationId = "20261112120000_AddThing";

    // Raw literals take the file's line endings from the checkout; the rewriter is tested on LF and CRLF explicitly.
    private static readonly string ScaffoldedMigration = LF(
        """
        using System;
        using Microsoft.EntityFrameworkCore.Migrations;

        #nullable disable

        namespace App.Migrations
        {
            /// <inheritdoc />
            public partial class AddThing : Migration
            {
                /// <inheritdoc />
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                }
            }
        }
        """);

    [Fact]
    public void AddMetadataAttributes_WhenScaffoldedMigration_ThenAddsAttributesAndUsings()
    {
        var result = MigrationCodeRewriter.AddMetadataAttributes(ScaffoldedMigration, MigrationId, typeof(BloggingContext));

        result.ShouldBe(LF(
            """
            using System;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Microsoft.EntityFrameworkCore.Migrations;
            using Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture;

            #nullable disable

            namespace App.Migrations
            {
                /// <inheritdoc />
                [DbContext(typeof(BloggingContext))]
                [Migration("20261112120000_AddThing")]
                public partial class AddThing : Migration
                {
                    /// <inheritdoc />
                    protected override void Up(MigrationBuilder migrationBuilder)
                    {
                    }
                }
            }
            """));
    }

    [Fact]
    public void AddMetadataAttributes_WhenUsingsAlreadyPresent_ThenDoesNotDuplicateThem()
    {
        var code = ScaffoldedMigration.Replace(
            "using Microsoft.EntityFrameworkCore.Migrations;",
            "using Microsoft.EntityFrameworkCore.Infrastructure;\nusing Microsoft.EntityFrameworkCore.Migrations;\nusing Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture;",
            StringComparison.Ordinal);

        var result = MigrationCodeRewriter.AddMetadataAttributes(code, MigrationId, typeof(BloggingContext));

        result.Split("using Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture;").Length.ShouldBe(2);
        result.Split("using Microsoft.EntityFrameworkCore.Infrastructure;").Length.ShouldBe(2);
    }

    [Fact]
    public void AddMetadataAttributes_WhenUsingsUnsorted_ThenPutsSystemFirstThenOrdinal()
    {
        var code = ScaffoldedMigration.Replace(
            "using System;\nusing Microsoft.EntityFrameworkCore.Migrations;",
            "using Zeta;\nusing Microsoft.EntityFrameworkCore.Migrations;\nusing System.Collections.Generic;\nusing System;",
            StringComparison.Ordinal);

        var result = MigrationCodeRewriter.AddMetadataAttributes(code, MigrationId, typeof(BloggingContext));

        result.ShouldStartWith(LF(
            """
            using System;
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Microsoft.EntityFrameworkCore.Migrations;
            using Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture;
            using Zeta;

            """));
    }

    [Fact]
    public void AddMetadataAttributes_WhenCrLfLineEndings_ThenKeepsCrLf()
    {
        var code = ScaffoldedMigration.ReplaceLineEndings("\r\n");

        var result = MigrationCodeRewriter.AddMetadataAttributes(code, MigrationId, typeof(BloggingContext));

        result.ShouldContain("[DbContext(typeof(BloggingContext))]\r\n    [Migration(\"20261112120000_AddThing\")]\r\n    public partial class");
        result.Replace("\r\n", string.Empty, StringComparison.Ordinal).ShouldNotContain('\n');
    }

    [Fact]
    public void AddMetadataAttributes_WhenFileScopedNamespace_ThenAddsAttributesWithoutIndent()
    {
        var code = LF(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            namespace App.Migrations;

            public partial class AddThing : Migration
            {
            }
            """);

        var result = MigrationCodeRewriter.AddMetadataAttributes(code, MigrationId, typeof(BloggingContext));

        result.ShouldContain("\n[DbContext(typeof(BloggingContext))]\n[Migration(\"20261112120000_AddThing\")]\npublic partial class AddThing");
    }

    [Fact]
    public void AddMetadataAttributes_WhenContextIsNested_ThenReferencesItThroughDeclaringType()
    {
        var result = MigrationCodeRewriter.AddMetadataAttributes(ScaffoldedMigration, MigrationId, typeof(Outer.NestedContext));

        result.ShouldContain("[DbContext(typeof(MigrationCodeRewriterTests.Outer.NestedContext))]");
        result.ShouldContain("using Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests;");
    }

    [Fact]
    public void AddMetadataAttributes_WhenNoMigrationClass_ThenThrows()
    {
        var code = ScaffoldedMigration.Replace(" : Migration", " : SomethingElse", StringComparison.Ordinal);

        var exception = Should.Throw<InvalidOperationException>(
            () => MigrationCodeRewriter.AddMetadataAttributes(code, MigrationId, typeof(BloggingContext)));

        exception.Message.ShouldContain("found 0");
    }

    [Fact]
    public void AddMetadataAttributes_WhenTwoMigrationClasses_ThenThrows()
    {
        var code = ScaffoldedMigration.Replace(
            "    public partial class AddThing : Migration",
            "    public partial class AddThing : Migration\n    {\n    }\n\n    public partial class AddOther : Migration",
            StringComparison.Ordinal);

        var exception = Should.Throw<InvalidOperationException>(
            () => MigrationCodeRewriter.AddMetadataAttributes(code, MigrationId, typeof(BloggingContext)));

        exception.Message.ShouldContain("found 2");
    }

    [Fact]
    public void AddMetadataAttributes_WhenUsingsNotContiguous_ThenThrows()
    {
        var code = ScaffoldedMigration.Replace(
            "using System;\nusing Microsoft",
            "using System;\n\nusing Microsoft",
            StringComparison.Ordinal);

        Should.Throw<InvalidOperationException>(
            () => MigrationCodeRewriter.AddMetadataAttributes(code, MigrationId, typeof(BloggingContext)));
    }

    [Fact]
    public void AddMetadataAttributes_WhenMigrationIdIsEmpty_ThenThrows()
    {
        Should.Throw<ArgumentException>(
            () => MigrationCodeRewriter.AddMetadataAttributes(ScaffoldedMigration, " ", typeof(BloggingContext)));
    }

    private static string LF(string text) => text.ReplaceLineEndings("\n");

    public static class Outer
    {
        public class NestedContext;
    }
}

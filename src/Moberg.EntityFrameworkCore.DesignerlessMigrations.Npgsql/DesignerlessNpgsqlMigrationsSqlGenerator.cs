using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql;

/// <summary>
/// Designerless migrations have no target model, so the model EF hands the SQL generator is <see langword="null"/>.
/// EF then treats every migration as scaffolded before EF Core 1.1 and ignores <c>oldType</c>/<c>OldColumn</c>: an
/// <c>AlterColumn</c> that only changes nullability or a default also emits <c>ALTER COLUMN ... TYPE &lt;same type&gt;</c>,
/// which takes an ACCESS EXCLUSIVE lock and can rewrite the table. Every compiled migration was scaffolded by a modern
/// EF, so the old-column data is reliable.
/// <para>Register it with <c>UseDesignerlessMigrations()</c>. Derive from it if you need your own generator.</para>
/// </summary>
public class DesignerlessNpgsqlMigrationsSqlGenerator : NpgsqlMigrationsSqlGenerator
{
    /// <summary>Initializes a new instance of the <see cref="DesignerlessNpgsqlMigrationsSqlGenerator"/> class.</summary>
    /// <param name="dependencies">EF's SQL generator dependencies.</param>
    /// <param name="npgsqlSingletonOptions">Npgsql's singleton options.</param>
#pragma warning disable EF1001 // NpgsqlMigrationsSqlGenerator's constructor takes this internal options type; versions follow Npgsql majors.
    public DesignerlessNpgsqlMigrationsSqlGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        INpgsqlSingletonOptions npgsqlSingletonOptions)
        : base(dependencies, npgsqlSingletonOptions)
    {
    }
#pragma warning restore EF1001

    /// <summary>Always <see langword="true"/>: honour <c>oldType</c> and friends even without a target model.</summary>
    /// <inheritdoc />
    protected override bool IsOldColumnSupported(IModel? model) => true;

    /// <summary>Always <see langword="false"/>: no designerless migration predates EF Core 2.0's rename operations.</summary>
    /// <inheritdoc />
    protected override bool HasLegacyRenameOperations(IModel? model) => false;
}

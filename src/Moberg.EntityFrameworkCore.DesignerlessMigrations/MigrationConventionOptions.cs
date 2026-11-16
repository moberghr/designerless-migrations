namespace Moberg.EntityFrameworkCore.DesignerlessMigrations;

/// <summary>Which conventions <see cref="MigrationConventions.FindViolations"/> enforces.</summary>
public sealed class MigrationConventionOptions
{
    /// <summary>
    /// Allow migrations to declare <c>Down()</c>. Defaults to <see langword="false"/>: migrations are forward-only and
    /// <c>Down()</c> is deleted before a PR. Set it for projects that revert migrations.
    /// </summary>
    public bool AllowDown { get; init; }

    /// <summary>
    /// Require <c>InsertData</c>, <c>UpdateData</c> and <c>DeleteData</c> to pass column types. Defaults to <see langword="true"/>.
    /// Checking it builds each migration's operations (runs <c>Up</c>, and <c>Down</c> when declared, against a
    /// <c>MigrationBuilder</c>; nothing touches the database).
    /// </summary>
    public bool RequireDataOperationColumnTypes { get; init; } = true;
}

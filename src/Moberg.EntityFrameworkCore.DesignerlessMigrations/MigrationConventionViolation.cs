namespace Moberg.EntityFrameworkCore.DesignerlessMigrations;

/// <summary>A migration that breaks a designerless convention.</summary>
/// <param name="MigrationType">The migration class.</param>
/// <param name="Rule">The convention it breaks.</param>
/// <param name="Message">What is wrong and how to fix it.</param>
public sealed record MigrationConventionViolation(Type MigrationType, MigrationConventionRule Rule, string Message)
{
    /// <inheritdoc />
    public override string ToString() => Message;
}

/// <summary>The conventions checked by <see cref="MigrationConventions.FindViolations"/>.</summary>
public enum MigrationConventionRule
{
    /// <summary>The class lacks <c>[DbContext]</c> or <c>[Migration]</c>, so EF silently skips it.</summary>
    NotDiscovered,

    /// <summary>The migration still has a <c>.Designer.cs</c> file (it declares <c>BuildTargetModel</c>).</summary>
    HasDesignerFile,

    /// <summary>The migration declares <c>Down()</c> while <see cref="MigrationConventionOptions.AllowDown"/> is off.</summary>
    DeclaresDown,

    /// <summary>An <c>InsertData</c>, <c>UpdateData</c> or <c>DeleteData</c> operation has no column types.</summary>
    DataOperationWithoutColumnTypes,
}

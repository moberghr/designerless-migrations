using System.Text.RegularExpressions;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Scaffolding;

/// <summary>
/// Moves the metadata EF normally keeps in <c>.Designer.cs</c> into the migration file itself.
/// EF discovers migrations only through <c>[Migration]</c> (ordering, history id) and <c>[DbContext]</c>;
/// without them a migration is silently skipped, so both must be on the class.
/// </summary>
internal static partial class MigrationCodeRewriter
{
    public static string AddMetadataAttributes(string migrationCode, string migrationId, Type contextType)
    {
        ArgumentNullException.ThrowIfNull(migrationCode);
        ArgumentNullException.ThrowIfNull(contextType);

        if (string.IsNullOrWhiteSpace(migrationId))
        {
            throw new ArgumentException("Migration id is required.", nameof(migrationId));
        }

        if (contextType.IsGenericType)
        {
            throw new ArgumentException($"Generic DbContext types are not supported: '{contextType}'.", nameof(contextType));
        }

        var outermostType = contextType;

        while (outermostType.DeclaringType != null)
        {
            outermostType = outermostType.DeclaringType;
        }

        if (string.IsNullOrEmpty(outermostType.Namespace))
        {
            throw new ArgumentException("The DbContext type must be declared in a namespace.", nameof(contextType));
        }

        var classDeclarations = MigrationClass().Matches(migrationCode);

        if (classDeclarations.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one 'public partial class X : Migration' declaration in the scaffolded migration, found {classDeclarations.Count}.");
        }

        var newLine = migrationCode.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var classDeclaration = classDeclarations[0];
        var indent = classDeclaration.Groups["indent"].Value;
        var attributes =
            $"{indent}[DbContext(typeof({GetTypeReference(contextType)}))]{newLine}" +
            $"{indent}[Migration(\"{migrationId}\")]{newLine}";

        var withAttributes = migrationCode.Insert(classDeclaration.Index, attributes);

        return AddUsings(
            withAttributes,
            newLine,
            "Microsoft.EntityFrameworkCore.Infrastructure",
            "Microsoft.EntityFrameworkCore.Migrations",
            outermostType.Namespace);
    }

    // Nested types are referenced through their declaring types (Outer.Inner); the namespace comes from a using.
    private static string GetTypeReference(Type type) =>
        type.DeclaringType == null ? type.Name : GetTypeReference(type.DeclaringType) + "." + type.Name;

    private static string AddUsings(string code, string newLine, params string[] namespaces)
    {
        var usings = UsingDirective().Matches(code);

        if (usings.Count == 0)
        {
            throw new InvalidOperationException("Expected the scaffolded migration to contain using directives.");
        }

        var start = usings[0].Index;
        var end = usings[^1].Index + usings[^1].Value.TrimEnd('\r').Length;
        var usingBlockLines = code[start..end].Split('\n');

        if (usingBlockLines.Length != usings.Count)
        {
            throw new InvalidOperationException("Expected the using directives in the scaffolded migration to be contiguous.");
        }

        var sorted = usings
            .Select(x => x.Groups["namespace"].Value)
            .Concat(namespaces)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x == "System" || x.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(x => x, StringComparer.Ordinal)
            .Select(x => $"using {x};");

        return code[..start] + string.Join(newLine, sorted) + code[end..];
    }

    [GeneratedRegex(@"^(?<indent>[ \t]*)public partial class \w+ : Migration\b", RegexOptions.Multiline)]
    private static partial Regex MigrationClass();

    [GeneratedRegex(@"^using (?<namespace>[\w.]+);\r?$", RegexOptions.Multiline)]
    private static partial Regex UsingDirective();
}

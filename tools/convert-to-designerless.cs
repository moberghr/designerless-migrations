#:package Microsoft.CodeAnalysis.CSharp@5.12.0
#:property PublishAot=false
#:property Nullable=enable
#:property ImplicitUsings=enable

// One-time conversion of an existing EF Core migrations folder to designerless migrations.
//
//   dotnet run tools/convert-to-designerless.cs -- <migrations directory> [--remove-down] [--dry-run]
//
// For every <id>.Designer.cs (searched recursively): moves [DbContext] and [Migration] from the Designer file onto the
// migration class, adds the usings they need, deletes the Designer file and, with --remove-down, deletes Down().
// Every pair is validated before anything is written; on any error nothing changes. BOM and line endings are kept.
// The model snapshot is not touched.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var positional = args.Where(x => !x.StartsWith("--", StringComparison.Ordinal)).ToList();
var unknown = args.Where(x => x.StartsWith("--", StringComparison.Ordinal) && x is not ("--remove-down" or "--dry-run")).ToList();

if (positional.Count != 1 || unknown.Count > 0)
{
    Console.Error.WriteLine("Usage: dotnet run tools/convert-to-designerless.cs -- <migrations directory> [--remove-down] [--dry-run]");
    return 2;
}

var directory = Path.GetFullPath(positional[0]);
var removeDown = args.Contains("--remove-down");
var dryRun = args.Contains("--dry-run");

if (!Directory.Exists(directory))
{
    Console.Error.WriteLine($"Directory not found: {directory}");
    return 2;
}

var designerFiles = Directory.GetFiles(directory, "*.Designer.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
var conversions = new List<Conversion>();
var errors = new List<string>();

foreach (var designerFile in designerFiles)
{
    try
    {
        conversions.Add(Converter.Plan(designerFile, removeDown));
    }
    catch (ConversionException ex)
    {
        errors.Add($"{Path.GetRelativePath(directory, designerFile)}: {ex.Message}");
    }
}

if (errors.Count > 0)
{
    Console.Error.WriteLine($"Nothing was changed. {errors.Count} of {designerFiles.Count} Designer files cannot be converted:");
    errors.ForEach(x => Console.Error.WriteLine("  " + x));
    return 1;
}

foreach (var conversion in conversions)
{
    var name = Path.GetRelativePath(directory, conversion.MigrationFile);
    var details = new List<string> { "attributes moved in" };

    if (conversion.AddedUsings.Count > 0)
    {
        details.Add("added " + string.Join(", ", conversion.AddedUsings.Select(x => $"using {x};")));
    }

    if (conversion.RemovedDown)
    {
        details.Add("Down() removed");
    }

    Console.WriteLine($"{(dryRun ? "Would convert" : "Converted")} {name}: {string.Join("; ", details)}");

    if (!dryRun)
    {
        File.WriteAllBytes(conversion.MigrationFile, conversion.NewContent);
        File.Delete(conversion.DesignerFile);
    }
}

Console.WriteLine($"{(dryRun ? "Dry run: would convert" : "Converted")} {conversions.Count} migration(s).");

return 0;

internal sealed record Conversion(
    string DesignerFile,
    string MigrationFile,
    byte[] NewContent,
    IReadOnlyList<string> AddedUsings,
    bool RemovedDown);

internal sealed class ConversionException(string message) : Exception(message);

internal static class Converter
{
    private static readonly byte[] Utf8Bom = Encoding.UTF8.GetPreamble();

    // Namespaces the Designer file imports for its own model-building code. Any other using is assumed to be what
    // brings the DbContext type into scope.
    private static readonly string[] DesignerOnlyNamespacePrefixes = ["System", "Microsoft.EntityFrameworkCore", "Npgsql"];

    public static Conversion Plan(string designerFile, bool removeDown)
    {
        var migrationFile = designerFile[..^".Designer.cs".Length] + ".cs";

        if (!File.Exists(migrationFile))
        {
            throw new ConversionException($"no matching migration file '{Path.GetFileName(migrationFile)}'.");
        }

        var designer = Parse(File.ReadAllBytes(designerFile), out _, out _);
        var designerClass = FindSingleClass(designer, x => HasAttribute(x, "Migration"), "a class with [Migration]");
        var migrationAttribute = GetAttribute(designerClass, "Migration")
            ?? throw new ConversionException("the Designer class has no [Migration].");
        var dbContextAttribute = GetAttribute(designerClass, "DbContext")
            ?? throw new ConversionException("the Designer class has no [DbContext].");
        var migrationId = GetStringArgument(migrationAttribute);
        var expectedFileName = migrationId + ".cs";

        if (!string.Equals(Path.GetFileName(migrationFile), expectedFileName, StringComparison.Ordinal))
        {
            throw new ConversionException($"[Migration(\"{migrationId}\")] does not match the file name; expected '{expectedFileName}'.");
        }

        var unexpectedMembers = designerClass.Members
            .Where(x => x is not MethodDeclarationSyntax { Identifier.Text: "BuildTargetModel" })
            .ToList();

        if (unexpectedMembers.Count > 0)
        {
            throw new ConversionException("the Designer class has members other than BuildTargetModel; move them by hand first.");
        }

        var originalBytes = File.ReadAllBytes(migrationFile);
        var migration = Parse(originalBytes, out var hasBom, out var text);
        var className = designerClass.Identifier.Text;
        var migrationClass = FindSingleClass(migration, x => x.Identifier.Text == className, $"class '{className}'");

        if (HasAttribute(migrationClass, "Migration") || HasAttribute(migrationClass, "DbContext"))
        {
            throw new ConversionException("the migration class already has [Migration] or [DbContext]; delete the Designer file by hand.");
        }

        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var edits = new List<(int Start, int Length, string Text)>();

        // Attributes go on their own lines right above the declaration, after any doc comment.
        var declarationStart = migrationClass.GetFirstToken().SpanStart;
        var lineStart = text.LastIndexOf('\n', declarationStart - 1) + 1;
        var indent = text[lineStart..declarationStart];

        if (indent.Any(x => !char.IsWhiteSpace(x)))
        {
            throw new ConversionException($"class '{className}' does not start its own line.");
        }

        edits.Add((lineStart, 0, $"{indent}[{dbContextAttribute}]{newLine}{indent}[{migrationAttribute}]{newLine}"));

        var down = migrationClass.Members
            .OfType<MethodDeclarationSyntax>()
            .SingleOrDefault(x => x.Identifier.Text == "Down" && x.Modifiers.Any(SyntaxKind.OverrideKeyword));
        var removedDown = removeDown && down != null;

        if (removedDown)
        {
            // Full span: the blank line and doc comment before it, and its own line ending.
            edits.Add((down!.FullSpan.Start, down.FullSpan.Length, string.Empty));
        }

        var existingUsings = migration.Usings.Select(x => x.Name!.ToString()).ToHashSet(StringComparer.Ordinal);
        var contextUsings = designer.Usings
            .Select(x => x.Name!.ToString())
            .Where(x => !DesignerOnlyNamespacePrefixes.Any(prefix => x == prefix || x.StartsWith(prefix + ".", StringComparison.Ordinal)));
        var addedUsings = new[] { "Microsoft.EntityFrameworkCore.Infrastructure", "Microsoft.EntityFrameworkCore.Migrations" }
            .Concat(contextUsings)
            .Where(x => !existingUsings.Contains(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (addedUsings.Count > 0)
        {
            edits.Add(UsingsEdit(migration, text, newLine, addedUsings));
        }

        var newText = text;

        foreach (var (start, length, replacement) in edits.OrderByDescending(x => x.Start))
        {
            newText = newText.Remove(start, length).Insert(start, replacement);
        }

        byte[] newContent = hasBom ? [.. Utf8Bom, .. Encoding.UTF8.GetBytes(newText)] : Encoding.UTF8.GetBytes(newText);

        return new Conversion(designerFile, migrationFile, newContent, addedUsings, removedDown);
    }

    // Re-sorts a plain, contiguous using block (System first, then ordinal, like the scaffolder); otherwise appends.
    private static (int Start, int Length, string Text) UsingsEdit(CompilationUnitSyntax unit, string text, string newLine, List<string> added)
    {
        var usings = unit.Usings;

        if (usings.Count == 0)
        {
            var start = unit.GetFirstToken(includeZeroWidth: true).FullSpan.Start;

            return (start, 0, string.Concat(added.Order(StringComparer.Ordinal).Select(x => $"using {x};{newLine}")) + newLine);
        }

        var plain = usings.All(x => x.Alias == null && x.StaticKeyword.IsKind(SyntaxKind.None) && x.GlobalKeyword.IsKind(SyntaxKind.None))
            && usings.Skip(1).All(x => x.GetLeadingTrivia().All(t => t.IsKind(SyntaxKind.WhitespaceTrivia)));

        if (!plain)
        {
            var end = usings[^1].Span.End;

            return (end, 0, string.Concat(added.Select(x => $"{newLine}using {x};")));
        }

        var blockStart = usings[0].Span.Start;
        var blockEnd = usings[^1].Span.End;
        var sorted = usings
            .Select(x => x.Name!.ToString())
            .Concat(added)
            .OrderBy(x => x == "System" || x.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(x => x, StringComparer.Ordinal)
            .Select(x => $"using {x};");

        return (blockStart, blockEnd - blockStart, string.Join(newLine, sorted));
    }

    private static CompilationUnitSyntax Parse(byte[] bytes, out bool hasBom, out string text)
    {
        hasBom = bytes.AsSpan().StartsWith(Utf8Bom);
        text = Encoding.UTF8.GetString(hasBom ? bytes[Utf8Bom.Length..] : bytes);

        return CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
    }

    private static ClassDeclarationSyntax FindSingleClass(CompilationUnitSyntax unit, Func<ClassDeclarationSyntax, bool> predicate, string description)
    {
        var matches = unit.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(predicate).ToList();

        return matches.Count == 1
            ? matches[0]
            : throw new ConversionException($"expected exactly one {description}, found {matches.Count}.");
    }

    private static AttributeSyntax? GetAttribute(ClassDeclarationSyntax type, string name) =>
        type.AttributeLists
            .SelectMany(x => x.Attributes)
            .SingleOrDefault(x => AttributeName(x) == name || AttributeName(x) == name + "Attribute");

    private static bool HasAttribute(ClassDeclarationSyntax type, string name) => GetAttribute(type, name) != null;

    private static string AttributeName(AttributeSyntax attribute) => attribute.Name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        _ => attribute.Name.ToString(),
    };

    private static string GetStringArgument(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments.SingleOrDefault()?.Expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
            ? literal.Token.ValueText
            : throw new ConversionException("[Migration] must have a single string literal argument.");
}

# Designerless EF Core migrations

Single-file EF Core migrations. `dotnet ef migrations add` writes **one file** with `[DbContext]` and `[Migration]` on the class, and **no `.Designer.cs`**. Each Designer file is a full copy of the model, often tens of thousands of lines, and a large project spends a big share of its build time compiling them.

You reference the package and that's it: no extra tool, and no design-time project in your repo.

| Package | Kind | What it does |
| --- | --- | --- |
| `Moberg.EntityFrameworkCore.DesignerlessMigrations` | design time (development dependency) | Scaffolder picked up by `dotnet ef` automatically. Safe `migrations remove`. Convention checks for tests. Never published with your app. |
| `Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql` | runtime | `UseDesignerlessMigrations()`: makes migrations without a Designer file generate the same SQL as with one. |

Package versions follow EF Core majors: **10.x is for EF Core 10** (`net10.0`, Npgsql.EntityFrameworkCore.PostgreSQL 10). Only the major is tied to EF: minor and patch are this project's own (`10.0.1` is a fix to these packages, not a build for EF 10.0.1), and every 10.x release works with any EF Core 10 patch from 10.0.2 on.

## Quick start: a new project

**1. Reference both packages** from the project `dotnet ef` uses as its startup project (usually the app that applies migrations):

```sh
dotnet add package Moberg.EntityFrameworkCore.DesignerlessMigrations
dotnet add package Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql
```

**2. Add `UseDesignerlessMigrations()` after `UseNpgsql(...)`.** That is the only code change:

```csharp
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("App"))
    .UseDesignerlessMigrations());
```

Use the same options wherever the context is configured for migrations: the app, an `IDesignTimeDbContextFactory` if you have one, and tests.

**3. Add migrations as usual:**

```sh
dotnet ef migrations add Initial
```

You get `Migrations/20260101120000_Initial.cs` with the attributes on the class, plus the model snapshot. No Designer file is written.

```csharp
[DbContext(typeof(AppDbContext))]
[Migration("20260101120000_Initial")]
public partial class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) { ... }
}
```

**4. Add the convention test** (recommended). Reference the design package from your test project with a plain reference. The `dotnet add package` form shown in [Setup details](#setup-details) leaves out compile assets, so the test couldn't call it:

```xml
<PackageReference Include="Moberg.EntityFrameworkCore.DesignerlessMigrations" Version="10.0.0" />
```

```csharp
[Fact]
public void Migrations_FollowDesignerlessConventions()
{
    using var context = new AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost").UseDesignerlessMigrations().Options);

    MigrationConventions.FindViolations(context).ShouldBeEmpty();
}
```

The test doesn't connect to a database. [`samples/NpgsqlSample`](samples/NpgsqlSample) and [`samples/NpgsqlSample.Tests`](samples/NpgsqlSample.Tests) are a complete working consumer.

## What you get

**Scaffolder.** `migrations add` puts `[DbContext]` and `[Migration]` on the migration class and writes no Designer file. Migrations stay plain C#, so you can edit them.

**Runtime SQL fix.** Without a Designer file, EF hands the SQL generator no target model. EF then treats the migration as if it were scaffolded before EF Core 1.1 and ignores `oldType`. An `AlterColumn` that only changes nullability or a default would also emit `ALTER COLUMN ... TYPE <same type>`. That statement takes an ACCESS EXCLUSIVE lock and can rewrite the table. `UseDesignerlessMigrations()` replaces the Npgsql SQL generator with one that keeps the old-column information, so the SQL matches what you'd get with Designer files.

**Safe `migrations remove`.** EF normally rebuilds the previous snapshot from the Designer file. Here the snapshot is restored from git instead (see [below](#migrations-remove)), and removal refuses whenever that could be wrong.

**Convention checks.** `MigrationConventions.FindViolations(context, options)` returns every violation, each with a message that says how to fix it:

| Rule | Meaning |
| --- | --- |
| `NotDiscovered` | The class lacks `[DbContext]` or `[Migration]`. EF skips such a migration **silently**. |
| `HasDesignerFile` | A `.Designer.cs` is still there (the class declares `BuildTargetModel`). |
| `DeclaresDown` | The migration declares `Down()` and `AllowDown` is off. |
| `DataOperationWithoutColumnTypes` | `InsertData`, `UpdateData` or `DeleteData` lacks column types. Without a model, EF throws when the migration is **applied**, i.e. at deploy time. |

Options: `new MigrationConventionOptions { AllowDown = true }` for projects that keep `Down()`. `RequireDataOperationColumnTypes = false` turns off the data-operation check, which builds each migration's operations (no database involved).

## Workflow

- **Add:** `dotnet ef migrations add <Name>` writes one file.
- **Iterate locally:** `Down()` is still generated, so `dotnet ef database update <Previous>` and `migrations remove --force` work while you iterate.
- **Forward-only (optional policy):** if your project never reverts migrations, delete `Down()` **entirely** before opening a PR, and keep `AllowDown` off so the convention test enforces it. Don't leave an empty `Down()`: reverting it would delete the history row and leave the schema in place. With no `Down()`, EF refuses to revert (`NotSupportedException`).
- **Data operations need column types**, because there is no model to look them up in:

  ```csharp
  migrationBuilder.InsertData(
      table: "Currencies",
      columns: ["Code", "Decimals"],
      columnTypes: ["text", "integer"],
      values: new object[,] { { "EUR", 2 }, { "JPY", 0 } });
  ```

  `UpdateData` needs both `columnTypes:` and `keyColumnTypes:`. `DeleteData` needs `keyColumnTypes:`.

### `migrations remove`

There is no Designer file to rebuild the previous snapshot from, so the snapshot is restored from git. It takes the version from just before the migration was added: `HEAD` for an uncommitted migration, or the parent of the commit that added it. The file is restored with `git cat-file --filters`, so its line endings and BOM match a checkout.

`migrations remove` refuses, and changes nothing, when:

- **A newer migration file is on disk than in the compiled assembly,** e.g. `migrations add X` followed by `migrations remove --no-build`. EF would otherwise delete the previous migration, which may already be deployed.
- **Another migration on disk isn't in the git revision being restored.** For example, two uncommitted migrations, or two added in the same commit. Restoring would silently drop that migration's model changes. Delete the migrations, restore the snapshot (`git checkout <revision> -- <snapshot>`), and add one combined migration.
- **The migration is applied** and `--force` isn't given. This is the same as EF. With `--force` the migration is reverted through its `Down()` first.
- **git isn't on PATH, or the project isn't in a git work tree.** The error tells you what to do by hand: revert the migration if it's applied, delete the migration file, and restore the snapshot from your version control.

Removing through the API with `dryRun: true` never touches the database; it only reports what it would revert.

## Setup details

- **The startup project must reference the design package,** as with `Microsoft.EntityFrameworkCore.Design`. The package's `build/*.props` adds `[assembly: DesignTimeServicesReference(...)]` to that project, and `dotnet ef` reads it from the startup assembly. The attribute lives in `Microsoft.EntityFrameworkCore`, so nothing extra is needed to compile it, and at runtime it is only a string. `dotnet add package` writes it like this:

  ```xml
  <PackageReference Include="Moberg.EntityFrameworkCore.DesignerlessMigrations" Version="10.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  ```

- **It is never published.** It is a development dependency, like EF Design, so neither it nor EF Design ends up in `dotnet publish` output. The end-to-end tests check this against a real publish.
- **`<GenerateAssemblyInfo>false</GenerateAssemblyInfo>`** turns off MSBuild's assembly attributes, including this one. In that case add it yourself: `[assembly: Microsoft.EntityFrameworkCore.Design.DesignTimeServicesReference("Moberg.EntityFrameworkCore.DesignerlessMigrations.DesignerlessDesignTimeServices, Moberg.EntityFrameworkCore.DesignerlessMigrations")]`.
- **Your own `IDesignTimeServices`** runs after this one, so it can still override `IMigrationsScaffolder` (e.g. with a subclass of `DesignerlessMigrationsScaffolder`). EF uses only the first `IDesignTimeServices` it finds in the startup assembly. To register the scaffolder yourself instead, set `<DesignerlessMigrationsRegisterDesignTimeServices>false</DesignerlessMigrationsRegisterDesignTimeServices>`.
- **Your own SQL generator:** derive from `DesignerlessNpgsqlMigrationsSqlGenerator` and replace `IMigrationsSqlGenerator` with it, instead of calling `UseDesignerlessMigrations()`.
- **Other providers:** the scaffolder, `migrations remove` and the convention checks work with any provider, but the SQL fix exists only for PostgreSQL. SQL Server and others have the same `oldType` problem without a Designer model.

## Pitfalls

1. **Never let a deployed assembly derive types from EF Design.** EF Design is never published, so `Assembly.GetTypes()` on an assembly containing such a type throws in production. With this package your assembly contains none: the scaffolder lives in the package. If you had your own scaffolder or `IDesignTimeServices` deriving from Design types, delete it.
2. **No target model means EF ignores `oldType`.** `UseDesignerlessMigrations()` fixes that for PostgreSQL. Use it at runtime too, not only for `dotnet ef`.
3. **Data operations need column types** (see [Workflow](#workflow)). The convention check catches it in a test instead of at deploy time.
4. **`migrations remove` is git-based** and refuses rather than guesses (see [`migrations remove`](#migrations-remove)).
5. **A missing `[Migration]` is silent:** EF skips the migration and never applies it. The `NotDiscovered` check exists for that.

## Converting an existing project

A one-time converter ships as a .NET 10 file-based app. Run it from a clone of this repository against your migrations folder:

```sh
dotnet run tools/convert-to-designerless.cs -- path/to/YourProject/Migrations --dry-run
dotnet run tools/convert-to-designerless.cs -- path/to/YourProject/Migrations [--remove-down]
```

For every `<id>.Designer.cs`, searched recursively, the converter:

- moves `[DbContext]` and `[Migration]` onto the migration class and adds the usings they need;
- deletes the Designer file;
- with `--remove-down`, deletes `Down()` together with its doc comment.

It first checks that the Designer's `[Migration("<id>")]` matches the file name. If any check fails, nothing is written. BOM and line endings are kept. The model snapshot is not touched.

Suggested steps:

1. Save the current SQL: `dotnet ef migrations script 0 -o before.sql`.
2. Reference the packages, add `UseDesignerlessMigrations()`, and delete any scaffolder or design-time services of your own.
3. Run the converter, then the convention test. Add column types where it reports data operations.
4. Script again and compare: `dotnet ef migrations script 0 -o after.sql`. The SQL should be byte-identical.

## Development

```sh
dotnet build DesignerlessMigrations.slnx
dotnet test --solution DesignerlessMigrations.slnx   # unit and end-to-end tests; needs Docker and git
eng/test-samples.sh                                  # packs a unique version, tests samples/ against it
```

- `tests/*.Tests`: unit tests. The scaffolder is built through EF's own `DesignTimeServicesBuilder`, with the package's real build props.
- `tests/*.EndToEnd.Tests`: packs both packages and copies `samples/NpgsqlSample` into a temporary git repository. It then runs the real `dotnet ef` (add, remove and its guards, script), `dotnet publish`, and the converter.
- `samples/`: a standalone consumer that references the packages by version.

To release, publish a GitHub release tagged with the version (e.g. `v10.0.1`). [`release.yml`](.github/workflows/release.yml) builds, tests, packs under that version and pushes to nuget.org with trusted publishing.

## License

[MIT](LICENSE)

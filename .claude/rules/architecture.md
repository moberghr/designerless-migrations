---
paths:
  - "src/**"
---

# Architecture (§2)

## §2.1 Two packages, split by when they run
- [ENFORCED] `Moberg.EntityFrameworkCore.DesignerlessMigrations` is design-time only: `<DevelopmentDependency>true</DevelopmentDependency>`, depends on EF Design. Evidence: `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/Moberg.EntityFrameworkCore.DesignerlessMigrations.csproj`.
- [ENFORCED] `Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql` ships to production and depends only on `Npgsql.EntityFrameworkCore.PostgreSQL`. Evidence: `src/Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql/Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql.csproj`.
- When code needs EF Design, it goes in the design package — because the runtime package must load in a published app where EF Design is absent (§0.4).

## §2.2 Registration needs no consumer code
- [ENFORCED] The design package registers itself through `[assembly: DesignTimeServicesReference]`, added by `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/build/Moberg.EntityFrameworkCore.DesignerlessMigrations.props`, pointing at `DesignerlessDesignTimeServices`.
- EF applies referenced services first, then provider and EF defaults (only if missing), then the startup project's own `IDesignTimeServices`. Evidence: `DesignerlessDesignTimeServices.cs` doc comment; `ScaffoldMigration_WhenResolvedFromDesignTimeServices_ThenIsDesignerlessScaffolder` in `tests/Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests/DesignerlessMigrationsScaffolderTests.cs`.
- When renaming `DesignerlessDesignTimeServices` or its namespace, update the props string and the README fallback attribute — because EF resolves the type by name with `Type.GetType`.
- [ENFORCED] Runtime registration is `UseDesignerlessMigrations()`, which requires `UseNpgsql` first and calls `ReplaceService<IMigrationsSqlGenerator, DesignerlessNpgsqlMigrationsSqlGenerator>`. Evidence: `src/Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql/DesignerlessMigrationsDbContextOptionsBuilderExtensions.cs`.

## §2.3 Small public API
- [ENFORCED] The public surface is eight types: `DesignerlessDesignTimeServices`, `DesignerlessMigrationsScaffolder`, `MigrationConventions`, `MigrationConventionOptions`, `MigrationConventionViolation`, `MigrationConventionRule`, `DesignerlessNpgsqlMigrationsSqlGenerator`, `DesignerlessMigrationsDbContextOptionsBuilderExtensions`. Everything else is `internal` under `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/Scaffolding/`, visible to `Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests` only.
- When adding a public type or member, document it with XML comments — because `GenerateDocumentationFile` is on (`src/Directory.Build.props`) and a missing comment fails the build.
- Do not name a namespace `*.Internal` — because the EF analyzer reports EF1001 on any type in such a namespace, including ours.

## §2.4 EF internals
- [ENFORCED] EF1001 is suppressed only locally, with `#pragma warning disable EF1001` around the single member that needs it and a comment saying why. Evidence: `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/DesignerlessMigrationsScaffolder.cs` (operation reporter), `src/Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql/DesignerlessNpgsqlMigrationsSqlGenerator.cs` (constructor).
- Prefer a public alternative over an internal type: the Npgsql check in `UseDesignerlessMigrations()` compares provider extension assemblies instead of using `NpgsqlOptionsExtension`.

## §2.5 Errors that reach `dotnet ef`
- [CONVENTION] Failures a user can act on throw `OperationException` with what happened and what to do next (15 sites under `src/`). Programming errors in `MigrationCodeRewriter` throw `InvalidOperationException` / `ArgumentException`.
- When a guard refuses, it refuses before changing anything: snapshot resolution happens before the database check, and both before any file write. Evidence: `DesignerlessMigrationsScaffolder.RemoveMigration`.

## §2.6 Dependency floors
- [ENFORCED] Library dependencies sit at the lowest version without known vulnerabilities (EF Design and Relational 10.0.2, Npgsql 10.0.0); tests raise them with `VersionOverride="$(EfCoreTestVersion)"` / `$(NpgsqlEfCoreTestVersion)`. Evidence: `Directory.Packages.props`.

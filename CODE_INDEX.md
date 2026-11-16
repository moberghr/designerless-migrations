# Code Index

> Capability index — what the codebase can do, not where files live.
> Refresh: `bash scripts/build-code-index.sh` (or `/mtk find duplicate capabilities`).
> Last built: 2026-11-16

## Scaffolding (design-time package)

| Capability | Entry point | Notes |
|---|---|---|
| register the scaffolder with dotnet ef | `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/DesignerlessDesignTimeServices.cs:DesignerlessDesignTimeServices` | wired by `build/*.props`; consumers write no code |
| scaffold and save a single-file migration | `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/DesignerlessMigrationsScaffolder.cs:DesignerlessMigrationsScaffolder` | `ScaffoldMigration` + `Save`; never writes a Designer file |
| remove the newest migration safely | `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/DesignerlessMigrationsScaffolder.cs:RemoveMigration` | replaces EF's entirely; refuses before acting |
| add `[DbContext]`/`[Migration]` and usings to generated code | `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/Scaffolding/MigrationCodeRewriter.cs:MigrationCodeRewriter` | keeps line endings; fails on unexpected shape |
| detect a stale assembly (newer migration file on disk) | `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/Scaffolding/MigrationFileGuard.cs:MigrationFileGuard` | also `IsMigrationFile` for the file-name pattern |
| restore the pre-migration snapshot from git | `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/Scaffolding/GitModelSnapshot.cs:GitModelSnapshot` | bytes via `git cat-file --filters`; refuses unsafe revisions |
| run git and capture raw stdout | `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/Scaffolding/Git.cs:Git` | `GitNotFoundException` when git can't start |

## Conventions (design-time package, used from tests)

| Capability | Entry point | Notes |
|---|---|---|
| list migrations that break designerless conventions | `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/MigrationConventions.cs:MigrationConventions` | options in `MigrationConventionOptions`; rules in `MigrationConventionRule` |

## Runtime (Npgsql package)

| Capability | Entry point | Notes |
|---|---|---|
| register the designerless SQL generator | `src/Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql/DesignerlessMigrationsDbContextOptionsBuilderExtensions.cs:UseDesignerlessMigrations` | call after `UseNpgsql` |
| generate migration SQL without a target model | `src/Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql/DesignerlessNpgsqlMigrationsSqlGenerator.cs:DesignerlessNpgsqlMigrationsSqlGenerator` | subclass it for a custom generator |

## Tooling

| Capability | Entry point | Notes |
|---|---|---|
| convert an existing repo's Designer migrations | `tools/convert-to-designerless.cs:Converter` | file-based app; validates all pairs before writing |
| test the samples against freshly packed packages | `eng/test-samples.sh` | unique version per run |

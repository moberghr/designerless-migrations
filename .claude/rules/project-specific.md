---
paths:
  - "src/**"
  - "tools/**"
  - "samples/**"
  - "README.md"
---

# Project-specific (§9)

## §9.1 "Designerless", not "forward-only"
- [ENFORCED] No `Down()` is an optional consumer policy: `MigrationConventionOptions.AllowDown` defaults to `false`, and projects that revert migrations set it. Do not bake forward-only assumptions into the scaffolder or `migrations remove`. Evidence: `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/MigrationConventionOptions.cs`.
- The scaffolder still generates `Down()` so local iteration works; deleting it is the consumer's PR step.

## §9.2 `migrations remove` is git-based
- [ENFORCED] The previous snapshot comes from git: `HEAD` for an uncommitted migration, otherwise the parent of the commit that added it. Saving a snapshot at add time was considered and rejected. Evidence: `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/Scaffolding/GitModelSnapshot.cs`, `.claude/references/decisions.md`.
- [ENFORCED] It refuses, changing nothing, when: a newer migration file is on disk than compiled; another migration on disk is missing from the restored revision; the migration is applied without `--force`; git is missing or the folder is not in a work tree (with manual steps in the message).
- [ENFORCED] A dry run never calls `Migrator.Migrate`. `dotnet ef migrations remove` has no `--dry-run` flag; the path is API-only and covered by `RemoveMigration_WhenDryRunAndMigrationIsApplied_ThenDoesNotRevertOrChangeFiles`.
- EF's own `RemoveMigration` is replaced entirely, never called — because it reads the Designer target model and would delete the snapshot.

## §9.3 Convention checks
- [ENFORCED] `MigrationConventions.FindViolations` returns violations (never throws on a violation) so a consumer test is one assertion. Migrations attributed to another `DbContext` are ignored; unattributed ones are reported. Each message says how to fix it.
- When adding a rule, add a `MigrationConventionRule` member, a fixture in `tests/Moberg.EntityFrameworkCore.DesignerlessMigrations.ConventionFixtures/Migrations.cs` and a README table row.

## §9.4 Converter
- [ENFORCED] `tools/convert-to-designerless.cs` is a .NET 10 file-based app (`dotnet run tools/convert-to-designerless.cs -- <dir>`), isolated from repo build settings by `tools/Directory.Build.props` and `tools/Directory.Packages.props`. It validates every Designer pair before writing anything and never touches the snapshot.

## §9.5 README
- [ENFORCED] `README.md` opens with the new-project setup (two package references, `UseDesignerlessMigrations()`, the convention test). Keep that first; update the README whenever behavior or setup changes (definition of done).

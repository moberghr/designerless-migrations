---
paths:
  - "tests/**"
  - "samples/NpgsqlSample.Tests/**"
---

# Testing (§4)

## §4.1 Stack
- [ENFORCED] xUnit v3 on Microsoft.Testing.Platform (`xunit.v3.mtp-v2`, `global.json` sets the runner), Shouldly assertions, Testcontainers.PostgreSql (`postgres:17-alpine`). Evidence: `tests/Directory.Build.props`, `tests/Moberg.EntityFrameworkCore.DesignerlessMigrations.EndToEnd.Tests/Infrastructure.cs`.
- [CONVENTION] Moq is used only for `IMigrator` and `IHistoryRepository` in remove tests. Evidence: `tests/Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests/DesignerlessMigrationsScaffolderTests.RemoveMigration.cs`.
- Filter under MTP with runner args, e.g. `dotnet test --project <csproj> --filter-class "*ConverterTests"`; `--filter <expr>` is rejected.

## §4.2 Naming
- [CONVENTION] Test methods are `Method_WhenCondition_ThenResult`, e.g. `RemoveMigration_WhenNotApplied_ThenDeletesMigrationAndRestoresSnapshot` (67 of 68). The one exception, `Migrations_FollowDesignerlessConventions` in `samples/NpgsqlSample.Tests/MigrationConventionsTests.cs`, mirrors the README snippet on purpose.

## §4.3 Call production code
- When testing scaffolding, build EF's design-time services with `DesignTimeServicesBuilder` against the fixture assembly, as `dotnet ef` does — because that proves the real `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/build/Moberg.EntityFrameworkCore.DesignerlessMigrations.props` attribute (imported by `tests/Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests/Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.csproj`) registers the scaffolder.
- Never reimplement production logic in a test or mock what an end-to-end test should run. Evidence: engineer interview — .claude/setup-answers.json (failure_modes).
- Broken-convention migrations live in their own assembly, `tests/Moberg.EntityFrameworkCore.DesignerlessMigrations.ConventionFixtures/`, so they never leak into other fixtures.

## §4.4 Line endings and git in tests
- When a test asserts on text from a raw string literal, normalize it (`LF(...)`, `Crlf(...)`) — because checkout line endings vary and the rewriter must be tested on both explicitly. Evidence: `tests/Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests/MigrationCodeRewriterTests.cs`, `tests/Moberg.EntityFrameworkCore.DesignerlessMigrations.EndToEnd.Tests/ConverterTests.cs`.
- Git-based tests use `TempGitRepository`, which sets identity, `commit.gpgsign=false` and `core.autocrlf=false` locally — because the machine's global git config must not change results.

## §4.5 End-to-end coverage
- [ENFORCED] Every guarantee about real tools is proven in `tests/Moberg.EntityFrameworkCore.DesignerlessMigrations.EndToEnd.Tests/`: `dotnet ef` add/remove/script on a `SampleWorkspace` copy of `samples/NpgsqlSample`, `dotnet publish` output, and `tools/convert-to-designerless.cs`.
- The fixture packs both packages once per run under a unique prerelease version — because the NuGet global cache would otherwise serve a stale package with the same version.
- When fixing a pitfall, add a test that fails without the fix (each pitfall in `README.md` has one).

## §4.6 Consumer tests
- `samples/NpgsqlSample.Tests/` is a real consumer: it references the packages by version (run via `bash eng/test-samples.sh`) and applies the sample migrations to PostgreSQL. Keep it free of project references to `src/`.

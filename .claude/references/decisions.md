# Decisions

> ADR-lite, append-only. A re-run may append entries; it never edits, reorders or deletes an existing one.

## 2026-11-16 — Package versions follow EF Core majors

**Context:** The scaffolder extends EF design-time APIs that are marked internal (EF1001) and may change between EF majors.
**Decision:** 10.x targets EF Core 10 and `net10.0`; the release workflow rejects non-10.x tags. Moving off EF 10 or raising a dependency floor needs approval.
**Evidence:** `.github/workflows/release.yml`; engineer interview — .claude/setup-answers.json (hard_nevers)

## 2026-11-16 — EF Design dependency floor is 10.0.2

**Context:** EF Design 10.0.0 and 10.0.1 pull System.Security.Cryptography.Xml/Pkcs 9.0.0, which have known vulnerabilities (NU1902/NU1903).
**Decision:** Library floors stay as low as possible without known vulnerabilities: EF Design and Relational 10.0.2, Npgsql 10.0.0. Tests run on the latest 10.0 patch.
**Evidence:** `Directory.Packages.props`

## 2026-11-16 — `migrations remove` restores the snapshot from git

**Context:** Without Designer files there is no per-migration model to rebuild the previous snapshot from.
**Decision:** Restore it from git (HEAD, or the parent of the commit that added the migration) and refuse whenever that could be wrong. Saving the snapshot at add time was considered and rejected for now.
**Evidence:** `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/Scaffolding/GitModelSnapshot.cs`; `README.md` (`migrations remove`)

## 2026-11-16 — No `Down()` is an optional policy

**Context:** One consumer forbids `Down()` on PRs, another keeps it.
**Decision:** Name the packages "Designerless", not "ForwardOnly"; `MigrationConventionOptions.AllowDown` lets a consumer keep `Down()`.
**Evidence:** `src/Moberg.EntityFrameworkCore.DesignerlessMigrations/MigrationConventionOptions.cs`

## 2026-11-16 — Releases go to nuget.org with trusted publishing

**Context:** Match the org's other open-source .NET library (warp).
**Decision:** Publish on a GitHub release via `release.yml`, `production` environment, OIDC login; MIT license.
**Evidence:** `.github/workflows/release.yml`; `LICENSE`

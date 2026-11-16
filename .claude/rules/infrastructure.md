---
paths:
  - ".github/workflows/**"
  - "eng/**"
  - "Directory.Build.props"
  - "Directory.Packages.props"
  - "src/Directory.Build.props"
  - "tests/Directory.Build.props"
  - "samples/Directory.Build.props"
  - "samples/Directory.Packages.props"
  - "samples/nuget.config"
  - "global.json"
  - "nuget.config"
  - ".editorconfig"
---

# Build, CI and release (§7)

## §7.1 Workflows
- [ENFORCED] `.github/workflows/ci.yml` runs on PRs and pushes to `main`: restore, build, test (EndToEnd included; Docker is on the runner), then `bash eng/test-samples.sh`.
- [ENFORCED] `.github/workflows/release.yml` runs on a published, non-draft GitHub release in the `production` environment and pushes to nuget.org with trusted publishing (`NuGet/login@v1`, `id-token: write`, `secrets.NUGET_USER`); no stored API key.
- Call repo scripts as `bash eng/<script>.sh` — because the executable bit is not recorded when committing from Windows.
- Actions are pinned to major versions matching warp (`actions/checkout@v7`, `actions/setup-dotnet@v5`) on `ubuntu-24.04`.

## §7.2 Versions
- [ENFORCED] A release takes its version from the release tag (`v` prefix optional) and rejects anything that is not `10.x`; `<Version>` in `Directory.Build.props` is only the local default.
- [ENFORCED] Pin exact package versions in `Directory.Packages.props` (central package management); never a wildcard or floating range.
- `NuGetAuditMode=all` with `TreatWarningsAsErrors` turns a vulnerable transitive package into a restore failure; fix it by raising the floor (with approval, §0.2), never by suppressing NU190x.

## §7.3 Samples are standalone
- [ENFORCED] `samples/` has its own `Directory.Build.props`, `Directory.Packages.props` and `nuget.config` and does not import the repo's — because it must consume the packages exactly as a real project does.
- `eng/test-samples.sh` packs a unique version per run into `artifacts/packages` and passes it as `DesignerlessMigrationsVersion`.

## §7.4 Analyzers and formatting
- [ENFORCED] `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, `TreatWarningsAsErrors` for all projects (`Directory.Build.props`).
- Analyzer severities live in `.editorconfig`, scoped as narrowly as possible (e.g. CA1707 only for the test projects), not in `<NoWarn>` — the org convention used by warp.
- [ENFORCED] The repository is LF (`.gitattributes` `* text=auto eol=lf`, `.editorconfig` `end_of_line = lf`).

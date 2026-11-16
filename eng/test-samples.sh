#!/usr/bin/env bash
# Packs both packages under a unique prerelease version and runs the sample consumer's tests against them.
# The unique version keeps the NuGet global cache from serving a stale package with the same version.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${DESIGNERLESS_SAMPLE_VERSION:-10.0.0-local.$(date +%Y%m%d%H%M%S)}"

echo "Packing $version"
dotnet pack "$repo_root/DesignerlessMigrations.slnx" -c Release -o "$repo_root/artifacts/packages" -p:Version="$version"

echo "Testing samples against $version"
export DesignerlessMigrationsVersion="$version"
dotnet test --project "$repo_root/samples/NpgsqlSample.Tests/NpgsqlSample.Tests.csproj" -c Release

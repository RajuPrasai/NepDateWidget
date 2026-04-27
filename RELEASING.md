# Releasing NepDate Widget

This file documents the release process for maintainers. End users do
not need to read it.

## Versioning policy

NepDate Widget follows [Semantic Versioning 2.0.0](https://semver.org)
applied to the perspective of an end user, not a library consumer:

- **MAJOR** (`x.0.0`) — incompatible changes that require user action,
  for example: settings file format change with no migration, removed
  feature, breaking change to the auto-update channel, change of
  `LOCALAPPDATA` data folder name.
- **MINOR** (`1.x.0`) — new feature, new setting, new tool, new theme,
  new language. Always backwards compatible for existing users.
- **PATCH** (`1.0.x`) — bug fix, performance fix, dependency bump
  with no behavior change, documentation fix.

Pre-release tags (`-rc1`, `-beta.1`) are allowed but never promoted to
the stable update channel until they are re-tagged without the suffix.

## Release checklist

1. Update `<Version>` in `src/NepDateWidget/NepDateWidget.csproj` to the
   target version (no `v` prefix).
2. Run the full test suite and a Release build:
   ```
   dotnet test tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj -c Release
   dotnet build src/NepDateWidget/NepDateWidget.csproj -c Release
   ```
3. Optional dry run on your local machine:
   ```
   ./build-release.ps1 -Version <x.y.z>
   ```
   Inspect `releases/` for `Setup.exe`, `<x.y.z>-full.nupkg`, and
   `RELEASES`. Install on a clean Windows VM and confirm it runs.
4. Commit any final changes with message `release: prepare vX.Y.Z`.
5. Tag the commit:
   ```
   git tag vX.Y.Z
   git push origin main --tags
   ```
6. The `Release` GitHub Actions workflow (`.github/workflows/release.yml`)
   triggers on the tag push. Watch it complete.
7. The workflow creates a **draft** GitHub Release. Edit it:
   - Write the release notes by hand. Group changes under
     **Added / Changed / Fixed / Security**. Link issues and PRs.
   - Confirm all expected files attached: the
     `NepDateWidget-vX.Y.Z-win-x64-self-contained.zip` archive,
     `checksums.sha256.txt`, `Setup.exe`, `<x.y.z>-full.nupkg`,
     optional `<x.y.z>-delta.nupkg`, `RELEASES`.
   - Publish the release.

   The release ships a single self-contained win-x64 build (no .NET
   prerequisite for end users). To add `win-arm64` or `win-x86`,
   create the matching `.pubxml` under
   `src/NepDateWidget/Properties/PublishProfiles/` and add a publish
   step + zip entry to `.github/workflows/release.yml`.
8. Verify the auto-update flow on a machine running the previous
   version: it should detect the new release within 24 hours (or
   immediately if you trigger the manual check).

## Yanking a broken release

If a release is discovered broken after publish:

1. Mark the GitHub Release as a pre-release immediately so casual
   downloads are deprioritized.
2. Delete the `RELEASES` file from the release assets so Velopack
   clients stop seeing the bad version (older `RELEASES` will be
   served from the previous release).
3. Publish a patched `vX.Y.Z+1` as soon as possible following the
   normal checklist.

Do not delete the tag itself. Tags are immutable in users' minds and
deleting one breaks reproducibility.

## Pre-release builds

For release candidates use a tag like `v1.1.0-rc1`. The release
workflow handles them identically to a stable tag, but you should mark
the GitHub Release as a pre-release and not advertise it to users on
the stable channel.

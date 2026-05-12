# Releasing NepDate Widget

This file documents the release process for maintainers. End users do
not need to read it.

## Versioning policy

NepDate Widget follows [Semantic Versioning 2.0.0](https://semver.org)
applied to the perspective of an end user, not a library consumer:

- **MAJOR** (`x.0.0`) - incompatible changes that require user action,
  for example: settings file format change with no migration, removed
  feature, change of `LOCALAPPDATA` data folder name.
- **MINOR** (`1.x.0`) - new feature, new setting, new tool, new theme,
  new language. Always backwards compatible for existing users.
- **PATCH** (`1.0.x`) - bug fix, performance fix, dependency bump
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
3. Commit any final changes with message `release: prepare vX.Y.Z`.
4. Tag the commit:
   ```
   git tag vX.Y.Z
   git push origin main --tags
   ```
5. The `Release` GitHub Actions workflow (`.github/workflows/release.yml`)
   triggers on the tag push. Watch it complete.
6. The workflow creates a **draft** GitHub Release. Edit it:
   - Write the release notes by hand. Group changes under
     **Added / Changed / Fixed / Security**. Link issues and PRs.
   - Confirm the attached files: `NepDateWidget-vX.Y.Z-win-x64-self-contained.zip`
     and `checksums.sha256.txt`.
   - Publish the release.

## Yanking a broken release

If a release is discovered broken after publish:

1. Mark the GitHub Release as a pre-release immediately so casual
   downloads are deprioritized.
2. Publish a patched `vX.Y.Z+1` as soon as possible following the
   normal checklist.

Do not delete the tag itself. Tags are immutable in users' minds and
deleting one breaks reproducibility.

## Pre-release builds

For release candidates use a tag like `v1.1.0-rc1`. The release
workflow handles them identically to a stable tag, but you should mark
the GitHub Release as a pre-release and not advertise it to users on
the stable channel.

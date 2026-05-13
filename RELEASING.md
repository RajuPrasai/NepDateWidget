# Releasing NepDate Widget

This file documents the release process for maintainers. End users do not need to read it.

## Versioning policy

NepDate Widget follows [Semantic Versioning 2.0.0](https://semver.org) applied to the user's perspective:

- **MAJOR** (`x.0.0`) - incompatible changes requiring user action: settings file migration, removed feature, `LOCALAPPDATA` folder rename.
- **MINOR** (`1.x.0`) - new feature, setting, tool, theme, or language. Always backwards compatible.
- **PATCH** (`1.0.x`) - bug fix, performance fix, dependency bump with no behavior change, documentation fix.

Pre-release tags (`-rc1`, `-beta.1`) are allowed but not promoted to the stable channel until re-tagged without the suffix.

## GitHub release

The GitHub release is the primary direct-download channel (self-contained zip). It is automated from a tagged commit.

**Checklist**

1. Update `<Version>` in `src/NepDateWidget/NepDateWidget.csproj` (no `v` prefix).
2. Run the full test suite and a Release build:
   ```
   dotnet test tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj -c Release
   dotnet build src/NepDateWidget/NepDateWidget.csproj -c Release
   ```
3. Commit with `release: prepare vX.Y.Z`.
4. Tag and push:
   ```
   git tag vX.Y.Z
   git push origin main --tags
   ```
5. The `Release` GitHub Actions workflow triggers on the tag. Watch it complete.
6. Edit the draft GitHub Release:
   - Write release notes grouped under **Added / Changed / Fixed / Security**. Link issues and PRs.
   - Confirm attached files: `NepDateWidget-vX.Y.Z-win-x64-self-contained.zip` and `checksums.sha256.txt`.
   - Publish.

## Microsoft Store release

The Store release targets worldwide users who install through the Store and receive automatic updates. Run this after completing the GitHub release checklist for the same version.

**Prerequisites**

Create `store-identity.ps1` from the committed template and fill in your Partner Center values:
```
Copy-Item store-identity.example.ps1 store-identity.ps1
```
That file is gitignored and never committed. Do it once; it persists across releases.

**Checklist**

1. Build both MSIX packages:
   ```
   ./build-store.ps1
   ```
   Or with an explicit version override:
   ```
   ./build-store.ps1 -Version X.Y.Z
   ```
   The script runs tests, publishes self-contained x64 and arm64 builds, generates Store logo assets, writes the manifest, and produces two files in `publish/`:
   - `NepDateWidget-X.Y.Z.0-x64.msix`
   - `NepDateWidget-X.Y.Z.0-arm64.msix`

2. Go to [Partner Center](https://partner.microsoft.com/dashboard):
   - Windows & Xbox → NepDate Widget → Start a submission.
   - On the Packages page drag and drop both `.msix` files. The Store automatically serves the correct architecture to each device.
   - Complete all required submission sections (release notes, pricing, availability, etc.) and submit.

**Version note**

The 4th segment of the Store version is reserved by the Store and must be `0` on submission. The build script enforces this automatically, so the Store version is always `X.Y.Z.0` regardless of what the csproj `<Version>` contains.

## Yanking a broken release

1. Mark the GitHub Release as a pre-release immediately so casual downloads are deprioritized.
2. For a Store submission still in certification, cancel it in Partner Center.
3. For an already published Store release, start a new submission with the fix as soon as possible.
4. Publish a patched `vX.Y.Z+1` following both checklists above.

Do not delete the tag. Tags are immutable in users' minds and deleting one breaks reproducibility.

## Pre-release builds

Use a tag like `v1.1.0-rc1`. The release workflow handles it identically to a stable tag. Mark the GitHub Release as a pre-release and do not advertise it on the stable channel. Do not submit pre-release builds to the Store.

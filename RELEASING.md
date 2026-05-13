# Releasing NepDate Widget

This file documents the release process for maintainers. End users do not need to read it.

## Versioning policy

NepDate Widget follows [Semantic Versioning 2.0.0](https://semver.org) applied to the user's perspective:

- **MAJOR** (`x.0.0`) - incompatible changes requiring user action: breaking migrations of local data files (`settings.json`, `reminders.json`), removed features, or `%LOCALAPPDATA%` folder renames.
- **MINOR** (`1.x.0`) - new feature, setting, tool, theme, or language. Always backwards compatible.
- **PATCH** (`1.0.x`) - bug fix, performance fix, dependency bump with no behavior change, documentation fix.

Pre-release tags (`-rc1`, `-beta.1`) are allowed but not promoted to the stable channel until re-tagged without the suffix.

## Microsoft Store release

The Store is the sole user-facing distribution channel. Users install and receive automatic updates through it.

**Prerequisites (one-time setup)**

- Windows SDK must be installed (required by `build-store.ps1` for `makeappx.exe`).
  Download: <https://developer.microsoft.com/windows/downloads/windows-sdk/>
- Create `store-identity.ps1` from the committed template and fill in your Partner Center values:
  ```
  Copy-Item store-identity.example.ps1 store-identity.ps1
  ```
  That file is gitignored and never committed. Do it once; it persists across releases.
  Values are at: Partner Center → Windows & Xbox → NepDate Widget → Product management → Product identity

**Checklist**

1. Update `<Version>` in `src/NepDateWidget/NepDateWidget.csproj` (no `v` prefix, three-part: `X.Y.Z`).
2. Run the full test suite and a Release build locally to catch failures before tagging:
   ```
   dotnet test tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj -c Release
   dotnet build src/NepDateWidget/NepDateWidget.csproj -c Release
   ```
3. Commit and tag:
   ```
   git add src/NepDateWidget/NepDateWidget.csproj
   git commit -m "release: prepare vX.Y.Z"
   git tag vX.Y.Z
   git push origin main --tags
   ```
4. The `Release` GitHub Actions workflow triggers on the tag. **Wait for it to pass.**
   It enforces that the tag version exactly matches `<Version>` in the csproj — if it fails here
   with a version mismatch, do a full recovery:
   ```
   # 1. Fix the csproj <Version>, then:
   git add src/NepDateWidget/NepDateWidget.csproj
   git commit --amend --no-edit          # fold the fix into the release commit
   git push origin main --force-with-lease
   git push origin :vX.Y.Z              # delete the remote tag
   git tag -d vX.Y.Z                    # delete the local tag
   git tag vX.Y.Z                       # re-tag the fixed commit
   git push origin vX.Y.Z
   ```
5. Build both MSIX packages locally:
   ```
   ./build-store.ps1
   ```
   Or with an explicit version override:
   ```
   ./build-store.ps1 -Version X.Y.Z
   ```
   The script runs tests again, publishes self-contained x64 and arm64 builds, generates Store
   logo assets, writes the manifest, and produces two files in `publish/`:
   - `NepDateWidget-X.Y.Z.0-x64.msix`
   - `NepDateWidget-X.Y.Z.0-arm64.msix`

   Use `-SkipTests` only if you already ran tests in step 2 and have not changed code since.
6. Go to [Partner Center](https://partner.microsoft.com/dashboard):
   - Windows & Xbox → NepDate Widget → Start a submission.
   - On the Packages page drag and drop both `.msix` files. The Store automatically serves the
     correct architecture to each device.
   - Complete all required submission sections (release notes, pricing, availability, etc.) and submit.

**Version note**

The 4th segment of the Store version is reserved by the Store and must be `0` on submission.
`build-store.ps1` enforces this automatically, so the Store version is always `X.Y.Z.0`
regardless of what the csproj `<Version>` contains.

## GitHub release (CI artifact)

Pushing a `vX.Y.Z` tag automatically triggers the `Release` workflow, which builds a
self-contained win-x64 zip and creates a **draft** GitHub Release. The draft is not published
as part of the Store-only release process — it is a reproducible build artifact and version
audit trail kept for reference.

If you choose to publish the draft (optional): edit it, write release notes grouped under
**Added / Changed / Fixed / Security**, confirm the attached zip and `checksums.sha256.txt`,
then publish.

## Yanking a broken release

1. For a Store submission still in certification, cancel it immediately in Partner Center.
2. For an already published Store release, start a new submission with the fix as soon as possible.
3. If the GitHub Release draft was published, mark it as a pre-release so it is deprioritized.
4. Publish a patched `vX.Y.Z+1` following the Store checklist above.

Do not delete the tag. Tags are immutable in users' minds and deleting one breaks reproducibility.

## Pre-release builds

Use a tag like `v1.1.0-rc1`. The `Release` workflow handles it identically to a stable tag and
creates a draft. Mark the draft as a pre-release if you publish it. Do not submit pre-release
builds to the Store.

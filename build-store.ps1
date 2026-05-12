# build-store.ps1
# Produces MSIX packages (x64 and arm64) ready for Microsoft Store submission.
#
# Usage:
#   ./build-store.ps1
#   ./build-store.ps1 -Version 1.0.0
#   ./build-store.ps1 -SkipTests
#
# IDENTITY VALUES
#   Create store-identity.ps1 from store-identity.example.ps1 and fill in your
#   Partner Center values. That file is gitignored and never committed.
#   Without it the script warns and builds with PLACEHOLDER values (not submittable).
#
#   Get values from: partner.microsoft.com/dashboard
#     → Windows & Xbox → your app → Product management → Product identity
#
# ARCHITECTURES
#   The script builds both x64 and arm64 packages from a single Windows x64 host.
#   Cross-compilation of ReadyToRun code for arm64 is supported by the .NET SDK
#   for .NET 6 and later without any extra tooling.
#   Upload both .msix files to the same Partner Center submission. The Store
#   automatically serves the correct architecture to each device.
#
# OUTPUT
#   publish/win-x64-store/                - unpacked x64 content (for inspection)
#   publish/win-arm64-store/              - unpacked arm64 content (for inspection)
#   publish/NepDateWidget-<Version>-x64.msix   - upload to Partner Center
#   publish/NepDateWidget-<Version>-arm64.msix - upload to Partner Center

param(
    # Version in 4-part format (Major.Minor.Build.Revision).
    # Defaults to the <Version> in NepDateWidget.csproj with ".0" appended.
    # Must be >= previously submitted version in Partner Center.
    [string]$Version = "",

    # Skip running the test suite (useful when iterating on packaging).
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Push-Location $root

try {
    $project = 'src/NepDateWidget/NepDateWidget.csproj'

    # ── Load identity from secrets file ──────────────────────────────────────
    $secretsFile = Join-Path $root 'store-identity.ps1'
    if (Test-Path $secretsFile) {
        . $secretsFile
        Write-Host 'Identity : loaded from store-identity.ps1' -ForegroundColor DarkGray
    } else {
        $PackageName          = 'PLACEHOLDER_PACKAGE_NAME'
        $Publisher            = 'PLACEHOLDER_PUBLISHER_CN'
        $PublisherDisplayName = 'PLACEHOLDER_PUBLISHER_DISPLAY_NAME'
        Write-Warning @"
store-identity.ps1 not found. Building with PLACEHOLDER identity values.
The MSIX packages cannot be submitted to Partner Center or locally installed.

Create store-identity.ps1 from the committed template:
  Copy-Item store-identity.example.ps1 store-identity.ps1
Then fill in your Partner Center values and re-run.
"@
    }

    $hasPlaceholders = $PackageName          -like 'PLACEHOLDER*' -or
                       $Publisher            -like 'PLACEHOLDER*' -or
                       $PublisherDisplayName -like 'PLACEHOLDER*'

    # ── Resolve and normalise version ─────────────────────────────────────────
    if (-not $Version) {
        $xml = [xml](Get-Content $project)
        $v = @($xml.Project.PropertyGroup) |
                 ForEach-Object { $_.Version } |
                 Where-Object   { $_ } |
                 Select-Object  -First 1
        if (-not $v) { throw "Could not read <Version> from $project." }
        $Version = $v
    }
    # Normalise to exactly four parts: "1.0.0" → "1.0.0.0".
    $parts = $Version.TrimEnd('.').Split('.')
    while ($parts.Count -lt 4) { $parts += '0' }

    # Store requirement: the 4th (Revision) segment is reserved for Store use
    # and must be 0 when you submit.
    if ($parts[3] -ne '0') {
        Write-Warning "4th version segment was $($parts[3]); forced to 0 per Store requirements."
        $parts[3] = '0'
    }
    if ([int]$parts[0] -eq 0) { throw 'Major version cannot be 0. Update <Version> in the .csproj.' }

    $Version4 = ($parts[0..3]) -join '.'

    Write-Host "Version  : $Version4" -ForegroundColor DarkGray
    Write-Host "Package  : $PackageName" -ForegroundColor DarkGray

    # ── [Tests] ───────────────────────────────────────────────────────────────
    if (-not $SkipTests) {
        Write-Host ''
        Write-Host '[Tests]' -ForegroundColor Cyan
        dotnet test 'tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj' -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    } else {
        Write-Host '[Tests] skipped' -ForegroundColor DarkGray
    }

    # ── Locate makeappx.exe once (shared across arch builds) ──────────────────
    $sdkRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
    if (-not (Test-Path $sdkRoot)) {
        throw "Windows SDK not found at $sdkRoot. " +
              "Install Windows 10/11 SDK from https://developer.microsoft.com/windows/downloads/windows-sdk/"
    }
    $sdkBin = Get-ChildItem $sdkRoot -Directory |
                  Where-Object { $_.Name -match '^\d' } |
                  Sort-Object Name -Descending |
                  Select-Object -First 1
    if (-not $sdkBin) { throw "No versioned SDK directory found under $sdkRoot." }

    $makeAppx = Join-Path $sdkBin.FullName 'x64\makeappx.exe'
    if (-not (Test-Path $makeAppx)) {
        throw "makeappx.exe not found at: $makeAppx`nEnsure the Windows SDK is fully installed."
    }

    # ── Load System.Drawing once for asset generation ─────────────────────────
    Add-Type -AssemblyName System.Drawing

    $iconSrc = "$root\src\NepDateWidget\Assets\icon.png"
    $src     = [System.Drawing.Bitmap]::new([string]$iconSrc)
    if ($src.Width -lt 310 -or $src.Height -lt 310) {
        Write-Warning "icon.png is $($src.Width)x$($src.Height). The 310x310 tile will be upscaled and may appear blurry. Recommended minimum source size is 400x400."
    }

    function Save-Tile([int]$w, [int]$h, [string]$name) {
        $bmp = [System.Drawing.Bitmap]::new($w, $h)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.DrawImage($src, 0, 0, $w, $h)
        $g.Dispose()
        $bmp.Save("$storeDir\$name", [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }

    function Save-WideTile([string]$name) {
        # 310×150: icon centred horizontally on a transparent canvas.
        # The icon is square, so we fit it to 150×150 and centre in the 310 width.
        $bmp = [System.Drawing.Bitmap]::new(310, 150)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $x = [int]((310 - 150) / 2)   # 80 px left margin
        $g.DrawImage($src, $x, 0, 150, 150)
        $g.Dispose()
        $bmp.Save("$storeDir\$name", [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }

    $outDir   = "$root\publish"
    $outFiles = @()

    # ── Per-architecture build ────────────────────────────────────────────────
    foreach ($arch in @('x64', 'arm64')) {

        Write-Host ''
        Write-Host "=== $arch ===" -ForegroundColor Magenta

        $publishDir = "$root\publish\win-$arch-store"

        # ── [1/4] Publish ─────────────────────────────────────────────────────
        Write-Host ''
        Write-Host "[$arch 1/4] Publish (win-$arch, self-contained, non-single-file)" -ForegroundColor Cyan
        # Clean the publish dir so stale files from previous runs do not sneak
        # into the MSIX (MakeAppx packs everything it finds in the directory).
        if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
        dotnet publish $project /p:PublishProfile=win-$arch-store
        if ($LASTEXITCODE -ne 0) { throw "Publish failed for $arch." }

        # ── [2/4] Store logo assets ───────────────────────────────────────────
        # All sizes must be exact pixels - the Store certification pipeline
        # rejects wrong-sized images. Assets are architecture-neutral (same PNGs
        # for x64 and arm64); they are generated into each publish dir separately.
        Write-Host ''
        Write-Host "[$arch 2/4] Generating store logo assets from Assets/icon.png" -ForegroundColor Cyan

        $storeDir = "$publishDir\Assets\Store"
        New-Item $storeDir -ItemType Directory -Force | Out-Null

        # Required by manifest + Store certification:
        Save-Tile  44  44  'Square44x44Logo.png'     # taskbar, app list
        Save-Tile  50  50  'StoreLogo.png'            # Settings → Apps, Properties logo
        Save-Tile 150 150  'Square150x150Logo.png'    # medium Start tile
        # Optional but declared in manifest - include for completeness:
        Save-Tile  71  71  'Square71x71Logo.png'      # small Start tile
        Save-Tile 310 310  'Square310x310Logo.png'    # large Start tile
        Save-WideTile      'Wide310x150Logo.png'      # wide Start tile

        # ── [3/4] Write AppxManifest.xml ──────────────────────────────────────
        # The source Package.appxmanifest is a template. We substitute the four
        # PLACEHOLDER_ tokens, VERSION_PLACEHOLDER, and ARCH_PLACEHOLDER, then
        # write AppxManifest.xml to the root of the publish directory where
        # MakeAppx expects it. String.Replace() is used (not regex) to avoid
        # issues with CN=... strings that may contain backslash or dollar-sign.
        Write-Host ''
        Write-Host "[$arch 3/4] Writing AppxManifest.xml" -ForegroundColor Cyan

        $template = Get-Content "$root\src\NepDateWidget\Package.appxmanifest" -Raw
        $manifest  = $template.
            Replace('PLACEHOLDER_PACKAGE_NAME',          $PackageName).
            Replace('PLACEHOLDER_PUBLISHER_CN',           $Publisher).
            Replace('PLACEHOLDER_PUBLISHER_DISPLAY_NAME', $PublisherDisplayName).
            Replace('VERSION_PLACEHOLDER',                $Version4).
            Replace('ARCH_PLACEHOLDER',                   $arch)
        [System.IO.File]::WriteAllText("$publishDir\AppxManifest.xml", $manifest,
            [System.Text.Encoding]::UTF8)

        # ── [4/4] MakeAppx ────────────────────────────────────────────────────
        # The tool validates manifest semantics (all referenced files present, no
        # duplicate keys, no forbidden protocols) and builds the content block map.
        Write-Host ''
        Write-Host "[$arch 4/4] MakeAppx pack" -ForegroundColor Cyan

        $outMsix = "$outDir\NepDateWidget-$Version4-$arch.msix"
        Remove-Item $outMsix -Force -ErrorAction SilentlyContinue

        # /d        - content directory (everything in publishDir goes into the package)
        # /p        - output .msix path
        # /o        - overwrite output if it exists
        # /h SHA256 - explicit SHA2-256 block map hash (Store requirement)
        # (no /nv)  - let MakeAppx run its full semantic validation
        & $makeAppx pack /d $publishDir /p $outMsix /o /h SHA256
        if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed for $arch. See output above." }

        $outFiles += $outMsix
        Write-Host "MSIX: $outMsix" -ForegroundColor Green
    }

    $src.Dispose()

    # ── Summary ───────────────────────────────────────────────────────────────
    Write-Host ''
    Write-Host 'Build complete.' -ForegroundColor Green
    $outFiles | ForEach-Object { Write-Host "  $_" -ForegroundColor Green }

    if ($hasPlaceholders) {
        Write-Host ''
        Write-Host 'REMINDER: identity values are PLACEHOLDER - packages are not submittable.' -ForegroundColor Yellow
    } else {
        Write-Host ''
        Write-Host 'Upload both files to the same Partner Center submission:' -ForegroundColor Cyan
        Write-Host '  https://partner.microsoft.com/dashboard' -ForegroundColor Cyan
        Write-Host '  → Your app → Start a submission → Packages → drag both .msix files' -ForegroundColor Cyan
        Write-Host '  The Store will serve the correct architecture to each device automatically.' -ForegroundColor DarkGray
    }

} finally {
    Pop-Location
}

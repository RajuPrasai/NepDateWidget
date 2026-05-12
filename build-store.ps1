# build-store.ps1
# Produces an MSIX package ready for Microsoft Store submission via Partner Center.
#
# Usage:
#   ./build-store.ps1 -Version 1.0.0.0 `
#                     -PackageName "YourReservedName" `
#                     -Publisher "CN=Contoso, O=Contoso, C=US" `
#                     -PublisherDisplayName "Contoso"
#
# Without the identity parameters the script will build with PLACEHOLDER values
# and warn you. The resulting MSIX cannot be submitted or locally installed, but
# it lets you validate the build pipeline and package structure.
#
# HOW TO GET IDENTITY PARAMETERS
#   partner.microsoft.com/dashboard
#     → Windows & Xbox → your app → Product management → Product identity
#     Copy: Package/Identity/Name, Package/Identity/Publisher (the CN=... string),
#           Package/Properties/PublisherDisplayName.
#
# SIGNING NOTE
#   The Store re-signs the package during certification. For Store submission you
#   do not need a code-signing certificate. For local testing via Add-AppxPackage
#   you do - create a self-signed cert whose Subject matches Publisher and install
#   it to the Trusted People store, then sign with signtool.exe.
#
# STORE UPLOAD
#   Partner Center accepts .msix directly. Upload the file at:
#     Packages page of your submission → drag and drop the .msix
#   The portal validates the package and displays device family availability.
#
# OUTPUT
#   publish/win-x64-store/     - unpacked content (for inspection)
#   publish/NepDateWidget-<Version>.msix  - upload this to Partner Center

param(
    # Version in 4-part format (Major.Minor.Build.Revision).
    # Defaults to the <Version> in NepDateWidget.csproj with ".0" appended.
    # Must be >= previously submitted version in Partner Center.
    [string]$Version = "",

    # Skip running the test suite (useful when iterating on packaging).
    [switch]$SkipTests,

    # Package/Identity/Name from Partner Center Product identity page.
    [string]$PackageName = "PLACEHOLDER_PACKAGE_NAME",

    # Package/Identity/Publisher from Partner Center (the full CN=... string).
    [string]$Publisher = "PLACEHOLDER_PUBLISHER_CN",

    # Package/Properties/PublisherDisplayName from Partner Center.
    [string]$PublisherDisplayName = "PLACEHOLDER_PUBLISHER_DISPLAY_NAME"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Push-Location $root

try {
    $project = 'src/NepDateWidget/NepDateWidget.csproj'

    # ── Resolve and normalise version ────────────────────────────────────────
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
    # and must be 0 when you submit.  See "Package version numbering" in the
    # Microsoft Store documentation.  Enforce this unconditionally.
    if ($parts[3] -ne '0') {
        Write-Warning "4th version segment was $($parts[3]); forced to 0 per Store requirements."
        $parts[3] = '0'
    }
    # The first (Major) segment cannot be 0.
    if ([int]$parts[0] -eq 0) { throw 'Major version cannot be 0. Update <Version> in the .csproj.' }

    $Version4 = ($parts[0..3]) -join '.'

    # ── Identity placeholder warning ─────────────────────────────────────────
    $hasPlaceholders = $PackageName -like 'PLACEHOLDER*' -or
                       $Publisher   -like 'PLACEHOLDER*' -or
                       $PublisherDisplayName -like 'PLACEHOLDER*'
    if ($hasPlaceholders) {
        Write-Warning @"
One or more identity values are still PLACEHOLDER. The MSIX will be created
but cannot be submitted to Partner Center or locally installed as-is.

Get real values from partner.microsoft.com/dashboard
  → Windows & Xbox → your app → Product management → Product identity

Then re-run:
  ./build-store.ps1 -PackageName "..." -Publisher "CN=..." -PublisherDisplayName "..."
"@
    }

    Write-Host "Version : $Version4" -ForegroundColor DarkGray
    Write-Host "Package : $PackageName" -ForegroundColor DarkGray

    # ── [1/5] Tests ───────────────────────────────────────────────────────────
    if (-not $SkipTests) {
        Write-Host ''
        Write-Host '[1/5] Tests' -ForegroundColor Cyan
        dotnet test 'tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj' -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    } else {
        Write-Host '[1/5] Tests (skipped)' -ForegroundColor DarkGray
    }

    # ── [2/5] Publish ─────────────────────────────────────────────────────────
    Write-Host ''
    Write-Host '[2/5] Publish (win-x64, self-contained, non-single-file)' -ForegroundColor Cyan
    $publishDir = "$root\publish\win-x64-store"
    # Clean the publish dir so stale files from previous runs do not sneak into
    # the MSIX (MakeAppx packs everything it finds in the directory).
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    dotnet publish $project /p:PublishProfile=win-x64-store
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

    # ── [3/5] Store logo assets ───────────────────────────────────────────────
    # Resize the project icon into every tile size required by the manifest and
    # by the Store. System.Drawing is available on Windows in .NET 7+ (the
    # Windows-only System.Drawing.Common support). All sizes must be exact pixels
    # - the Store certification pipeline rejects wrong-sized images.
    Write-Host ''
    Write-Host '[3/5] Generating store logo assets from Assets/icon.png' -ForegroundColor Cyan

    $iconSrc   = "$root\src\NepDateWidget\Assets\icon.png"
    $storeDir  = "$publishDir\Assets\Store"
    New-Item $storeDir -ItemType Directory -Force | Out-Null

    Add-Type -AssemblyName System.Drawing
    $src = [System.Drawing.Bitmap]::new([string]$iconSrc)

    function Save-Tile([int]$w, [int]$h, [string]$name) {
        $bmp = [System.Drawing.Bitmap]::new($w, $h)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
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
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $x = [int]((310 - 150) / 2)   # 80 px left margin
        $g.DrawImage($src, $x, 0, 150, 150)
        $g.Dispose()
        $bmp.Save("$storeDir\$name", [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }

    # Required by manifest + Store certification:
    Save-Tile  44  44  'Square44x44Logo.png'     # taskbar, app list
    Save-Tile  50  50  'StoreLogo.png'            # Settings → Apps, Properties logo
    Save-Tile 150 150  'Square150x150Logo.png'    # medium Start tile
    # Optional but declared in manifest - include for completeness:
    Save-Tile  71  71  'Square71x71Logo.png'      # small Start tile
    Save-Tile 310 310  'Square310x310Logo.png'    # large Start tile
    Save-WideTile      'Wide310x150Logo.png'      # wide Start tile

    $src.Dispose()

    # ── [4/5] Write AppxManifest.xml ─────────────────────────────────────────
    # The source Package.appxmanifest is a template. We substitute the four
    # PLACEHOLDER_ tokens and the VERSION_PLACEHOLDER, then write AppxManifest.xml
    # to the root of the publish directory where MakeAppx expects it.
    # String.Replace() is used (not regex) to avoid issues with CN=... strings
    # that may contain backslash or dollar-sign characters.
    Write-Host ''
    Write-Host '[4/5] Writing AppxManifest.xml' -ForegroundColor Cyan

    $template = Get-Content "$root\src\NepDateWidget\Package.appxmanifest" -Raw
    $manifest  = $template.
        Replace('PLACEHOLDER_PACKAGE_NAME',          $PackageName).
        Replace('PLACEHOLDER_PUBLISHER_CN',           $Publisher).
        Replace('PLACEHOLDER_PUBLISHER_DISPLAY_NAME', $PublisherDisplayName).
        Replace('VERSION_PLACEHOLDER',                $Version4)
    [System.IO.File]::WriteAllText("$publishDir\AppxManifest.xml", $manifest,
        [System.Text.Encoding]::UTF8)

    # ── [5/5] MakeAppx ───────────────────────────────────────────────────────
    # Find the highest-version Windows SDK installed (to get makeappx.exe).
    # The tool validates manifest semantics (all referenced files present, no
    # duplicate keys, no forbidden protocols) and builds the content block map.
    Write-Host ''
    Write-Host '[5/5] MakeAppx pack' -ForegroundColor Cyan

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

    $outDir  = "$root\publish"
    $outMsix = "$outDir\NepDateWidget-$Version4.msix"
    Remove-Item $outMsix -Force -ErrorAction SilentlyContinue

    # /d        - content directory (everything in publishDir goes into the package)
    # /p        - output .msix path
    # /o        - overwrite output if it exists
    # /h SHA256 - explicit SHA2-256 block map hash (Store requirement; this is
    #             also the MakeAppx default, but specifying it makes compliance visible)
    # (no /nv)  - let MakeAppx run its full semantic validation
    & $makeAppx pack /d $publishDir /p $outMsix /o /h SHA256
    if ($LASTEXITCODE -ne 0) { throw 'MakeAppx failed. See output above.' }

    Write-Host ''
    Write-Host "MSIX created: $outMsix" -ForegroundColor Green

    if ($hasPlaceholders) {
        Write-Host 'REMINDER: identity values are PLACEHOLDER - not submittable.' -ForegroundColor Yellow
    } else {
        # ── WACK (Windows App Certification Kit) ─────────────────────────────
        # Microsoft requires you to validate with WACK before submitting.
        # WACK ships with the Windows SDK and runs Desktop Bridge-specific tests
        # (launch, manifest resources, supported APIs, security features, etc.).
        # It must run in an active user session; it cannot be automated silently.
        #
        # WACK is at: C:\Program Files (x86)\Windows Kits\10\App Certification Kit\
        #
        # Steps:
        #   1. Enable Developer Mode (Settings → Privacy & Security → Developer Mode)
        #   2. Install the MSIX for testing:
        #        Add-AppxPackage -Path "$outMsix"
        #      (requires a self-signed cert matching Publisher, or Developer Mode)
        #      OR register without signing:
        #        Add-AppxPackage -Register "$publishDir\AppxManifest.xml"
        #   3. Run WACK from the App Certification Kit directory:
        #        appcert.exe reset
        #        appcert.exe test -packagefullname "<PackageFullName>" `
        #                         -reportoutputpath "$root\publish\wack-report.xml"
        #      Replace <PackageFullName> with the full name shown in:
        #        Get-AppxPackage | Where-Object Name -eq '$PackageName' | Select PackageFullName
        #   4. Review the HTML/XML report and fix any failures.
        #   5. Uninstall the test package:
        #        Remove-AppxPackage <PackageFullName>
        Write-Host ''
        Write-Host 'Next step - run WACK before submitting:' -ForegroundColor Yellow
        $wackDir = 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit'
        Write-Host "  $wackDir\appcert.exe reset" -ForegroundColor DarkGray
        Write-Host "  $wackDir\appcert.exe test -packagefullname <FullName> -reportoutputpath $root\publish\wack-report.xml" -ForegroundColor DarkGray
        Write-Host ''
        Write-Host 'Then upload to Partner Center:' -ForegroundColor Cyan
        Write-Host '  https://partner.microsoft.com/dashboard' -ForegroundColor Cyan
        Write-Host '  → Your app → Start a submission → Packages → drag the .msix' -ForegroundColor Cyan
    }

} finally {
    Pop-Location
}

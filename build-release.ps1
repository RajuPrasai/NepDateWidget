# Local release build script — produces the same artefacts as the GitHub Actions
# Release workflow so you can validate locally before pushing a tag.
#
#   ./build-release.ps1 -Version 1.0.0
#
# Output (everything goes into ./releases/):
#   NepDateWidget-win-Setup.exe          ← installer (run on a fresh machine)
#   NepDateWidget-<Version>-full.nupkg   ← full update payload
#   NepDateWidget-<Version>-delta.nupkg  ← delta vs previous version (if any)
#   RELEASES                             ← manifest read by the in-app updater
#
# To publish a GitHub Release, upload these three files:
#   Setup.exe + <Version>-full.nupkg + RELEASES
# (delta.nupkg is optional but recommended when present.)
#
# IMPORTANT: do NOT delete ./releases/ between version builds. Velopack reads
# the existing RELEASES file and the previous *-full.nupkg to generate the
# delta package for the new version.
#
# Requirements: .NET 10 SDK. Velopack CLI is installed automatically if missing.

param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Push-Location $root

try {
    $project = "src/NepDateWidget/NepDateWidget.csproj"

    # csproj <Version> must match (mirrors release.yml step)
    $xml = [xml](Get-Content $project)
    $csprojVersion = @($xml.Project.PropertyGroup) |
        ForEach-Object { $_.Version } |
        Where-Object   { $_ } |
        Select-Object  -First 1
    if ($csprojVersion -ne $Version) {
        Write-Error "csproj <Version> ($csprojVersion) does not match -Version $Version. Update the .csproj first."
        exit 1
    }

    if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
        Write-Host "Installing Velopack CLI..." -ForegroundColor Cyan
        dotnet tool install -g vpk --version 0.0.1298
    }

    Write-Host "[1/3] Tests" -ForegroundColor Cyan
    dotnet test tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj -c Release

    Write-Host "[2/3] Publish (win-x64, self-contained, single-file)" -ForegroundColor Cyan
    dotnet publish $project /p:PublishProfile=win-x64-portable

    # Note: PDBs are embedded into the assembly via <DebugType>embedded</DebugType>
    # in the .csproj, so there are no loose .pdb files to strip after publish.

    Write-Host "[3/3] Velopack pack" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force releases | Out-Null
    vpk pack `
        --packId NepDateWidget `
        --packVersion $Version `
        --packDir publish/win-x64-portable `
        --mainExe NepDateWidget.exe `
        --packTitle "NepDate Widget" `
        --packAuthors "RajuPrasai" `
        --icon src/NepDateWidget/Assets/icon.ico `
        --outputDir releases

    Write-Host ""
    Write-Host "Done. Upload these 3 files to the GitHub Release:" -ForegroundColor Green
    Get-ChildItem releases -File |
        Where-Object { $_.Name -in @(
            "NepDateWidget-win-Setup.exe",
            "NepDateWidget-$Version-full.nupkg",
            "RELEASES"
        )} |
        Format-Table Name, @{N='MB';E={[math]::Round($_.Length/1MB,2)}}

    $delta = Get-ChildItem releases -File -Filter "NepDateWidget-$Version-delta.nupkg" -ErrorAction SilentlyContinue
    if ($delta) {
        Write-Host "Optional (smaller incremental update for users on the previous version):" -ForegroundColor Cyan
        $delta | Format-Table Name, @{N='MB';E={[math]::Round($_.Length/1MB,2)}}
    }
}
finally {
    Pop-Location
}

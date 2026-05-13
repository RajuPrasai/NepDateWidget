#Requires -Version 5.1

$OutputFolderName = "Organized"

# ───────────────────── UI HELPERS ────────────────────────────────────────────

function Write-Header {
    Clear-Host
    Write-Host ""
    Write-Host "  +============================================+" -ForegroundColor Cyan
    Write-Host "  |     DESKTOP ORGANIZER                      |" -ForegroundColor Cyan
    Write-Host "  |     Sorts Desktop files by type            |" -ForegroundColor DarkCyan
    Write-Host "  +============================================+" -ForegroundColor Cyan
    Write-Host ""
}

function Write-OK($msg)   { Write-Host "  [+] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "  [!] $msg" -ForegroundColor Magenta }

# ───────────────────── CATEGORY MAP ──────────────────────────────────────────

function Get-Category($extension) {
    $e = $extension.TrimStart('.').ToLower()
    switch -Regex ($e) {
        '^(jpg|jpeg|png|gif|bmp|tiff|tif|webp|ico|heic|heif|avif|raw|cr2|cr3|nef|arw|orf|rw2|dng|psd|psb|xcf)$' { return "Images" }
        '^(ai|eps|svg|cdr|indd|vsd|vsdx)$'                                                                        { return "Images_Vector" }
        '^(mp4|mkv|avi|mov|wmv|flv|webm|m4v|mpg|mpeg|3gp|3g2|ts|mts|m2ts|divx|vob|rm|rmvb|f4v|ogv|asf|dv)$'    { return "Videos" }
        '^(mp3|wav|flac|aac|ogg|oga|wma|m4a|m4b|opus|aiff|aif|mid|midi|amr|au|mka|wv|ape|ra)$'                   { return "Audio" }
        '^(pdf|doc|docx|docm|odt|rtf|tex|wpd|pages|wps|abw|sdw)$'                                                { return "Documents" }
        '^(xls|xlsx|xlsm|xlsb|csv|ods|numbers|tsv|dif|slk)$'                                                     { return "Spreadsheets" }
        '^(ppt|pptx|pptm|odp|key|sxi)$'                                                                          { return "Presentations" }
        '^(zip|7z|rar|tar|gz|bz2|xz|iso|cab|tgz|lzma|lz4|zst|ace|arj|lha|lzh|cpio)$'                            { return "Archives" }
        '^(epub|mobi|azw|azw3|fb2|djvu|djv|cbz|cbr|cb7|lit|lrf|pdb)$'                                            { return "eBooks" }
        '^(ttf|otf|woff|woff2|eot|fon|fnt|pfb|pfm|afm)$'                                                         { return "Fonts" }
        '^(exe|msi|msix|appx|deb|rpm|dmg|pkg|apk|xapk|ipa|jar|war|ear|appimage|run|com)$'                        { return "Executables" }
        '^(lnk|url|webloc|desktop|appref-ms|website)$'                                                           { return "Shortcuts" }
        '^(dll|so|dylib|sys|ocx|drv|lib|ko|vxd|cpl|scr)$'                                                        { return "System_Libraries" }
        '^(cs|js|ts|jsx|tsx|java|py|rb|php|go|rs|cpp|c|cc|cxx|h|hpp|swift|kt|scala|dart|lua|r)$'                 { return "Code" }
        '^(m|mm|f|f90|asm|s|pl|pm|vb|groovy|clj|ex|exs|elm|erl|hs|ml|nim|d|cr|jl|zig|raku|coffee|vue|svelte|astro)$' { return "Code" }
        '^(html|htm|css|sass|scss|less|xml|json|json5|yaml|yml|toml|graphql|gql|wasm|webmanifest)$'               { return "Web" }
        '^(sql|db|sqlite|sqlite3|mdb|accdb|accde|dbf|bak|dump|ldf|mdf|sdf)$'                                     { return "Database" }
        '^(ini|cfg|conf|config|env|properties|reg|plist|inf|editorconfig|gitignore|gitattributes|npmrc|dockerignore|ovpn)$' { return "Configs" }
        '^(ps1|psm1|psd1|bat|cmd|sh|bash|zsh|fish|vbs|wsf|wsh|ahk|au3|nsi|iss)$'                                { return "Scripts" }
        '^(txt|log|md|markdown|rst|nfo|asc|readme|changelog|license|todo|diff|patch)$'                           { return "Text" }
        '^(obj|fbx|stl|blend|3ds|dae|gltf|glb|step|stp|iges|igs|f3d|skp|dwg|dxf|sldprt|ipt)$'                   { return "3D_CAD" }
        '^(vmdk|vhd|vhdx|vdi|ova|ovf|img|qcow2|nrg|cue|mdf|mds|ccd|toast)$'                                     { return "Disk_Images" }
        '^(pem|crt|cer|der|key|pfx|p12|p7b|csr|pub|gpg|sig)$'                                                    { return "Certificates" }
        '^(parquet|avro|orc|h5|mat|npy|npz|pkl|feather|arrow)$'                                                  { return "Data" }
        '^(torrent|part|crdownload|download|tmp)$'                                                                { return "Temp_Downloads" }
        default { return "Others" }
    }
}

# ───────────────────── DUPLICATE HANDLER ─────────────────────────────────────

function Get-SafeDestPath($destFolder, $fileName) {
    $fullPath = Join-Path $destFolder $fileName
    if (-not (Test-Path $fullPath)) { return $fullPath }
    $base = [System.IO.Path]::GetFileNameWithoutExtension($fileName)
    $ext  = [System.IO.Path]::GetExtension($fileName)
    $i = 1
    do {
        $newName  = "$base ($i)$ext"
        $fullPath = Join-Path $destFolder $newName
        $i++
    } while (Test-Path $fullPath)
    return $fullPath
}

# ───────────────────── MAIN ───────────────────────────────────────────────────

try {
    Write-Header

    $sourceFolder = [Environment]::GetFolderPath("Desktop")
    $targetFolder = Join-Path $sourceFolder $OutputFolderName

    Write-Host "  Desktop : $sourceFolder" -ForegroundColor DarkGray
    Write-Host "  Output  : $targetFolder" -ForegroundColor DarkGray
    Write-Host ""

    # -File without -Recurse returns only direct-child files; subdirectories are never entered.
    # The DirectoryName check makes that contract explicit regardless of future edits.
    $files = Get-ChildItem -Path $sourceFolder -File |
             Where-Object { $_.DirectoryName -eq $sourceFolder }

    $total = $files.Count
    Write-Host "  Found $total file(s) to organize" -ForegroundColor Yellow
    Write-Host ""

    if ($total -eq 0) {
        Write-Host "  Desktop is already clean." -ForegroundColor Green
        Write-Host ""
        exit 0
    }

    $moved   = 0
    $skipped = 0
    $summary = [ordered]@{}

    foreach ($file in $files) {
        $category = Get-Category $file.Extension
        $destDir  = Join-Path $targetFolder $category

        if (-not (Test-Path $destDir)) {
            New-Item -Path $destDir -ItemType Directory -Force | Out-Null
        }

        $safeDest  = Get-SafeDestPath $destDir $file.Name
        $finalName = Split-Path $safeDest -Leaf
        $wasDuped  = $finalName -ne $file.Name

        try {
            Move-Item -Path $file.FullName -Destination $safeDest -Force -ErrorAction Stop
            $moved++
            if ($wasDuped) {
                Write-OK "$($file.Name)  ->  $category\$finalName  [numbered]"
            } else {
                Write-OK "$($file.Name)  ->  $category"
            }
            if ($null -eq $summary[$category]) { $summary[$category] = 0 }
            $summary[$category]++
        } catch {
            Write-Warn "Could not move '$($file.Name)': $($_.Exception.Message)"
            $skipped++
        }
    }

    Write-Host ""
    Write-Host "  --------------------------------------------" -ForegroundColor DarkGray
    Write-Host "  Moved   : $moved" -ForegroundColor Green
    Write-Host "  Skipped : $skipped" -ForegroundColor Magenta
    Write-Host ""

    if ($summary.Count -gt 0) {
        Write-Host "  Category Breakdown:" -ForegroundColor White
        Write-Host ""
        $maxLen = ($summary.Keys | Measure-Object -Property Length -Maximum).Maximum
        foreach ($cat in $summary.Keys) {
            $count  = $summary[$cat]
            $bar    = "=" * [Math]::Min($count, 30)
            $padded = $cat.PadRight($maxLen + 2)
            Write-Host "    $padded" -NoNewline -ForegroundColor DarkCyan
            Write-Host "[$bar] $count" -ForegroundColor Cyan
        }
        Write-Host ""
    }

    Write-Host "  Output: $targetFolder" -ForegroundColor DarkGray
    Write-Host ""

} catch {
    Write-Host ""
    Write-Host "  ERROR: $_" -ForegroundColor Red
    Write-Host ""
}

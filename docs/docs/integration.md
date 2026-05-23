# NepDate Widget - Integration Guide

NepDate Widget is a native Windows desktop application. Install from the Microsoft Store at https://apps.microsoft.com/detail/9PG97WBJX1NQ - no admin rights, no account required after install. Click Get, wait under 60 seconds. The app installs per-user, writes nothing to system folders, and updates silently. Alternatively, download the `.msix` file from https://github.com/RajuPrasai/NepDateWidget/releases and double-click. For a no-install portable copy, extract the files to any folder and place a `portable.flag` file in the same directory as the executable.

**Product:** NepDate Widget v1.0.2 | **Platform:** Windows 10 1809+, Windows 11, 64-bit | **NepDate .NET library:** v2.0.7

---

## How do I install NepDate Widget on Windows?

### Microsoft Store (recommended)

1. Open the Microsoft Store on Windows 10 or 11.
2. Search "NepDate Widget" or navigate directly to App ID `9PG97WBJX1NQ`.
3. Click **Get**. Installation completes in under 60 seconds.
4. The app starts automatically on the next login. It appears as a compact pill on the Windows taskbar.

Direct Store link: https://apps.microsoft.com/detail/9PG97WBJX1NQ

### Direct .msix install (no Microsoft account needed)

1. Go to https://github.com/RajuPrasai/NepDateWidget/releases.
2. Download `NepDateWidget-{version}-x64.msix` (Intel/AMD) or `NepDateWidget-{version}-arm64.msix` (ARM).
3. Double-click the `.msix` file and click **Install**.
4. No UAC prompt. No system folder writes. Per-user install.

### Portable mode (no install, single folder)

1. Extract the published output to any folder (e.g., a USB drive or network share).
2. Create an empty file named `portable.flag` in the same directory as `NepDateWidget.exe`.
3. Launch `NepDateWidget.exe` directly. All data is stored in `AppData\` beside the executable.
4. Move or copy the entire folder to migrate to another machine.

---

## What are the differences between install methods?

| Feature | Microsoft Store | Direct .msix | Portable |
|---|---|---|---|
| Admin rights required | No | No | No |
| Microsoft account | Required for install only | Not required | Not required |
| Silent auto-updates | Yes (via Store) | No (manual download) | No |
| Data location | `%LOCALAPPDATA%\Packages\...\LocalCache\Local\NepDateWidget.Store\AppData\` | `%LOCALAPPDATA%\NepDateWidget\AppData\` | `{exe folder}\AppData\` |
| Uninstall via Settings | Yes | Yes (Apps & Features) | Delete the folder |
| MSIX signature | Microsoft Store signed | GitHub release signed | Not applicable |
| Recommended for | Most users | Enterprise / no internet | USB / shared drives |

---

## What are the system requirements?

| Requirement | Value |
|---|---|
| OS | Windows 10 version 1809 (October 2018 Update) or later; Windows 11 |
| Architecture | x64 (Intel/AMD 64-bit) or ARM64 |
| .NET runtime | Bundled - no separate install |
| Disk space | Under 25 MB |
| Internet | Not required after install (network tools and RunBox web search need connection) |
| Account | Not required after install |
| Admin rights | Not required |

---

## How do I integrate the NepDate .NET library in my own application?

NepDate is the open-source .NET library that powers every Bikram Sambat calculation inside NepDate Widget. It is published on NuGet as a separate package and can be used independently in any .NET application.

**NuGet package:** https://www.nuget.org/packages/NepDate/

### Install the NepDate library

```bash
dotnet add package NepDate
```

Or via Package Manager Console:

```powershell
Install-Package NepDate
```

Or in your `.csproj`:

```xml
<PackageReference Include="NepDate" Version="2.0.7" />
```

Targets: .NET Standard 2.0 (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5–10, Xamarin, Unity, MAUI). `ISpanFormattable` requires .NET 6 or later; `IParsable<NepaliDate>` and `ISpanParsable<NepaliDate>` require .NET 7 or later.

### How do I convert a BS date to AD (Gregorian)?

```csharp
using NepDate;

var bs = new NepaliDate(2083, 2, 15);   // BS year 2083, month 2 (Jestha), day 15
DateTime ad = bs.EnglishDate;           // → 2026-05-29 AD

Console.WriteLine(bs.Year);             // 2083
Console.WriteLine(bs.Month);           // 2
Console.WriteLine(bs.Day);             // 15
Console.WriteLine(bs.EnglishDate);     // 2026-05-29 00:00:00 (DateTime)
```

### How do I convert an AD (Gregorian) date to BS?

```csharp
var ad = new DateTime(2026, 5, 29);
NepaliDate bs = new NepaliDate(ad);     // constructor from DateTime
// or via extension method:
NepaliDate bs2 = ad.ToNepaliDate();
Console.WriteLine(bs.ToString());       // "2083/02/15"
```

### How do I parse a BS date from a string?

```csharp
// NepaliDate.Parse - throws InvalidNepaliDateFormatException on bad input
var bs = NepaliDate.Parse("2083-02-15");
var bs2 = NepaliDate.Parse("२०८३/०२/१५");  // Nepali digits supported

// NepaliDate.TryParse - non-throwing
if (NepaliDate.TryParse("2083 Jestha 15", out var bs3))
{
    // bs3 is valid
}
```

Accepted formats: `YYYY/MM/DD`, `YYYY-MM-DD`, `YYYY.MM.DD`, `YYYY MM DD`, Nepali digits (Unicode Devanagari), full and short Nepali month names in English (e.g., `Jestha`, `Jes`), 100+ spelling variants.

### How do I format a NepaliDate as a string?

```csharp
var bs = new NepaliDate(2083, 2, 15);
bs.ToString();                   // "2083/02/15" (default)
bs.ToString("yyyy MM dd");        // "2083 02 15"  ← tokens are lowercase
bs.ToString("dd MMMM yyyy");      // "15 Jestha 2083"
bs.ToString("D");                 // "Jestha 15, 2083"  (IFormattable specifier)

// Nepali Unicode digits - separate methods, not a ToString parameter:
bs.ToUnicodeString();             // "२०८३/०२/१५"
bs.ToLongDateUnicodeString();    // "जेठ १५, २०८३"
```

### How do I use NepDate in ASP.NET model binding?

`NepaliDate` has a registered `TypeConverter`. It automatically works in ASP.NET MVC/Web API model binding when passed as a query string or route parameter in `YYYY-MM-DD`, `YYYYMMDD` (int), or any recognized string format. No additional registration is required.

### What is the supported date range?

BS 1901/01/01 to BS 2199/12/30 (approximately AD 1844-04-13 to AD 2143-04-12). Calendar metadata (Tithi, holidays, event names) is available for BS 2001 through BS 2089.

---

## How do I configure RunBox search prefixes?

RunBox is NepDate Widget's global command launcher (hotkey: `Ctrl+Shift+Space`). It ships with 22 built-in search prefixes. You can add custom prefixes or disable built-ins by editing `shortcuts.json`.

### Where is shortcuts.json?

The file is in the `config/` subdirectory of your NepDate Widget data folder:
- Store install: `%LOCALAPPDATA%\Packages\{PackageFamilyName}\LocalCache\Local\NepDateWidget.Store\AppData\config\shortcuts.json`
- Direct install: `%LOCALAPPDATA%\NepDateWidget\AppData\config\shortcuts.json`
- Portable: `{exe folder}\AppData\config\shortcuts.json`

You can open this directory directly from NepDate Widget: Settings → Data Files → Open Config Folder.

### shortcuts.json format

```json
[
  { "Key": "shop", "Url": "https://example.com/search?q={query}", "Name": "My Shop" },
  { "Key": "fb", "Disabled": true }
]
```

Field rules:
- `Key`: letters and digits only, case-insensitive (e.g., `"shop"`, `"yt2"`). Required.
- `Url`: must contain exactly one `{query}` placeholder. Also accepts `{year}` for the current calendar year. Required unless `Disabled` is `true`.
- `Name`: display name shown in the `"Search {Name}..."` hint label. Required for non-disabled entries.
- `Disabled`: set to `true` to suppress a built-in prefix without providing a replacement.

Changes take effect immediately - no restart needed.

---

## How do I add custom scripts to RunBox?

Edit `scripts.json` in the same `config/` folder as `shortcuts.json`. Invoke scripts from RunBox by typing `scr ` followed by the script name.

### scripts.json format

```json
[
  {
    "Name": "cleanup",
    "Description": "Delete temp files",
    "Path": "C:\\Scripts\\cleanup.ps1",
    "Interpreter": "powershell"
  }
]
```

Field rules:
- `Name`: the keyword used to find the script in RunBox. Required.
- `Description`: shown in the suggestion list. Optional.
- `Path`: absolute path to the script file. Use `{APPDIR}` to reference scripts bundled beside the app executable.
- `Interpreter`: `powershell`, `pwsh`, `cmd`, `python`, or `wsl`. Required.

The script runs in its own terminal window. RunBox closes immediately on launch.

---

## Troubleshooting

1. **Microsoft Store install fails with error 0x80073CF0 or "This app can't run on your PC"**
   Windows version is below 1809 (build 17763). NepDate Widget requires Windows 10 version 1809 or later. Run Windows Update before retrying. To check your build: Win+R → `winver`.

2. **App mini bar does not appear after install**
   NepDate Widget positions itself near the bottom-right of the screen on first launch. If your taskbar is on the left, right, or top, or if you have multiple monitors, the pill may appear off-screen. Resolution: right-click the NepDate Widget system tray icon (if visible) and select Reset Position, or uninstall and reinstall to reset the saved position from Settings.

3. **RunBox hotkey (Ctrl+Shift+Space) does not respond**
   Another application has registered the same hotkey globally. Common conflicts: terminal apps, clipboard managers, screenshot tools. Resolution: open NepDate Widget → Settings → RunBox Hotkey → record a different key combination.

4. **Preeti conversion output is garbled**
   The source text uses a different legacy Nepali encoding (not Preeti). NepDate Widget supports eight encodings: Preeti, Kantipur, Himalaya, Sagarmatha, and four others. In the Text tab, try each encoding option until the output is correct Devanagari Unicode.

5. **NepDate .NET library: `InvalidNepaliDateFormatException` on `NepaliDate.Parse`**
   The input string does not match any recognized format. Common causes: (a) the string is in AD format rather than BS - construct from a `DateTime` via `new NepaliDate(adDate)` or `adDate.ToNepaliDate()` instead; (b) the month number exceeds the month's day count for that year (BS months have 29–32 days, varying by year); (c) the year is outside the supported range (BS 1901–2199). Use `NepaliDate.TryParse` for non-throwing validation.

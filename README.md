<div align="center">

<img width="400" height="350" alt="NepDate Widget" src="https://github.com/user-attachments/assets/a80a7d70-8d8a-4e3d-8c85-a83023e6002a" />

# NepDate Widget

A desktop widget for Nepali (BS) and English (AD) dates on Windows. Always on screen, never in your way.

[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square&logo=windows)](https://github.com/RajuPrasai/NepDateWidget/releases)
[![Version](https://img.shields.io/badge/version-1.0.0-brightgreen?style=flat-square)](https://github.com/RajuPrasai/NepDateWidget/releases)
[![Framework](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/license-PolyForm%20Strict%201.0.0-blue?style=flat-square)](LICENSE)

</div>

---

## Summary

NepDate Widget sits on your desktop as a compact bar showing the current Nepali (Bikram Sambat) and English (Gregorian) date and time. Click it to expand into a full calendar with conversion tools and settings.

---

## Features

### Collapsed Mini Bar
<img width="390" height="62" alt="mini-mode" src="https://github.com/user-attachments/assets/49d38ea9-6982-4dc1-908c-679301001d5e" />

The mini bar shows two configurable lines. Each element is independently toggleable from Settings.

**Line 1 - Time row** *(hidden when timezone is off)*

Clock in 12h or 24h format, a timezone label (e.g. `Singapore +08:00`), and a UTC offset.

**Line 2 - Date row**

Day of week, Nepali (BS) date, and English (AD) date - each toggle independently.

---

### Calendar

<img align="right" width="300" src="https://github.com/user-attachments/assets/9b1c9eac-e723-424b-a0b8-751c36d352ff" alt="Calendar view" />

Full Bikram Sambat month grid with English day numbers alongside each BS date. Navigate months with the arrow buttons or scroll the mouse wheel directly on the grid.

Today's cell is highlighted with your chosen accent color, and a one-click **Today** button brings you back from any month. The footer shows the current **fiscal year and quarter** at a glance.

Right-click any cell to copy that date in your preferred format.

<br clear="right" />

---

### Tools

Convert, Days, and Time are accessible from the **Tools tab** or directly via the right-click context menu.

#### Convert

<img align="right" width="300" src="https://github.com/user-attachments/assets/087613b5-8ddf-4476-b274-86362295605f" alt="Convert tool" />

BS ↔ AD date conversion. Type in either direction and the result appears instantly in both short and long formats. The widget remembers the last used direction so you pick up exactly where you left off.

<br clear="right" />

#### Days

<img align="right" width="300" src="https://github.com/user-attachments/assets/51a39882-4944-4449-801e-68662a0b7a29" alt="Days tool" />

**Add / Subtract** - Enter a BS date and a positive or negative day offset. The resulting BS date is returned immediately.

**Difference** - Enter any two BS dates. The result breaks down the gap in years, months, and days, plus the total raw day count.

<br clear="right" />

#### Time

<img align="right" width="300" src="https://github.com/user-attachments/assets/02d528b0-c8a7-4fbc-82b0-8d0d9f433bb4" alt="Time converter" />

Timezone converter with compact labeled dropdowns (e.g. `Nepal +05:45`, `Singapore +08:00`). Select any two system timezones, enter a time, and the conversion appears instantly. A **Swap** button reverses the direction in one click.

<br clear="right" />

---

### Unit

Area converter between traditional Nepali land units (Ropani, Aana, Paisa, Dam) and metric/imperial (sq metres, sq feet). Weight and volume converter spanning traditional Nepali units (Dharni, Pawa, Mana, Pathi, Muri, Tola), metric (kg, g, mg, tonne, litre), and imperial (lb, oz). A character-level Nepali script reference is also included.

---

### Text Tools

**Password** - Configurable generator with adjustable length and charset toggles (uppercase, lowercase, digits, symbols, Nepali characters). A strength indicator shows which criteria are met in real time.

**Word Count** - Counts words, characters, and characters excluding spaces for any pasted or typed text.

**Unicode / Preeti** - Converts Preeti-encoded legacy Nepali text to Unicode and back, character by character.

**Script** - Batch-converts `.docx` and `.txt` files between Preeti and Unicode encodings.

---

### Banking

Interest and EMI calculators are accessible from the **Banking tab** or directly via the right-click context menu.

#### Interest

<img align="right" width="300" src="https://github.com/user-attachments/assets/d32d62dc-c0cf-4417-83e7-7840be6b1b56" alt="Interest calculator" />

Simple interest calculator with support for changing rates across multiple periods.

Enter a principal amount and a global From / To date range. Switch freely between BS and AD date input. Add as many rate periods as needed - each row carries its own From date and annual rate. The **Add Period** button auto-fills the next row's start date to the first day of the following month.

Results show a per-row interest breakdown and a grand total line.

<br clear="right" />

#### EMI

Loan repayment schedule on the reducing balance method. Enter principal, annual interest rate, and loan term in months. The calculator produces a full amortization table showing principal, interest, and outstanding balance for each period, plus cumulative totals.

---

### Network Tools

Six tools accessible from the **Network tab** or directly via the right-click context menu.

- **My IP** - Fetches your public IPv4 and IPv6 addresses via `api.ipify.org` / `api64.ipify.org`.
- **Ping** - ICMP ping to any host with configurable count and per-reply RTT.
- **Scan** - discovers all active hosts on the local subnet; shows IP, hostname, MAC address, manufacturer, and inferred device type.
- **Traceroute** - Hop-by-hop path trace with RTT per hop.
- **WHOIS** - Raw WHOIS lookup against the appropriate registry.
- **DNS** - DNS record query (A, AAAA, MX, TXT, CNAME, NS, and others).

---

### More

**Notes** - Per-day notes tied to the BS calendar. Each note is attached to its date and accessible from the calendar cell or the Notes tab.

**Reminders** - One-off and recurring reminders with title, date, and optional time. Overdue and upcoming reminders surface in the mini bar footer.

**Documents** - A library for `.docx` files with built-in Preeti-to-Unicode detection and batch conversion. Import documents, browse the library, and convert encodings in place.

---

### RunBox

A global hotkey launcher (default: `Ctrl+Shift+Space`) that opens a spotlight-style input field. It can run programs, open files or folders, open URLs, or fall back to a web search.

Special modes:
- `= <expression>` - inline calculator; press Enter to copy the result to clipboard
- `scr <name>` - run a named user script from the Scripts registry
- `<prefix> <query>` - shortcut-based web search (e.g. `yt cats` opens YouTube search for "cats")
- `help` - opens the built-in RunBox help reference

Type `Escape` once to close the dropdown, again to dismiss the launcher. History is saved across sessions and supports Tab completion.

---

### Appearance

<img align="right" width="300" src="https://github.com/user-attachments/assets/74a793e1-08cb-44f8-85e9-ca658dd8b690" alt="Appearance settings" />

**Themes:** Light and Dark

**Color presets:** 10 palettes × 2 themes = 20 combinations

| Preset | Character |
|---|---|
| Forest | Muted green - the default |
| Ocean | Cool teal |
| Default | Neutral grey-blue |
| Sunset | Warm amber |
| Cherry | Rose pink |
| Aurora | Purple-green |
| Midnight | Deep navy |
| Slate | Cool grey |
| Monochrome | Black and white |
| Ember | Burnt orange |

**Corner styles:** Rounded or Sharp

Switch between **English** and **नेपाली** instantly without restarting the app.

<br clear="right" />

---

### Other Settings

<img align="center" width="400" src="https://github.com/user-attachments/assets/35617d8d-5b20-4442-baec-0ebccb5cb267" alt="Settings panel" />

<br clear="center" />

---

### Context Menu

<img align="right" width="300" src="https://github.com/user-attachments/assets/5ba0c4c5-07d9-4a04-b50a-39f62ebecde0" alt="Context menu" />

Right-click anywhere on the widget to open the menu. Tool header labels are non-clickable; clicking a tool item expands the widget and switches directly to that tool.

```
Tools
├── Convert
├── Days
└── Time
Unit
├── Area
└── Weight
Text
├── Unicode / Preeti
├── Word Count
├── Password
└── Script
Banking
├── Interest
└── EMI
Network
├── My IP
├── Ping
├── Scan
├── Traceroute
├── WHOIS
└── DNS
Copy Today
├── BS short    (2082/01/15)
├── BS long     (Baisakh 15, 2082)
├── AD short    (2025-04-28)
└── AD long     (April 28, 2025)
---
More
Settings
Exit
```

<br clear="right" />

---

## Requirements

| Requirement | Detail |
|---|---|
| OS | Windows 10 version 1809 or later, Windows 11 |
| Runtime | None - self-contained build, .NET 10 bundled |
| Architecture | x64 |
| Admin rights | Not required |

---

## Installation

**Microsoft Store (recommended)**
Search for *NepDate Widget* in the Microsoft Store and click **Get**.
No admin rights required. Updates are delivered automatically by the Store.

**Portable zip (GitHub Releases)**

1. Download the latest zip from [Releases](https://github.com/RajuPrasai/NepDateWidget/releases).
2. Extract to a permanent folder (e.g. `C:\Tools\NepDateWidget\`).
3. Run `NepDateWidget.exe`. No installer, no admin rights needed.
4. Enable **Start with Windows** in Settings to auto-launch on login.

**Files created at runtime:**

Location depends on build type:

- **Store build:** `%LOCALAPPDATA%\NepDateWidget.Store\AppData\`
- **Portable build:** the `AppData\` subfolder beside `NepDateWidget.exe` (created automatically when a `portable.flag` file exists beside the EXE)

Files (organized into subdirectories within the data folder):

**config/** - user-editable configuration
- `settings.json` - app preferences
- `shortcuts.json` - RunBox URL shortcut prefixes
- `scripts.json` - RunBox named scripts

**data/** - user content
- `notes.json` - per-day calendar notes
- `reminders.json` - one-off and recurring reminders
- `documents.json` - document library metadata
- `run-history.json` - RunBox launch history

**Root:**
- `nepdate.log` - diagnostic log, auto-trimmed at 10 MB (configurable up to 100 MB)

---

## Uninstall

**Portable build (zip):**

1. In the widget, open **Settings** and disable **Start with Windows**.
2. Right-click the widget and choose **Exit**.
3. Delete the folder where you placed `NepDateWidget.exe`.
4. Optional: delete the user data folder `%LOCALAPPDATA%\NepDateWidget\AppData\`
   if it was created by an older unpackaged build.

**Store build:**

Use **Settings → Apps → Installed apps** in Windows to uninstall
*NepDate Widget*. User data under `%LOCALAPPDATA%\NepDateWidget.Store\`
is left intact; delete that folder manually for a full cleanup.

---

## Privacy

NepDate Widget does not collect telemetry, analytics, or usage data.
All calendar, date conversion, reminder, and notes processing happens
locally on your machine.

Network requests are made only in the following cases, and only when
you opt in:

- **Network Tools** (only when you actively run a tool) - outbound
  DNS, ICMP (ping), HTTP(S), or WHOIS requests against the host you
  enter. Public IP lookup uses `https://api.ipify.org` /
  `https://api64.ipify.org`.

Local data files are stored in plain JSON at
`%LOCALAPPDATA%\NepDateWidget.Store\AppData\` (Store build) or beside
the EXE (portable build). Do not store secrets in notes or reminders.

---

## Troubleshooting

**The widget never appears after launch.**
The saved window position may be off-screen (for example after
unplugging an external monitor). Quit the app from the tray context
menu if visible, then delete `settings.json` from the data folder
(`%LOCALAPPDATA%\NepDateWidget.Store\AppData\` for Store, or beside `NepDateWidget.exe` for portable) and
restart. The widget will return to its first-run position next to the
taskbar.

**SmartScreen blocks the app or shows "Unknown publisher".**
The portable build is not yet code-signed. If Windows blocks `NepDateWidget.exe`,
click **More info → Run anyway**. A signed build is planned via SignPath Foundation.
This does not apply to the Store build (Store apps are verified by Microsoft).

**Settings reset themselves on restart.**
This happens when `settings.json` cannot be parsed. The app keeps a
timestamped backup as `settings.json.broken-<yyyyMMdd-HHmmss>` in the
same folder. Open `nepdate.log` for the parse error and report the
backed-up file with your bug report if needed.

**Where are the logs?**
Store build: `%LOCALAPPDATA%\NepDateWidget.Store\AppData\nepdate.log`.
Portable build: `AppData\nepdate.log` beside `NepDateWidget.exe`.

---

## License

This project is **source-available**, not OSI open source. It is
distributed under the [PolyForm Strict License 1.0.0](LICENSE).

In short:

- You **may** read, audit, build, and run the source code for personal
  evaluation.
- You **may not** redistribute the source code or compiled binaries.
- You **may not** publish a derivative work, fork, or competing
  product based on this code.
- You **may not** use the software to operate a business or provide a
  service to others.

For a commercial license or any use beyond the above, contact the
copyright holder.

See [SECURITY.md](SECURITY.md) for vulnerability reporting,
[CONTRIBUTING.md](CONTRIBUTING.md) for contribution terms (note: by
submitting a contribution you grant the project owner the right to
relicense it), [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community
expectations, and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for
upstream dependency and bundled-font attributions (NepDate,
DocumentFormat.OpenXml under MIT; Cascadia Code, DM Sans, IBM Plex Sans,
Imprima, Inter, Lato, Montserrat, Noto Sans, Nunito, Open Sans, Poppins,
Quicksand, Raleway, Roboto, Rubik, Source Sans 3, Work Sans under SIL OFL 1.1).

---

## Building from Source

**Requirements:** .NET 10 SDK, Windows

```powershell
# Clone
git clone https://github.com/RajuPrasai/NepDateWidget.git
cd NepDateWidget

# Build
dotnet build src/NepDateWidget/NepDateWidget.csproj -c Debug

# Run tests
dotnet test tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj

# Publish (single EXE)
dotnet publish src/NepDateWidget/NepDateWidget.csproj -c Release -r win-x64
```

The published output is a single self-contained EXE. No separate runtime or DLLs required on the target machine.

---

## Tech Stack

| Item | Detail |
|---|---|
| Framework | .NET 10, WPF |
| UI pattern | MVVM (no framework - hand-rolled) |
| Calendar engine | [NepDate](https://www.nuget.org/packages/NepDate) |
| Tests | xUnit, 1534 tests, no mocking frameworks |
| Settings storage | JSON in `%LOCALAPPDATA%\NepDateWidget.Store\AppData\` (Store) or `AppData\` beside EXE (portable), atomic write (tmp → replace) |

---

<div align="center">
Made with care for the Nepali community &nbsp;|&nbsp; Copyright © 2025-2026 RajuPrasai
</div>

# NepDate Widget Changelog - Version History & Release Notes

All notable changes to NepDate Widget are documented here. Versions follow [semantic versioning](https://semver.org).

**Source page:** https://nepdatewidget.rajuprasai.com.np/changelog.html

## v1.0.2 - May 2026 (Latest)

### New features

- **Image Tools:** batch compress, resize, and convert images in a single pass. Supports 30+ input formats including RAW camera files and PDF. Five-level quality slider, metadata stripping, and format-specific options
- **ID Photo:** crop photos to exact passport and ID dimensions (Passport/MRP, Auto, Stamp, US/DV, or custom). Tile multiple copies onto a print sheet (A4, 4R, 5R, or custom size) for home or studio printing
- **QR Code generator:** create QR codes from text, URLs, or contact info and save as PNG
- **WiFi scanner:** discover nearby wireless networks with signal strength, channel, and security details

### Improvements

- Faster widget expand and smoother animations throughout the app
- Reduced memory usage and CPU overhead when the widget is idle
- Improved calendar grid rendering performance during month navigation
- Visual polish and consistency updates across all tabs
- RunBox now opens faster with improved search and history ranking
- Better keyboard navigation in the date converter and banking tools

## v1.0.1 - 2026 (Patch)

### Bug fixes & enhancements

- Fixed calendar not refreshing correctly after returning from sleep or hibernate
- Improved mini bar positioning on multi-monitor setups when display scaling differs between screens
- Minor visual adjustments to theme colors and font rendering in dark mode

## v1.0.0 - 2026 (Initial Release)

The first public release of NepDate Widget. A Nepali calendar that lives on your Windows taskbar, with every Nepali tool you actually use.

### Mini bar

- Always-on-screen collapsed bar with two configurable lines
- Time row: 12h / 24h clock, timezone label, UTC offset
- Date row: day of week, Bikram Sambat date, Gregorian date
- Each element toggles independently from Settings
- Fades transparent when not hovered - toggleable from Settings

### Calendar

- Bikram Sambat month grid with English day numbers alongside
- Tithi, public holidays, and festival data sourced from authoritative BS calendars
- Today highlighted with the chosen accent color
- Mouse wheel and arrow navigation, one-click Today button
- Right-click any cell to copy the date in BS or AD, short or long format
- Footer shows current fiscal year and quarter
- **Holiday countdown:** the calendar header counts down to the next public holiday; hover to see upcoming holidays for the next year. Toggleable from Settings
- **Daily events notification:** on the first launch of each day, a notification lists today's festivals and observances. Toggleable from Settings
- **Calendar dots:** days with a note show a colored dot; days with a reminder show a red dot
- **Day popup:** click any calendar day to see its note and reminders, and edit or delete them inline without opening the More tab

### Date tools

- **Convert:** instant BS ↔ AD with last-direction memory
- **Days:** add or subtract days, plus difference between two BS dates broken down into years, months, days
- **Time:** timezone converter with compact labeled dropdowns and one-click swap

### Bank tools

- **EMI:** monthly EMI, total interest, and total payment with year-by-year and month-by-month amortization breakdown
- **Interest:** multi-period simple interest with rate changes over time, BS or AD input

### Text tools

- **Password:** generator with adjustable length, A–Z / a–z / 0–9 / symbols / Nepali toggles, and live strength meter
- **Word:** word, character, sentence, and line counts
- **Preeti ↔ Unicode:** convert Preeti-encoded text or entire files to Unicode Devanagari, or reverse
- **Script:** Devanagari ↔ Romanized Nepali conversion with file support

### Network tools

- **My IP:** public and private addresses with geolocation
- **Ping:** per-packet RTT plus Min / Max / Avg summary
- **IP Scanner:** scans the local subnet, parallel-pings every host, and maps each device with hostname, MAC address, and manufacturer
- **Traceroute, Whois, DNS:** additional network diagnostics

### Unit tools

- **Area:** Bigha, Kattha, Dhur, Ropani, Aana, Paisa, Dam, Khetmuri, plus Sq. Metres and Sq. Feet
- **Weight:** Dharni, Pawa, Mana, Pathi, Muri, plus kilograms, grams, tola, pounds, litres etc.
- **Script:** Devanagari ↔ Romanized Nepali (also accessible from Unit tab)

### More tab

- **Documents:** attach files (PDF, images, Word, Excel) with a title, tags, and optional notes. Open the file or jump to its folder directly from the widget. The Documents folder is automatically pinned to Windows Explorer Quick Access on startup so it appears in every file-upload dialog sidebar.
- **Notes:** quick local notes pinned to BS dates
- **Reminders:** one-shot or recurring (daily, weekly, monthly, yearly), pinned to BS dates

### RunBox

- Global hotkey (default Ctrl+Shift+Space) summons a compact command launcher from anywhere in Windows
- Runs commands exactly like the Windows native Run dialog; detects URLs and opens them in the default browser; falls back to a Google search for anything else
- History of up to 500 entries with prefix-ranked filtering, arrow-key navigation, and Tab completion
- Hotkey configurable from Settings

### Appearance & settings

- Light and Dark themes
- 10 color presets × 2 themes = 20 combinations (Forest, Ocean, Default, Sunset, Cherry, Aurora, Midnight, Slate, Monochrome, Ember)
- Custom Saturday and holiday highlight color with 12 choices, independent of the theme accent
- Rounded or sharp corners
- 20 fonts available in the font picker (3 system fonts + 17 embedded)
- English / नेपाली UI switch with no restart
- Start with Windows, enabled automatically on first launch and kept in sync with the registry thereafter
- RunBox hotkey configuration (record any key combination)
- Notification display duration and sound toggle
- Animations on/off for a fully static UI if preferred
- Calendar display: tithi, events, English day numbers, public holiday highlighting, and Saturday/Sunday highlighting each toggle independently
- Widget window remembers its last position on screen
- Reset all settings to defaults with one click

### Backup & restore

- Export all data as a single ZIP - settings, notes, reminders, and documents included
- Restore from a backup to move to a new machine or recover after a reinstall

### Distribution & storage

- Available through the Microsoft Store - per-user install, no admin prompt, auto-updates delivered by the Store
- Settings, notes, and reminders stored under `%LOCALAPPDATA%`
- No telemetry, no network calls except for explicit network tools

### Under the hood

- Built on [NepDate](https://nepdate.rajuprasai.com.np/) for every BS computation
- .NET 10 desktop, PolyForm Strict 1.0.0

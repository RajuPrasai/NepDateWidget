# NepDate Widget Features - Every Tab, Every Tool

Full documentation for all tools in NepDate Widget, a free Windows 10/11 taskbar widget combining 25+ Nepali productivity tools.

**Download:** [Microsoft Store](https://apps.microsoft.com/detail/9PG97WBJX1NQ) | **Website:** https://nepdatewidget.rajuprasai.com.np/

---

## Date Tab - BS↔AD Converter and Date Math

### BS to AD Conversion
Type any Bikram Sambat year, month, and day. The Gregorian equivalent appears instantly. Accepts:
- Nepali digits: १, २, ३, ४, ५, ६, ७, ८, ९, ०
- English digits: 1, 2, 3...
- Full month names: Baishakh, Jestha, Ashadh, Shrawan, Bhadra, Ashwin, Kartik, Mangsir, Poush, Magh, Falgun, Chaitra
- Short names and any separator (/, -, ., space)

### AD to BS Conversion
Type any Gregorian date - the Bikram Sambat equivalent appears immediately.

### Date Difference Calculator
Enter two BS dates to get the exact gap in years, months, and days. Useful for calculating age from a Nepali birth certificate date.

### Date Arithmetic
Add or subtract a number of days from any BS date to get a new BS date. Useful for deadline calculations on BS-dated documents.

Supported range: BS 1970 to BS 2100 (AD 1913 to AD 2043).

---

## Calendar Tab - Bikram Sambat Patro (Nepali Calendar)

Full monthly grid for any BS month from 1970 to 2100. Each cell shows:
- BS day number
- Corresponding AD date

Optional overlays (individually toggleable):
- **Tithi** (तिथि) - lunar day from the Hindu calendar
- **Public holidays** - Nepal government gazetted holidays
- **Festivals** - Dashain, Tihar, Chhath Puja, Holi, Teej, Janai Purnima, Krishna Janmashtami, and all major Nepali observances with names in English and Nepali
- **Fiscal year markers** - Nepal fiscal year runs Shrawan 1 to Ashadh end
- **Saturday/Sunday highlights**

The taskbar mini bar shows a **live countdown** to the next public holiday in days.

Current year: **BS 2083** (April 13, 2026 – April 13, 2027 AD).

---

## Bank Tab - Loan EMI Calculator

### EMI Calculator
Input: loan principal (any currency), annual interest rate (%), loan term in months, and BS start date.
Output:
- Monthly EMI amount
- Total interest paid
- Total repayment amount
- Full amortization schedule with each payment date in Bikram Sambat

### Multi-Period Interest Calculator
Compare scenarios across different loan terms or interest rates in the same view.

---

## Text Tab - Legacy Font Converter, Password Generator, QR Code

### Nepali Legacy Font to Unicode Converter
Converts legacy Nepali typeface-encoded text to Unicode Devanagari and back. Supported legacy fonts: **Preeti, Kantipur, Himalaya, Sagarmatha**, and four more (8 fonts total). Paste an entire document for bulk conversion - no line-by-line processing needed.

Background: Preeti and similar legacy fonts predate Unicode. Documents typed in them appear as garbled Latin/ASCII characters on modern systems. This converter makes those documents readable in any modern application, browser, or search engine.

### Password Generator
- Configurable length slider
- Toggle character sets: uppercase, lowercase, digits, symbols, Nepali characters
- Live entropy-based strength meter
- Paste any existing password to check its strength

### QR Code Generator and Decoder
Generate QR codes for:
- **Plain text** - any string
- **URL** - direct link QR
- **vCard** - contact card
- **WiFi** - reads your saved Windows WiFi profiles, password auto-filled for user-owned profiles, generates a scannable connection code

Export as PNG or copy to clipboard. **Decode mode**: paste or drop any QR image to read its encoded content.

---

## Unit Tab - Traditional Nepali Unit Converter

### Land Area Units
Converts between: Ropani (रोपनी), Aana (आना), Paisa (पैसा), Dam (दाम), Bigha (बिघा), Katha (कठ्ठा), Dhur, Khetmuri, and standard units (square metres, square feet, acres). Results update as you type.

Reference values: 1 Ropani = 5476 sq ft = 508.72 sq m. 1 Bigha = 6772.63 sq m (Terai standard).

### Weight Units
Converts between: Tola, Chatak (Pau), Dharni, Mana, Pathi, Muri, and standard units (grams, kilograms, ounces, pounds).

Reference: **1 tola = 11.6638 grams** (Nepal Weights and Measures Act).

---

## Network Tab - Seven Diagnostics Tools (Requires Internet)

- **Ping** - sends ICMP echo requests to any host, shows live RTT graph and packet loss
- **Traceroute** - hop-by-hop path and latency to any destination
- **IP Scanner** - scans a subnet for live hosts, lists IP and MAC addresses
- **Whois** - queries domain registration records
- **DNS Lookup** - resolves A and AAAA records for a hostname
- **My IP** - shows public IPv4 and IPv6, all active local interface IPs
- **WiFi Scanner** - discovers nearby wireless networks with signal strength, channel, and security type

All other tabs work fully offline.

---

## More Tab - ID Photo, Image Tools, Notes, Reminders, Documents

### ID Photo Tool
Crop any photo to standard print dimensions:
- Passport/MRP: 35×45 mm
- Auto/VISA Size: 25×30 mm
- Stamp Size: 20×25 mm
- US/DV Lottery Visa: 51×51 mm
- Custom: any dimension

Crop modes: square or circle. Export a single cropped photo or tile copies onto a print sheet (A4, 4R, 3R, 5R, or custom) for home or studio printing.

### Image Tools
Batch process multiple images in one pass. Operations:
- **Compress**: 5-level quality slider, metadata stripping, PDF/GIF/TIFF-specific options
- **Resize**: exact pixel dimensions
- **Convert format**: any supported format to any other

30+ supported input formats: JPEG, PNG, WebP, AVIF, GIF, BMP, TIFF, ICO, TGA, **HEIC**, RAW formats (ARW, CR2, CR3, DNG, NEF, ORF, RAF, RW2, ERF, PEF, X3F), PDF.

Output formats: JPEG, PNG, WebP, AVIF, GIF, BMP, TIFF, ICO, TGA, PDF.

### Notes
Attach freeform notes to specific Bikram Sambat calendar dates. Notes appear as indicator dots on the calendar grid.

### Reminders
One-time or recurring reminders (daily, weekly, monthly, yearly). Snooze support. Fires system notifications even when the widget is collapsed. All stored locally as JSON.

### Document Manager
Pin documents, folders, and executables for one-click access from the widget without navigating the filesystem.

---

## RunBox - Global Command Launcher

Summoned with a configurable hotkey (default: Ctrl+Shift+Space) from anywhere in Windows - no need to open or click the widget. Features:
- **Web search prefixes**: type `g nepal` to search Google for "nepal", `yt nepali songs` for YouTube, `wiki Bikram Sambat` for Wikipedia, plus custom prefixes
- **Arithmetic**: type `450 * 12` to get 5400
- **File/folder/app opening**: type a path or partial name to open
- **Custom scripts**: PowerShell (.ps1), batch (.bat/.cmd), Python (.py), WSL - assign any keyword

All prefixes and scripts are defined in plain JSON files in %LOCALAPPDATA%. Edit with any text editor - changes take effect immediately, no restart.

---

## Settings Tab

- **20 color themes**: 10 light presets, 10 dark presets
- **20 font choices**
- **Corner style**: rounded or sharp
- **Transparency**: optional transparency when the widget is collapsed to the mini bar
- **Language**: English or Nepali (नेपाली) - switch instantly, no restart
- **Calendar toggles**: individually enable/disable tithi, holidays, AD day numbers, Saturday/Sunday highlights

---

## How to Convert a Nepali (BS) Date to English (AD)

1. Install NepDate Widget from the [Microsoft Store](https://apps.microsoft.com/detail/9PG97WBJX1NQ) (free, no admin required)
2. Click the mini bar on the taskbar to expand the widget
3. Click the **Date** tab
4. Select **BS → AD** direction
5. Type the Bikram Sambat year, month, and day
6. The Gregorian date appears instantly - no button press needed

## How to Convert an English (AD) Date to Nepali (BS)

Follow the same steps above but select **AD → BS** and type the Gregorian date.

---

## Frequently Asked Questions

**How do I convert Preeti to Unicode?**
Open the Text tab, select Unicode converter. Paste Preeti text on the left - Unicode Devanagari appears on the right instantly. Also works for Kantipur, Himalaya, Sagarmatha, and four more legacy fonts.

**How do I convert bigha to ropani?**
Open the Unit tab, select Area mode, choose Bigha as source and Ropani as target, type the value.

**How do I calculate a Nepal bank loan EMI?**
Open the Bank tab, enter loan amount, annual interest rate %, term in months, and BS start date. Monthly EMI, total interest, and amortization schedule are shown immediately.

**How do I find my IP address?**
Open the Network tab, select My IP. Public IPv4/IPv6 and all local interface IPs appear on one screen.

**How do I generate a WiFi QR code?**
Open the Text tab, select QR Code, switch to WiFi type. Select your network from the dropdown, enter the password, click Generate. Anyone who scans the code connects without typing credentials.

**Does it show Nepali calendar for 2083, 2084, 2085?**
Yes. The calendar covers BS 1970 to 2100 with tithi and holidays for every month.

**Can I resize and compress images?**
Yes. The Image Tools section handles batch compression, resize, and format conversion in one pass. 30+ input formats including RAW and HEIC.

**How do I make a passport size (PP size) photo?**
Open the More tab, select ID Photo. Drop a photo, choose the Passport/MRP 35×45 mm preset, position the crop, export. Optionally tile copies onto an A4 or 4R print sheet.

**How is NepDate Widget different from Hamro Patro?**
Hamro Patro is a mobile and web platform with news and horoscopes. NepDate Widget is a native Windows desktop app: offline, zero telemetry, taskbar integration, EMI calculator, Preeti converter, network tools, RunBox launcher, and image processing - tools Hamro Patro does not offer.

**What legacy Nepali fonts are supported beyond Preeti?**
Preeti, Kantipur, Himalaya, Sagarmatha, and four additional legacy Nepali typefaces - 8 fonts total.

---

Developer: [Raju Prasai](https://github.com/RajuPrasai)
Source: https://github.com/RajuPrasai/NepDateWidget

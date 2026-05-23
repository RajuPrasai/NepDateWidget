# NepDate Widget - FAQ

Answers organized by topic. For full tool documentation see https://nepdatewidget.rajuprasai.com.np/features.html.md. For installation and developer integration see https://nepdatewidget.rajuprasai.com.np/docs/integration.md.

---

## Bikram Sambat Calendar

**What is Bikram Sambat?**
Bikram Sambat (abbreviated BS; also: Vikram Samvat, Vikram Sambat; Nepali: बिक्रम सम्बत) is Nepal's official civil and administrative calendar. It is used for all government documents, court filings, tax returns, property deeds, school certificates, and employment contracts. Converting between BS and AD is a daily routine for anyone working with Nepali paperwork or living between Nepal and the wider world.

**How far ahead is Bikram Sambat from the Gregorian calendar?**
Approximately 56 years and 8 months. The exact offset varies month-to-month because BS months have irregular lengths (29–32 days each, defined by a fixed lookup table, not a formula). BS 2083 corresponds to April 2026 – April 2027 AD.

**What is the current Bikram Sambat year?**
As of May 2026 AD, the current BS year is 2083 (Jestha 2083). BS New Year 2083 (Nava Varsha) began April 14, 2026 AD. BS New Year 2084 begins April 14, 2027 AD. NepDate Widget's taskbar mini bar always shows the current BS date.

**What are the BS month names?**

| # | Nepali | English transliteration | Approximate Gregorian span |
|---|---|---|---|
| 1 | बैशाख | Baishakh | mid-April to mid-May |
| 2 | जेठ | Jestha | mid-May to mid-June |
| 3 | असार | Ashadh | mid-June to mid-July |
| 4 | साउन | Shrawan | mid-July to mid-August |
| 5 | भदौ | Bhadra | mid-August to mid-September |
| 6 | असोज | Ashwin | mid-September to mid-October |
| 7 | कार्तिक | Kartik | mid-October to mid-November |
| 8 | मंसिर | Mangsir | mid-November to mid-December |
| 9 | पुस | Poush | mid-December to mid-January |
| 10 | माघ | Magh | mid-January to mid-February |
| 11 | फागुन | Falgun | mid-February to mid-March |
| 12 | चैत | Chaitra | mid-March to mid-April |

**How many days does a BS month have?**
BS months have 29, 30, 31, or 32 days depending on both the month and the year. The exact day count is defined by a fixed calendar table; it cannot be derived by formula. NepDate Widget uses the NepDate .NET library which embeds this table for BS 1901 through BS 2199.

**What is tithi?**
Tithi (तिथि) is the lunar day in the Hindu calendar. Each tithi is approximately 23 hours 37 minutes. NepDate Widget shows tithi on the calendar grid for BS 2001 through 2089. Data is from compiled Bikram Sambat calendar references.

**What is the Nepal fiscal year?**
Nepal's fiscal year runs from Shrawan 1 (around mid-July AD) to the last day of Ashadh (around mid-July the following year). For example, fiscal year 2082/83 runs from Shrawan 1, 2082 (approximately mid-July 2025 AD) to the last day of Ashadh 2083 (approximately mid-July 2026 AD). NepDate Widget marks fiscal year start and end on the calendar grid.

---

## Date Conversion

**How do I convert a BS date to an AD (Gregorian) date?**
Open the Date tab, select the BS → AD direction, enter the Bikram Sambat year, month, and day. The Gregorian equivalent appears instantly. Accepted inputs: Nepali digits (१२३), English digits, full or short month names, any separator (/, -, ., space).

**How do I convert an AD date to a BS date?**
Open the Date tab, select the AD → BS direction, enter the Gregorian year, month, and day. The BS date appears immediately.

**How do I calculate a Nepali date from a birth certificate?**
Open the Date tab, select BS → AD, enter the BS birth date from the certificate. The Gregorian birthdate appears. For age calculation in years, months, and days, use the Date Difference tool in the same tab: enter the birth date as the start date and today's BS date as the end date.

**How do I add or subtract days from a BS date?**
In the Date tab, use the Date Arithmetic tool: enter a BS date and a number of days to add or subtract. Useful for calculating deadlines on BS-dated government documents.

**What is the conversion range?**
NepDate Widget supports BS 1970 through BS 2100 (approximately AD 1913 through AD 2043). The NepDate .NET library used internally supports BS 1901 through BS 2199.

**Why doesn't a simple formula work for BS-to-AD conversion?**
BS months have irregular lengths that vary by year and are defined by a table compiled from traditional astronomical observations. There is no algebraic formula that correctly converts all dates. Any tool claiming formula-based conversion will produce wrong results for many dates.

---

## App Installation and Compatibility

**How do I install NepDate Widget?**
Install free from the Microsoft Store: https://apps.microsoft.com/detail/9PG97WBJX1NQ - no admin rights, no account after install. Or download the `.msix` file from https://github.com/RajuPrasai/NepDateWidget/releases.

**What Windows versions are supported?**
Windows 10 version 1809 (October 2018 Update, build 17763) or later, and Windows 11. 64-bit (x64 or ARM64) only.

**Do I need admin rights to install NepDate Widget?**
No. Per-user install. Nothing written to system folders. No UAC prompt.

**Do I need a Microsoft account?**
A Microsoft account is required only for the Store install step. The app itself requires no account and no sign-in.

**Does it work offline?**
Almost entirely yes. Date converter, calendar, EMI calculator, Preeti/legacy font converter, password generator, QR code generator, unit converter, notes, reminders, document manager, RunBox launcher, ID photo, and image tools all work with no internet. Only the network toolkit (ping, traceroute, IP scan, Whois, DNS, My IP) and RunBox web search prefixes need a connection.

**Does NepDate Widget send data anywhere?**
No. Zero telemetry, no analytics, no account required. Outbound calls happen only when you explicitly use a network tool (ping, traceroute, etc.) or a RunBox web search prefix.

**Where is user data stored?**
In the `AppData\` subdirectory of the app data folder:
- Store install: `%LOCALAPPDATA%\Packages\{PackageFamilyName}\LocalCache\Local\NepDateWidget.Store\AppData\`
- Direct install: `%LOCALAPPDATA%\NepDateWidget\AppData\`
- Portable: `{exe folder}\AppData\`

All data is plain JSON files. Back up by copying the folder.

**How do I migrate data to a new PC?**
Copy the entire `AppData\` folder from the old machine to the same path on the new machine. Install NepDate Widget first, then copy the data. Settings, notes, reminders, document shortcuts, and RunBox history are all in that folder.

---

## Tools Reference

**How do I use the Preeti font converter?**
Open the Text tab, select Unicode Converter. Paste Preeti-encoded text on the left - Unicode Devanagari output appears on the right. Also supports Kantipur, Himalaya, Sagarmatha, and four other legacy fonts (8 total). For bulk conversion, paste the entire document. Preeti is a pre-Unicode encoding widely used in Nepal in the 1990s–2000s; text typed in Preeti appears as garbled ASCII characters on modern systems.

**How do I calculate a Nepal bank loan EMI?**
Open the Bank tab, enter the loan principal (any currency), annual interest rate (%), term in months, and a BS start date. The calculator shows monthly EMI, total interest paid, total repayment, and a full amortization schedule with each payment date in Bikram Sambat.

**How do I generate a WiFi QR code?**
Open the Text tab, select QR Code, switch to WiFi type. The tool reads your saved Windows WiFi profiles - select a network, verify or enter the password, click Generate. Export as PNG or copy to clipboard. Anyone who scans the code connects without typing credentials.

**How do I convert bigha to ropani?**
Open the Unit tab, select Area mode, set source to Bigha and target to Ropani (or reverse). Type the value - result updates live. Supported land units: Ropani, Aana, Paisa, Dam, Bigha, Katha, Dhur, Khetmuri, square metres, square feet, acres. Weight units: Tola, Chatak (Pau), Dharni, Mana, Pathi, Muri, and standard SI units.

**What is 1 tola in grams?**
1 tola = 11.6638 grams (per Nepal Weights and Measures Act). NepDate Widget's Unit Converter uses this statutory value.

**Can I use RunBox without clicking the widget?**
Yes. The global hotkey (`Ctrl+Shift+Space` by default) summons RunBox from anywhere in Windows, even when the widget is collapsed. Configure the hotkey in Settings.

**What network tools are included?**
Six tools in the Network tab: Ping (with live RTT graph), Traceroute (hop-by-hop latency), IP Scanner (subnet scan for live hosts; shows IP, hostname, MAC, manufacturer, and device type), Whois lookup, DNS lookup (resolves A and AAAA records for a hostname), and My IP (public IPv4/IPv6 and local interface IPs). All require an internet connection.

**How is NepDate Widget different from Hamro Patro?**
Hamro Patro is a mobile and web platform focused on news, horoscopes, and community features. NepDate Widget is a native Windows desktop application for productivity: it lives on your taskbar, works fully offline, stores zero data on external servers, and includes 25+ tools (EMI calculator, Preeti converter, network toolkit, RunBox launcher, image tools, QR generator) that Hamro Patro does not offer.

**What image formats does the Image Tools tab support?**
Input: JPEG, PNG, WebP, AVIF, GIF, BMP, TIFF, ICO, TGA, HEIC, and all major RAW camera formats (ARW, CR2, CR3, DNG, NEF, ORF, RAF, RW2, ERF, PEF, X3F), PDF - 30+ formats total. Output formats: JPEG, PNG, WebP, AVIF, GIF, BMP, TIFF, ICO, TGA, PDF. Operations: batch compress (5-level quality slider), resize to exact pixel dimensions, format convert - all three in one pass.

**What ID photo print presets are available?**
Passport/MRP 35×45 mm, Auto/VISA Size 25×30 mm, Stamp Size 20×25 mm, US/DV Lottery Visa 51×51 mm, or fully custom dimensions. Square or circle crop. Tiling output to A4, 4R, 3R, 5R, or custom print sheet.

---

## Developer and NepDate Library

**What is the NepDate .NET library?**
NepDate is the open-source, zero-dependency .NET library that powers every BS date calculation in NepDate Widget. Published on NuGet (`dotnet add package NepDate`). Supports BS 1901–2199. Zero heap allocations on both conversion paths (BS→AD: 4.55 ns, AD→BS: 13.60 ns). MIT license.

**What .NET versions does NepDate support?**
.NET Standard 2.0 - covers .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5 through 10, Xamarin, Unity, MAUI. `ISpanFormattable` is available when targeting .NET 6 or later; `IParsable<NepaliDate>` and `ISpanParsable<NepaliDate>` are available when targeting .NET 7 or later.

**Does NepDate have external dependencies?**
No. Zero external runtime dependencies. Newtonsoft.Json and System.Text.Json converters are internalized with `PrivateAssets=all` and do not surface in your dependency graph.

**Is NepDate thread-safe?**
Yes. `NepaliDate` is a `readonly struct`, making it inherently immutable. `BulkConvert` operations use thread-safe parallel processing automatically for 500+ items.

**How do I handle invalid BS dates in NepDate?**
Use `NepaliDate.TryParse` for non-throwing validation. Constructors and `NepaliDate.Parse` throw `InvalidNepaliDateFormatException` for unrecognized formats and `ArgumentOutOfRangeException` for out-of-range values. Each month's exact day count is enforced per year (29–32 days depending on month and year).

**Can NepDate be used with ASP.NET model binding?**
Yes. `NepaliDate` has a registered `TypeConverter` enabling automatic model binding in ASP.NET MVC/Web API when a query string or route parameter is in any recognized string format or YYYYMMDD integer format. No additional registration is needed.

**How is calendar metadata (Tithi, holidays) sourced?**
Compiled from authoritative Bikram Sambat calendar references and cross-verified for accuracy. Coverage: BS 2001 through 2089. Properties return empty/default values for dates outside this range without throwing.

**Where is the NepDate library source code?**
https://github.com/RajuPrasai/NepDate - MIT license. Issues and pull requests welcome.

# NepDate Widget - API Reference

Two machine-readable interfaces: the **NepDate .NET library** (`NuGet: NepDate`) and the **RunBox JSON config** (`shortcuts.json`, `scripts.json`). Both are described below with exact types, constraints, and examples.

**NepDate library version:** 2.0.7 | **NuGet:** https://www.nuget.org/packages/NepDate/ | **Source:** https://github.com/RajuPrasai/NepDate

---

## NepDate .NET Library

### NepaliDate struct

`NepaliDate` is a `readonly partial struct`. Single integer backing field (YYYYMMDD). Zero heap allocations on both conversion paths. Implements `IFormattable`, `IComparable<NepaliDate>`, `IEquatable<NepaliDate>`, `IParsable<NepaliDate>` (net7.0+), `ISpanFormattable` (net6.0+), `ISpanParsable<NepaliDate>` (net7.0+), and has a registered `TypeConverter`.

**Supported date range:** BS 1901/01/01 through BS 2199/12/last (approximately AD 1844-04-13 through AD 2143-04-12). Calendar metadata (tithi, holidays, events) is available only for BS 2001–2089; all metadata properties return empty defaults outside this range without throwing.

### Constructors

| Constructor | Description |
|---|---|
| `new NepaliDate(int year, int month, int day)` | From BS components. Throws `ArgumentOutOfRangeException` for out-of-range values. |
| `new NepaliDate(string input)` | Parses `"YYYY/MM/DD"` string format. Throws `InvalidNepaliDateFormatException` on failure. |
| `new NepaliDate(int yyyymmdd)` | From a YYYYMMDD integer (e.g. `20830215`). |
| `new NepaliDate(DateTime gregorian)` | Converts an AD `DateTime` to BS. |

**Extension method:** `DateTime.ToNepaliDate()` - equivalent to the DateTime constructor above.

### Static properties and parse methods

| Member | Kind | Return | Description |
|---|---|---|---|
| `NepaliDate.Today` | static property | `NepaliDate` | Current date per local system clock. |
| `NepaliDate.Now` | static property | `NepaliDate` | Same as `Today`. Parity with `DateTime.Now`. |
| `NepaliDate.MinValue` | static property | `NepaliDate` | 1 Baisakh 1901 BS. |
| `NepaliDate.MaxValue` | static property | `NepaliDate` | Last day of Chaitra 2199 BS. |
| `NepaliDate.Parse(string s)` | static method | `NepaliDate` | Parses `"YYYY/MM/DD"` or similar. Throws `InvalidNepaliDateFormatException`. For natural-language inputs use `SmartDateParser.Parse`. |
| `NepaliDate.TryParse(string s, out NepaliDate result)` | static method | `bool` | Non-throwing parse. |

**Smart parser:** `SmartDateParser.Parse(string input)` and `SmartDateParser.TryParse` handle 100+ format variants including Devanagari digits, month names, and ambiguous orderings. String extension: `"15 Jestha 2083".ToNepaliDate()`.

### Instance properties

| Property | Type | Description |
|---|---|---|
| `Year` | `int` | BS year (1901–2199). |
| `Month` | `int` | BS month (1–12). 1 = Baisakh, 12 = Chaitra. |
| `Day` | `int` | BS day (1 to `MonthEndDay`; range 29–32). |
| `EnglishDate` | `DateTime` | Equivalent Gregorian date. Time is always midnight. |
| `DayOfWeek` | `DayOfWeek` | Day of week (.NET enum). Saturday is Nepal's weekly holiday. |
| `DayOfYear` | `int` | 1-based ordinal within the BS calendar year. |
| `MonthEndDay` | `int` | Number of days in this month/year (29–32). |
| `MonthName` | `NepaliMonths` | Month as enum (e.g. `NepaliMonths.Jestha`). |
| `IsDefault` | `bool` | `true` when the instance is uninitialized (default struct). |
| `TithiNp` | `string` | Tithi in Nepali Devanagari. Empty string outside BS 2001–2089. |
| `TithiEn` | `string` | Tithi transliterated to English. Empty string outside BS 2001–2089. |
| `IsPublicHoliday` | `bool` | `true` if this date is a Nepal government gazetted holiday. `false` outside BS 2001–2089. |
| `EventsNp` | `string[]` | Event names in Nepali Devanagari. Empty array if none. |
| `EventsEn` | `string[]` | Event names in English. Empty array if none. |

**Efficient calendar lookup:** `date.GetCalendarInfo()` returns a `CalendarInfo` struct with all five calendar fields (`TithiNp`, `TithiEn`, `IsPublicHoliday`, `EventsNp`, `EventsEn`) in a single internal table access. Use this instead of reading individual properties when you need more than one.

### Arithmetic methods

| Method | Signature | Description |
|---|---|---|
| `AddDays` | `NepaliDate AddDays(double days)` | Returns new date `days` later. Negative moves backward. |
| `AddMonths` | `NepaliDate AddMonths(double months, bool awayFromMonthEnd = false)` | Adds whole or fractional months (~30.42 days/month). Day clamped to month end unless `awayFromMonthEnd: true`. |
| `AddYears` | `NepaliDate AddYears(int years, bool awayFromMonthEnd = false)` | Delegates to `AddMonths(years * 12)`. |
| `Subtract` | `TimeSpan Subtract(NepaliDate other)` | Elapsed time between two dates. Equivalent to the `b - a` operator. |

### Date difference

```csharp
NepaliDate a = new NepaliDate(2083, 1, 1);
NepaliDate b = new NepaliDate(2083, 6, 15);

TimeSpan diff = b - a;          // operator overload, returns TimeSpan
TimeSpan same = b.Subtract(a);  // method equivalent
int totalDays = (int)diff.TotalDays;
```

### Formatting

`NepaliDate` implements `IFormattable` and `ISpanFormattable`. Custom format tokens (case-sensitive, lowercase):

| Token | Output | Example (2083/02/15) |
|---|---|---|
| `yyyy` | 4-digit BS year | `2083` |
| `yy` | 2-digit BS year | `83` |
| `MM` | 2-digit zero-padded month | `02` |
| `M` | Month without padding | `2` |
| `MMMM` | Full English month name | `Jestha` |
| `MMM` | 3-letter abbreviation | `Jes` |
| `dd` | 2-digit zero-padded day | `15` |
| `d` | Day without padding | `15` |

**IFormattable specifiers:** `"d"` or `"G"` → `"2083/02/15"`, `"D"` → `"Jestha 15, 2083"`, `"s"` → `"2083-02-15"` (sortable, dash-separated).

```csharp
var bs = new NepaliDate(2083, 2, 15);

bs.ToString();                              // "2083/02/15"
bs.ToString("dd MMMM yyyy");               // "15 Jestha 2083"
bs.ToString("D");                          // "Jestha 15, 2083"
bs.ToString("s");                          // "2083-02-15"
$"{bs:yyyy-MM-dd}";                        // "2083-02-15" (interpolation)

// Unicode/Devanagari output - separate methods, not a ToString parameter:
bs.ToUnicodeString();                                         // "२०८३/०२/१५"
bs.ToUnicodeString(DateFormats.DayMonthYear, Separators.Dot); // "१५.०२.२०८३"
bs.ToLongDateUnicodeString();                                 // "जेठ १५, २०८३"
bs.ToLongDateString();                                        // "Jestha 15, 2083"
```

### Fiscal year operations

Nepal fiscal year runs Shrawan 1 (month 4) to the last day of Ashadh (month 3) of the following calendar year.

Quarter mapping:

| Quarter | Enum value | BS months | Month numbers |
|---|---|---|---|
| Q1 (First) | `FiscalYearQuarters.First = 4` | Shrawan to Ashoj | 4–6 |
| Q2 (Second) | `FiscalYearQuarters.Second = 7` | Kartik to Poush | 7–9 |
| Q3 (Third) | `FiscalYearQuarters.Third = 10` | Magh to Chaitra | 10–12 |
| Q4 (Fourth) | `FiscalYearQuarters.Fourth = 1` | Baishakh to Ashadh | 1–3 |

```csharp
var bs = new NepaliDate(2083, 2, 15);  // Jestha 15, 2083 → Q4 of FY 2082/83

// Instance methods (method calls, not properties):
NepaliDate fyStart = bs.FiscalYearStartDate();         // 2082/04/01 (Shrawan 1, 2082)
NepaliDate fyEnd   = bs.FiscalYearEndDate();           // 2083/03/last
NepaliDate qStart  = bs.FiscalYearQuarterStartDate();  // 2083/01/01 (Q4 start)
NepaliDate qEnd    = bs.FiscalYearQuarterEndDate();    // 2083/03/last (Q4 end)

// Next fiscal year boundaries:
NepaliDate nextStart = bs.FiscalYearStartDate(yearOffset: 1);  // 2083/04/01

// Static methods:
NepaliDate s = NepaliDate.GetFiscalYearStartDate(2082);         // 2082/04/01
NepaliDate e = NepaliDate.GetFiscalYearEndDate(2082);           // 2083/03/last
var (qs, qe) = NepaliDate.GetFiscalYearQuarterStartAndEndDate(2082, month: 1); // Q4 of FY2082
```

### Date range operations

```csharp
var range = new NepaliDateRange(new NepaliDate(2083, 1, 1), new NepaliDate(2083, 3, 31));

range.Length;                // int - total days in range (not TotalDays)
range.IsEmpty;               // bool
range.Contains(someDate);    // bool
range.Overlaps(otherRange);  // bool
range.Intersect(other);      // NepaliDateRange (empty range if no overlap)
range.Union(other);          // NepaliDateRange
range.Except(other);         // NepaliDateRange[] (0, 1, or 2 segments)
range.SplitByMonth();        // NepaliDateRange[]
range.SplitByFiscalQuarter();// NepaliDateRange[]
range.WorkingDays();         // IEnumerable<NepaliDate> (excludes Saturdays; pass excludeSunday: true to also exclude Sundays)

foreach (NepaliDate d in range) { }  // range is enumerable
```

Factory methods: `NepaliDateRange.ForMonth(year, month)`, `NepaliDateRange.ForFiscalYear(year)`, `NepaliDateRange.ForCalendarYear(year)`, `NepaliDateRange.CurrentMonth()`, `NepaliDateRange.CurrentFiscalYear()`, `NepaliDateRange.FromDayCount(start, days)`.

### Bulk conversion

```csharp
// Automatically uses parallel processing for collections exceeding 500 items:
IEnumerable<NepaliDate> bsDates = NepaliDate.BulkConvert.ToNepaliDates(manyGregorianDates);
IEnumerable<DateTime>   adDates = NepaliDate.BulkConvert.ToEnglishDates(manyBsDates);

// Also accepts string collections:
IEnumerable<DateTime> adDates2 = NepaliDate.BulkConvert.ToEnglishDates(manyBsStrings);

// Explicit batch size:
IEnumerable<NepaliDate> batched = NepaliDate.BulkConvert.BatchProcessToNepaliDates(engDates, batchSize: 2000);
```

### Serialization

**System.Text.Json:** `[JsonConverter]` is auto-registered on .NET 5+. Default serialized format is `"YYYY-MM-DD"` (dash separator). Use `ConfigureForNepaliDate()` to select object format or to opt into Newtonsoft.Json support.

```csharp
using NepDate.Serialization;

// Default (string format, dash separator):
var opts = new JsonSerializerOptions().ConfigureForNepaliDate();
string json = JsonSerializer.Serialize(new NepaliDate(2083, 2, 15), opts); // "2083-02-15"

// Object format: {"Year":2083,"Month":2,"Day":15}
var optsObj = new JsonSerializerOptions().ConfigureForNepaliDate(useObjectFormat: true);
```

**Newtonsoft.Json:** `new JsonSerializerSettings().ConfigureForNepaliDate()` - same string/object modes.

**XML:** Wrap in `NepaliDateXmlSerializer` (required because `NepaliDate` is a struct without a parameterless constructor). See library docs for example.

### Parsing accepted formats

`NepaliDate.Parse` and `NepaliDate.TryParse` accept standard `"YYYY/MM/DD"` strings and common variants. For natural language and Devanagari input, use `SmartDateParser.Parse` (or the `string.ToNepaliDate()` extension), which handles 100+ format variants:

| Input | Parsed as |
|---|---|
| `"2083/02/15"` | Year 2083, Month 2, Day 15 |
| `"2083-02-15"` | Year 2083, Month 2, Day 15 |
| `"२०८३/०२/१५"` | Same (Devanagari digits) |
| `"2083 Jestha 15"` | Same (English month name) |
| `"2083 Jeth 15"` | Same (short variant) |
| `"15 Jestha 2083"` | Same (day-first) |
| `"१५ जेठ २०८३"` | Same (full Nepali) |

Integer input uses the constructor: `new NepaliDate(20830215)` (not `Parse`).

---

## RunBox JSON Config

RunBox reads two user-editable files from the `config/` subdirectory of the app data folder.

### Data folder paths

| Install mode | Base path |
|---|---|
| Microsoft Store | `%LOCALAPPDATA%\Packages\{PackageFamilyName}\LocalCache\Local\NepDateWidget.Store\AppData\` |
| Direct .msix / Dev | `%LOCALAPPDATA%\NepDateWidget\AppData\` |
| Portable | `{exe directory}\AppData\` |

Config files are at `{base path}\config\`. Open directly from within the app: Settings → Data Files → Open Config Folder.

### shortcuts.json

Defines RunBox web search prefixes. User entries are merged with the 22 built-ins; user entries win on key collision.

**Schema:** JSON array of objects.

```json
[
  { "Key": "shop", "Url": "https://example.com/search?q={query}", "Name": "My Shop" },
  { "Key": "yt2",  "Url": "https://youtube.com/results?search_query={query}&sp=EgIQAQ%3D%3D", "Name": "YouTube Videos" },
  { "Key": "fb",   "Disabled": true }
]
```

**Object fields:**

| Field | Type | Required | Constraints |
|---|---|---|---|
| `Key` | `string` | Yes | Letters and digits only. Case-insensitive. Max one entry per key (last write wins). |
| `Url` | `string` | Yes (unless `Disabled`) | Must contain exactly one `{query}` placeholder. May also contain `{year}` (replaced with current calendar year at invocation). |
| `Name` | `string` | Yes (unless `Disabled`) | Display name shown in the `"Search {Name}..."` hint label. |
| `Disabled` | `bool` | No | Set to `true` to suppress the built-in with this `Key` without providing a replacement URL. |

**Built-in prefix keys (22):**

| Key | Site |
|---|---|
| `g` | Google |
| `yt` | YouTube |
| `gh` | GitHub |
| `pp` | Perplexity |
| `rd` | Reddit |
| `tw` | X / Twitter |
| `wp` | Wikipedia |
| `li` | LinkedIn |
| `so` | Stack Overflow |
| `im` | Google Images |
| `map` | Google Maps |
| `fb` | Facebook |
| `tr` | Google Translate |
| `ig` | Instagram |
| `dz` | Daraz Nepal |
| `hb` | HamroBazaar |
| `ek` | eKantipur (uses `{year}` token) |
| `ok` | OnlineKhabar |
| `mj` | MeroJob |
| `ss` | ShareSansar |
| `gpt` | ChatGPT |
| `cl` | Claude |

Hot-reload: changes take effect immediately without restarting the app. Invalid entries are silently skipped (logged to `nepdate.log`).

### scripts.json

Defines RunBox custom script commands. Invoke from RunBox by typing `scr ` followed by the script name. Scripts run in their own terminal window.

**Schema:** JSON array of objects.

```json
[
  {
    "Name": "cleanup",
    "Description": "Delete temp files in Downloads",
    "Path": "C:\\Scripts\\cleanup.ps1",
    "Interpreter": "powershell"
  },
  {
    "Name": "deploy",
    "Description": "Run deployment script",
    "Path": "{APPDIR}\\scripts\\deploy.sh",
    "Interpreter": "wsl"
  }
]
```

**Object fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `Name` | `string` | Yes | Keyword used to find the script in RunBox. Matched case-insensitively against the query after `scr `. |
| `Description` | `string` | No | Shown in the suggestion list below the script name. |
| `Path` | `string` | Yes | Absolute path to the script file. `{APPDIR}` is replaced at runtime with the directory containing `NepDateWidget.exe`. |
| `Interpreter` | `string` | Yes | One of: `powershell`, `pwsh`, `cmd`, `python`, `wsl`. Determines how the script is executed. |

**Interpreter behavior:**

| Value | Launched as |
|---|---|
| `powershell` | `powershell.exe -File "{path}" {args}` in a new window |
| `pwsh` | `pwsh.exe -File "{path}" {args}` in a new window (PowerShell 7+) |
| `cmd` | `cmd.exe /K "{path}" {args}` in a new window |
| `python` | `python.exe "{path}" {args}` in a new window |
| `wsl` | `wsl.exe bash "{wsl_path}" {args}` - Windows path is converted to `/mnt/` mount path automatically |

---

## App Data File Schemas

All user data is stored as plain JSON. Files are written atomically (write to `.tmp`, then replace). Back up by copying the entire `AppData\` folder.

| File | Path | Content |
|---|---|---|
| `settings.json` | `config/settings.json` | App settings (theme, font, language, calendar toggles, hotkey) |
| `localization.json` | `config/localization.json` | UI string overrides for the selected language |
| `shortcuts.json` | `config/shortcuts.json` | User RunBox search prefix overrides (see above) |
| `scripts.json` | `config/scripts.json` | User RunBox script commands (see above) |
| `notes.json` | `data/notes.json` | Notes attached to BS calendar dates, keyed by `"YYYY/MM/DD"` |
| `reminders.json` | `data/reminders.json` | Recurring and one-time reminder entries |
| `documents.json` | `data/documents.json` | Pinned document/folder/executable shortcuts |
| `run-history.json` | `data/run-history.json` | RunBox command history |
| `runtime.json` | `runtime.json` | Window position, last session state (not user-edited) |
| `nepdate.log` | `nepdate.log` | Operational log (rotated; not user-edited) |

# NepDate Widget Site

Teaches agents how to navigate and extract information from https://nepdatewidget.rajuprasai.com.np, the companion website for NepDate Widget - a free Nepali date converter and calendar desktop application for Windows.

## What this site covers

The site documents a Windows desktop application (MSIX, distributed via Microsoft Store) that provides:
- Bikram Sambat (BS) to Gregorian (AD) and AD to BS date conversion
- Nepali calendar with tithi, public holidays, and fiscal year
- Preeti to Unicode and Unicode to Preeti text conversion
- Loan EMI calculator with amortization schedule
- Land and weight unit converter (Nepali traditional units)
- Network toolkit (ping, traceroute, IP scan, Whois, DNS)
- RunBox global command launcher
- Notes and reminders pinned to BS dates
- Document manager, ID photo tool, batch image compression

## Pages

| URL | Content |
|---|---|
| / | Homepage with hero, feature grid, code demo, download |
| /features | Full feature list with screenshots |
| /bikram-sambat | Explanation of the Bikram Sambat calendar system |
| /download | Download and installation instructions |
| /changelog | Version history |
| /nepali-calendar-2082 | Full 2082 BS calendar reference |
| /nepali-calendar-2083 | Full 2083 BS calendar reference |
| /nepali-calendar-2084 | Full 2084 BS calendar reference |
| /docs/api-reference | NepDate .NET library API reference |
| /docs/integration | Integration guide for developers |
| /docs/faq | Frequently asked questions |

## Key facts for agents

- App is free, offline, and Windows-only (Windows 10 1803+)
- Single external dependency: NepDate NuGet package (nepali date math)
- BS date range supported: 1901/01/01 through 2199/12/last
- Calendar metadata (tithi, holidays) available for BS 2001-2089
- App language: English and Nepali (Devanagari)
- No account or internet required after install

## How to find download links

The /download page lists the Microsoft Store link and direct MSIX package links for x64 and ARM64 architectures.

## NepDate .NET library

The site also documents the NepDate open-source .NET library used by the app. Full API reference at /docs/api-reference. NuGet: https://www.nuget.org/packages/NepDate/

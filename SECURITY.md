# Security Policy

## Supported versions

| Version | Supported          |
|---------|--------------------|
| 1.0.x   | Yes                |

Security fixes are issued only against the latest published
release. Older builds will not receive backports.

## Reporting a vulnerability

**Do not open a public GitHub issue for security problems.**

Report privately via GitHub Security Advisories:
<https://github.com/RajuPrasai/NepDateWidget/security/advisories/new>

Please include:

1. **Affected version** - visible in **Settings → About**
2. **Vulnerability type** - e.g. Path Traversal, Command Injection, URL Hijack
3. **Steps to reproduce** - clear instructions or a minimal proof of concept
4. **Impact** - what an attacker could achieve if the flaw is exploited
5. **OS and locale** - if relevant (this application handles locale-sensitive calendar data)

## What to expect

- Acknowledgement within 5 working days
- A first-pass triage and severity assessment within 10 working days
- A fix or mitigation plan communicated before any public disclosure
- Credit in the release notes if you wish

## Scope

In scope:

- Code in this repository (`src/`, `tests/`, build scripts, GitHub Actions)
- Local data files where unsafe deserialization or path-traversal is possible:
  `settings.json`, `reminders.json`, `notes.json`, `documents.json`,
  `run-history.json`, `runtime.json`, `localization.json`, `nepdate.log`
- `scripts.json` - user-defined scripts executed by RunBox via `Process.Start`;
  a crafted file could trigger arbitrary command execution
- `shortcuts.json` - user-defined URL prefix shortcuts opened via the default
  browser handler; a crafted entry could direct users to unintended URLs
- Network requests from the Network Tools tab (outbound DNS, ICMP, HTTP/S, WHOIS
  against user-supplied hosts; public IP lookup via `api.ipify.org`)

Out of scope:

- Vulnerabilities in third-party dependencies that are already publicly
  tracked upstream - report those to the upstream maintainers and link
  the advisory here so the dependency can be upgraded
- Issues that require physical access to an unlocked machine
- Social engineering or phishing
- Theoretical vulnerabilities without a reproducible proof of concept

Note: Dependabot is configured and monitors NuGet and GitHub Actions
dependencies monthly. If a CVE affects a dependency used here and the
project has not yet upgraded to a safe version, that **is** in scope -
knowingly shipping a vulnerable dependency version is a valid report.

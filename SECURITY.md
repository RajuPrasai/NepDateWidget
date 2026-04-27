# Security Policy

## Supported versions

| Version | Supported          |
|---------|--------------------|
| 1.0.x   | Yes                |

Security fixes are issued only against the latest published
release. Older builds will not receive backports.

## Reporting a vulnerability

**Do not open a public GitHub issue for security problems.**

Report privately via:

- GitHub Security Advisories: <https://github.com/RajuPrasai/NepDateWidget/security/advisories/new>

Please include:

- Affected version (visible in **Settings → About**)
- A description of the issue and its impact
- Steps to reproduce, or a minimal proof of concept
- Your operating system and locale, if relevant

## What to expect

- Acknowledgement within 5 working days
- A first-pass triage and severity assessment within 10 working days
- A fix or mitigation plan communicated before any public disclosure
- Credit in the release notes if you wish

## Scope

In scope:

- Code in this repository (`src/`, `tests/`, build scripts, GitHub Actions)
- The auto-update flow (Velopack feed, signature handling, channel resolution)
- Local data files (`settings.json`, `reminders.json`, `notes.json`,
  `nepdate.log`) where unsafe deserialization or path-traversal is possible

Out of scope:

- Vulnerabilities in third-party dependencies that are already publicly
  tracked upstream (please report those to the maintainers of those
  projects instead, and link the advisory here)
- Issues that require physical access to an unlocked machine
- Social engineering or phishing

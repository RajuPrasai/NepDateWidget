# Contributing to NepDate Widget

Thank you for your interest. Before writing any code, please read this
file in full. The licensing model is **source-available**, not
traditional open source, and that affects what contributions are
accepted and how they are used.

## Licensing of contributions

This project is distributed under the **PolyForm Strict License 1.0.0**
(see [LICENSE](LICENSE)). It is source-available: you may read and
audit the code, but redistribution and derivative or competing products
are not permitted.

By submitting a pull request, issue containing a patch, or any other
contribution, you agree that:

1. You wrote the contribution yourself, or have explicit permission
   from the copyright holder to submit it.
2. You assign copyright in your contribution to the project owner
   (RajuPrasai), or grant a perpetual, worldwide, royalty-free,
   irrevocable license to use, modify, sublicense, and relicense your
   contribution as part of this project, including under future
   licenses chosen by the project owner.
3. You understand that the project may be relicensed in future and that
   your contribution may be redistributed under those terms.

If you cannot agree to the above, please do not submit code. Bug
reports and design feedback in plain-text issues are always welcome
and are not affected by this clause.

## Reporting bugs

Open an issue with:

- A short, specific title
- App version (Settings → About)
- Windows version and build (`winver`)
- Steps to reproduce
- What you expected vs. what happened
- A snippet from `nepdate.log` if relevant (located in
  `%LOCALAPPDATA%\NepDateWidget.Store\AppData\` for Store builds,
  `%LOCALAPPDATA%\NepDateWidget\AppData\` for unpackaged/installed builds,
  or `AppData\` beside the EXE for portable builds)

For security issues, follow [SECURITY.md](SECURITY.md) instead.

## Suggesting features

Open an issue with the `enhancement` label. Describe the problem you
are trying to solve, not just the solution you have in mind. Small,
focused proposals are easier to discuss than large ones.

## Pull requests

1. Open an issue first for anything non-trivial. Avoid spending time on
   a PR that may not align with the project direction.
2. Fork, branch from `main`, keep the change focused on one concern.
3. Match the existing coding style. The project uses nullable
   reference types, file-scoped namespaces, and standard .NET naming.
4. Add or update tests for any logic change. Run the full suite:
   `dotnet test tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj`
5. Do not commit binaries, build outputs, signing keys, or PFX files.
6. Write clear commit messages: `area: short summary` (for example,
   `calendar: fix month navigation on DST boundary`).

## Coding conventions

- C#: nullable enabled, `var` only when the type is obvious from the
  right-hand side, expression-bodied members where they improve
  readability.
- WPF: keep code-behind for view-specific concerns only. Business logic
  goes in the relevant ViewModel or Service.
- Async: use `ConfigureAwait(false)` in non-UI code paths. Do not block
  on async with `.Result` or `.Wait()`.
- Logging: use `Log.Info`, `Log.Action`, `Log.Warn`, `Log.Error`,
  `Log.Fatal`. Include enough context that the entry is meaningful in
  isolation.
- Exception handling: do not swallow exceptions silently. If you must
  catch broadly (for example, to keep the app alive), log at
  `Warn` or `Error` with the full exception.

## Code of conduct

By participating you agree to abide by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

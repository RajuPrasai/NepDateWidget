<!-- Thanks for the contribution. By submitting this PR you agree to the
terms in CONTRIBUTING.md. -->

## What this PR does

<!-- One or two sentences. -->

## Why

<!-- The problem being solved or the motivation for the change. Link the
related issue if there is one (Closes #123). -->

## How it was tested

- [ ] `dotnet build src/NepDateWidget/NepDateWidget.csproj -c Release`
- [ ] `dotnet test tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj -c Release`
- [ ] Manual smoke test on Windows: <!-- describe what you exercised -->

## Notes for the reviewer

<!-- Anything that is not obvious from the diff. Edge cases, design
choices, follow-ups deferred to a later PR. -->

## Checklist

- [ ] Code follows the conventions in CONTRIBUTING.md (nullable enabled,
      `Log.*` for logging, `ConfigureAwait(false)` in non-UI async)
- [ ] Tests added or updated for any logic change
- [ ] No binaries, build outputs, or signing material committed
- [ ] User-facing change? README and (if applicable) the docs folder updated

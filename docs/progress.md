# Progress

Running log of Milestone 1 ("extraction core + diagnostics + CI"), kept up
to date with every commit so work can resume from here if the chat's
context runs out. Decisions worth reviewing are in `docs/decisions.md`.

**Milestone 1 end goal:** `dotnet run --project backend/src/Plannr.Eval -- extract <url>`
prints a validated card with uncertainty flags; failures print an error
reference findable in the log; GitHub Actions runs the tests on every push.

## Done

| Change | Commit | What |
|---|---|---|
| 1 + 2 | `c356b0f` | Solution skeleton, `Directory.Build.props`, `.gitignore`; `ErrorReference`, `PlannrLogging`, `AppOptionsBase`, `GetValidated` |
| 2b | `a59618d` | Minimal `Plannr.Api`: exception middleware, `ApiErrorResponse`, `/health`, `.gitattributes` |
| 3 | `eeaf84b` | Core domain: `SourceType`, `RetrievedContent`, `LlmCardFields`, `CardDraft`, `FieldFlag`, interfaces, `CaptureException` family |
| 4a | `3e4ed0a` | `LinkClassifier`, `InstagramNotSupportedException` |
| 4b | `3913f7d` | `HtmlContentParser` (OG, Event + Place JSON-LD, body text), `RetrievalOptions` |
| 4c | `79ca854` | `HttpPageFetcher`, `PublicAddressGuard` (SSRF), `WebPageRetriever`, `AddPlannrRetrieval` |
| 4d | `2a114ce` | `PlaywrightPageFetcher` + JS-shell fallback |

## In progress / left

- [ ] 5 Groq extractor
- [ ] 6 JSON-LD mapper, normaliser, pipeline
- [ ] 7 Eval host and commands
- [ ] 8 Fixture-based tests
- [ ] 9 CI workflow, **to be done together** (not started on purpose)

## Needs you

- **Groq key:** revoke the old one (see D13), create a new one, then from `backend/src/Plannr.Eval`:
  `dotnet user-secrets set "Groq:ApiKey" "<your new key>"` (available once change 7 adds the UserSecretsId).
- **Push:** nothing pushed yet; `origin` = `https://github.com/SunchokePasta/Plannr.git`. Planned for change 9.

## Machine setup notes

- .NET SDK 10.0.400.
- Playwright Chromium installed in `%LOCALAPPDATA%\ms-playwright` (317 MB). Reinstall:
  `powershell backend/src/Plannr.Eval/bin/Debug/net10.0/playwright.ps1 install chromium`
  (must be an executable project's output; a class library's bin has no Playwright DLL).
- Only Windows PowerShell 5.1 is installed (no `pwsh`); the Playwright script works with it.
- Tests: `dotnet test backend` runs everything; `--filter "Category!=Integration"` skips the browser tests.

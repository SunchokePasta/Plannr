# Progress

Running log of Milestone 1 ("extraction core + diagnostics + CI"), kept up
to date with every commit so work can resume from here if the chat's
context runs out. Decisions worth reviewing are in `docs/decisions.md`.

**Milestone 1 end goal:** `dotnet run --project backend/src/Plannr.Eval -- extract <url>`
prints a validated card with uncertainty flags; failures print an error
reference findable in the log; GitHub Actions runs the tests on every push.

**State on 21 Oct 2026:** changes 1-8 (+5e) done and committed (nothing pushed). 298 tests pass
(293 unit + 5 real-Chromium integration). Change 9 (CI) in progress, built together: 9a done.

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
| 5a | `3a2fd34` | `GroqOptions`, Groq chat DTOs (incl. image parts), Refit `IGroqApi`; these docs |
| 5b | `20abd8b` | `ExtractionPrompt`, `ActivityCategories`; `RetrievedContent.SourceUrl` nullable |
| 5c | `4e6b9cd` | `GroqLlmExtractor` (429/json_validate_failed/401 handling), `LlmAnswerParser`, `UkTime` |
| 5d | `8cb1150` | `AddPlannrLlm` with Refit 16 generated client; test through real Refit + stub |
| 6a | `c74a2ab` | `JsonLdEventMapper`, `UkDateParser`, `PriceParser`; `CardDraft` reporting fields |
| 6b | `313a922` | `CardNormaliser`, `ExtractionOptions` |
| 6c | `fd28f9d` | `ExtractionPipeline`, `IExtractionPipeline`, `ImageSignature`, `UnsupportedImageException`, `AddPlannrCapture` |
| 7a | `9b88be5` | Eval host, error boundary (references, exit codes), user-secrets, `EvalArguments` |
| 7b | `ad09fcf` | `extract <url>` / `extract --image`; address-repeat and log-format fixes found on a live page |
| 7c | `8cee99b` | `models` command; root `README.md` |
| 8a | `fa94a1d` | Eight synthetic fixture pages + parser/mapper tests over them |
| 8b | `3b49bb3` | Whole-pipeline tests over the fixtures (fake network + LLM only) |
| 9a | `b834426` | Pre-push clean-up for the public repo (D13 rewritten, fake key renamed) |
| 5e | (this commit) | Groq waits brought up to Applr's JobAnalyser rules; Eval waits up to 15 min (D29) |

**Verified live (21 Oct):** `extract` on a real Eventbrite event produced a correct card in ~0.6 s
from JSON-LD alone (Groq skipped, dummy key). The no-key error path prints the setting name and
a reference.

## Left in Milestone 1

- [ ] **9 CI workflow, to be done together** (not started on purpose). Points to cover:
  - `dotnet test backend --filter "Category!=Integration"` as the fast gate;
  - a second job/step that installs Chromium (`playwright.ps1 install --with-deps chromium`, which needs `pwsh` on the runner) and runs `--filter Category=Integration`;
  - unit tests must never reach the network (see D17); consider enforcing it;
  - push to `origin` for the first time.

## Not verified yet

- **Groq for real.** Needs your key; then `models` to choose `Groq:VisionModel`, and `extract` on a page without complete JSON-LD (e.g. a blog post) and on a screenshot.
- **The Playwright fallback on a real JavaScript-only site.** Only tested against a local test page.

## Needs you

- **Groq key:** from `backend/src/Plannr.Eval`:
  `dotnet user-secrets set "Groq:ApiKey" "<your key>"` (see D13: never in a tracked file).
- **Review** `docs/decisions.md` (entries marked `review`).
- **Push:** nothing pushed yet; `origin` = `https://github.com/SunchokePasta/Plannr.git`. Planned for change 9.

## Ideas noticed along the way (not started)

- **Cancelled/postponed events:** JSON-LD has `eventStatus` (`EventCancelled`, `EventPostponed`); the mapper ignores it. Flagging those would stop users planning for a cancelled event.
- **List pages** (D26): offer a choice of activities.
- **Eventbrite `lowPrice: "0.0"`** (D28): check in the data pool.
- **Milestone 2 (data pool):** an Eval `snapshot` command saving pages privately, a labels file, and a `score` command for G3 per link type, reporting LLM failures and browser fallbacks separately.

## Machine setup notes

- .NET SDK 10.0.400.
- Playwright Chromium installed in `%LOCALAPPDATA%\ms-playwright` (317 MB). Reinstall:
  `powershell backend/src/Plannr.Eval/bin/Debug/net10.0/playwright.ps1 install chromium`
  (must be an executable project's output; a class library's bin has no Playwright DLL).
- Only Windows PowerShell 5.1 is installed (no `pwsh`); the Playwright script works with it.
- Tests: `dotnet test backend` runs everything; `--filter "Category!=Integration"` skips the browser tests.

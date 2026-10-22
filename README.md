# Plannr

An LLM-assisted mobile app that turns links and screenshots people share
(event pages, TikToks, screenshots) into activity cards friends can agree
a date for. CS310 project.

**Status:** Milestone 1, the extraction core. See [`docs/progress.md`](docs/progress.md)
for what's done and what's left, and [`docs/decisions.md`](docs/decisions.md)
for design choices awaiting review.

## Layout

```
backend/
  Directory.Build.props    shared build settings (net10.0, nullable, warnings as errors)
  Plannr.slnx
  src/Plannr.Core          domain: models, interfaces, ExtractionPipeline, JSON-LD mapper, normaliser
  src/Plannr.Infrastructure page fetching (HTTP + Playwright), Groq client, logging setup, DI wiring
  src/Plannr.Api           ASP.NET Core API (so far: exception middleware and /health)
  src/Plannr.Eval          console tool: run extraction on a link or screenshot
  tests/Plannr.Tests       xUnit
docs/                      progress and decision logs
```

## Setup (once)

1. **.NET SDK 10** (`dotnet --list-sdks`).
2. **Chromium for Playwright** (used only for pages that need JavaScript). Build once, then:
   ```
   dotnet build backend
   powershell backend/src/Plannr.Eval/bin/Debug/net10.0/playwright.ps1 install chromium
   ```
   It must be an executable project's `bin` folder; a class library's has no Playwright DLL.
   Without Chromium, set `Retrieval:BrowserFallbackEnabled` to `false`.
3. **Groq API key**, kept out of the repo with user-secrets:
   ```
   cd backend/src/Plannr.Eval
   dotnet user-secrets set "Groq:ApiKey" "gsk_..."   # the key itself in the quotes, no < >
   ```
4. Check which models the key can use, and set `Groq:VisionModel` in
   `backend/src/Plannr.Eval/appsettings.json` to one that accepts images:
   ```
   dotnet run --project backend/src/Plannr.Eval -- models
   ```

## Running

```
dotnet run --project backend/src/Plannr.Eval -- extract https://www.eventbrite.co.uk/e/...
dotnet run --project backend/src/Plannr.Eval -- extract --image screenshot.png
dotnet run --project backend/src/Plannr.Eval -- extract <url> --verbose   # includes the Groq prompt
```

The card is printed as JSON on stdout and the log on stderr. Every run's log is also in
`backend/src/Plannr.Eval/bin/Debug/net10.0/logs/log-yyyyMMdd.txt`. A failure prints a
reference such as `7f3a9c21:03:01`, which is on the matching log line. The first part is a
unique id; `03` means Plannr.Eval and `01` its log file (see `ErrorReference`).

The API: `dotnet run --project backend/src/Plannr.Api`, then `GET http://localhost:5043/health`.

## Tests

```
dotnet test backend                                      # everything
dotnet test backend --filter "Category!=Integration"     # skip the real-Chromium tests
```

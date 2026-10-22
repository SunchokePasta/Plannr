# Progress

Running log of the build, kept current with every commit so work can
resume from here if the chat's context runs out. Choices to review:
[`decisions.md`](decisions.md). Everything planned, file by file:
[`structure.json`](structure.json) (overview in [`structure.md`](structure.md)).

**State on 22 Oct 2026:** Milestone 1 is complete and pushed to
`github.com/SunchokePasta/Plannr` (public). CI is green: build → unit tests →
integration tests → deploy placeholder. 299 unit tests + 5 real-Chromium tests.
The rest of the project is laid out as placeholders (`PLANNED` stubs) tracked
by `structure.json`.

## Deadlines (spec, with your amendment)

| Date | What |
|---|---|
| 6 Nov 2026 | P2 requirements; **P3 data pool: 100 links labelled** |
| 10 Dec 2026 | **P4 progress report** with proof-of-concept accuracy (moved from 10 Nov) |
| 11 Dec 2026 | **P5 MVP**: all Musts pass their tests; share → card → group → confirmed date works on Android; **G3** |
| 19 Feb 2027 | P6 user trial (7+ participants) and G1-G4 analysed |
| 8 Apr 2027 | Final reports |

## Done

| Change | Commit | What |
|---|---|---|
| 1 + 2 | `c356b0f` | Solution skeleton, `Directory.Build.props`, `.gitignore`; `ErrorReference`, `PlannrLogging`, `AppOptionsBase`, `GetValidated` |
| 2b | `a59618d` | Minimal `Plannr.Api`: exception middleware, `ApiErrorResponse`, `/health`, `.gitattributes` |
| 3 | `eeaf84b` | Core domain: `SourceType`, `RetrievedContent`, `LlmCardFields`, `CardDraft`, `FieldFlag`, interfaces, `CaptureException` family |
| 4a-4d | `3e4ed0a`..`2a114ce` | `LinkClassifier`, `HtmlContentParser`, HTTP fetching + SSRF guard, `WebPageRetriever`, Playwright fallback |
| 5a-5d | `3a2fd34`..`8cb1150` | Groq options/DTOs/Refit client, shared prompt, `GroqLlmExtractor`, `AddPlannrLlm` |
| 6a-6c | `c74a2ab`..`fd28f9d` | `JsonLdEventMapper`, date/price parsers, `CardNormaliser`, `ExtractionPipeline`, `AddPlannrCapture` |
| 7a-7c | `9b88be5`..`8cee99b` | Eval: host and error boundary, `extract`, `models`; README |
| 8a-8b | `fa94a1d`, `3b49bb3` | Synthetic fixture pages; parser/mapper and whole-pipeline tests over them |
| 9a | `b834426` | Pre-push clean-up for the public repo |
| 5e | `5755d2a` | Groq waits/retries brought up to Applr's JobAnalyser rules; Eval waits up to 15 min |
| 9b | `f142d87` | NuGet lock files; SDK pinned in `backend/global.json` |
| 9c | `9736316` | `.github/workflows/ci.yml`: build → unit → integration → deploy placeholder |
| 9d | (push) | First push; run `39766496866` green in ~3 min |
| 10a-10c | `4fc47be`, `5bb230c`, (this commit) | `structure.json` + `StructureManifestTests`; placeholders for backend, data pool, mobile, infra; `structure.md` |

## What's left, in order

Milestone numbers match `structure.json`. From today there are about 7
weeks to the 10-11 Dec deadlines for M2-M8, which is tight: M2 has to
come first (the data pool is due 6 Nov), and M3-M7 overlap the data-pool
labelling.

| Milestone | What | Needed for |
|---|---|---|
| **M2** | Data pool tooling in Eval: `snapshot` (save pages, which expire), a labels file, `score` (G3 per link type; LLM failures and browser fallbacks reported separately) | P3 (6 Nov), P4, G3 |
| **M3** | API skeleton: EF Core + MySQL (docker-compose), Identity + JWT, `ActivityCard` + flags, `/cards`, OpenAPI document; middleware 404/403/409 arms | Everything after |
| **M4** | Capture loop: `POST /captures` → Captures table (job queue) → **Plannr.Worker** → card → SignalR; rate limiting and caching (D10); Worker error boundary | The share → card flow |
| **M5** | Mobile shell: Expo dev build (EAS), login, generated API client, wishlist, card view/edit, **share intent** on your phone | The flow on Android |
| **M6** | TikTok (oEmbed, then location tag via Playwright); screenshots end to end from the app | TikTok + screenshot Musts |
| **M7** | Groups, invites (deep link), plans, yes/no responses, confirm, push notifications | Group-planning Musts |
| **M8** | Test pass, G3 on the 70-link held-out set, MVP | P5, G3 (11 Dec) |
| **T2** | Should features: link in bio, YouTube, date ranges, auto date selection, reminders, calendar, search; **AWS deploy** (fills the CI deploy job) | Trial (P6) |

## Not verified yet

- **Groq for real.** Needs your key; then `models` to choose `Groq:VisionModel`, and `extract` on a page without complete JSON-LD (e.g. a blog post) and on a screenshot.
- **The Playwright fallback on a real JavaScript-only site.** Tested against a local page, locally and in CI.

## Needs you

- **Groq key:** from `backend/src/Plannr.Eval`: `dotnet user-secrets set "Groq:ApiKey" "<your key>"` (D13).
- **Branch protection (9e):** GitHub → Settings → Branches → add a rule for `main` requiring the CI checks. Only you can change repo settings.
- **Review** `decisions.md` (entries marked `review` or `question`) and the open questions in `structure.json`.
- **Applr (separate repo):** its `appsettings.Development.json` is tracked by git and now holds a key as an uncommitted change. Untrack it (`git rm --cached`, plus a `.gitignore` rule) before your next Applr commit.

## Ideas noticed along the way (not started)

- **Cancelled/postponed events:** JSON-LD has `eventStatus`; the mapper ignores it. Flagging those stops users planning for a cancelled event.
- **List pages** (D26): offer a choice of activities.
- **Eventbrite `lowPrice: "0.0"`** (D28): check in the data pool.
- **CI extras:** block network for unit tests (D17); pin actions to commit SHAs + Dependabot; coverage; a Windows matrix job.

## Machine setup notes

- .NET SDK 10.0.400 (pinned by `backend/global.json`).
- Playwright Chromium in `%LOCALAPPDATA%\ms-playwright` (317 MB). Reinstall:
  `powershell backend/src/Plannr.Eval/bin/Debug/net10.0/playwright.ps1 install chromium`
  (an executable project's output; a class library's bin has no Playwright DLL).
- Only Windows PowerShell 5.1 is installed (no `pwsh`); the Playwright script works with it.
- Tests: `dotnet test backend` runs everything; `--filter "Category!=Integration"` skips the browser tests.
- No `gh` CLI: CI results are read from GitHub's public API or the Actions tab.

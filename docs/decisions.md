# Decisions to review

Choices that go beyond the spec or the agreed plan, or carry some risk.
Each says what was chosen, why, what it costs, and how to undo it.
**Status:** `agreed` = you approved it in chat; `review` = made while
working unattended, waiting for your review.

---

## D1 · `.slnx` solution format · review
- **What:** `backend/Plannr.slnx` (the XML format .NET 10 creates by default) instead of Applr's `.sln`.
- **Why:** default for new .NET 10 solutions; readable diffs.
- **Cost:** tools older than VS 2022 17.13 / Rider 2024.3 can't open it.
- **Undo:** `dotnet sln migrate` works the other way too, or recreate with `dotnet new sln --format sln`.

## D2 · Warnings are errors · review
- **What:** `TreatWarningsAsErrors` in `Directory.Build.props`.
- **Why:** nullable warnings are future NullReferenceExceptions; failing the build stops them accumulating.
- **Cost:** a new package that emits a warning breaks the build until it's dealt with (never by `NoWarn` without discussing it).

## D3 · Exception middleware brought forward into a minimal Plannr.Api · agreed

## D4 · No `EventPage` source type · agreed
Event vs general web page is decided after fetching (`HasEventData`), not from the URL.

## D5 · Screenshots have no retriever · agreed
The pipeline has two entry points, link and image, instead of an `ImageRetriever`.

## D6 · HTTP first, Playwright only for JavaScript shells, never for 403 · agreed
- **Risk:** the "JS shell" rule (no JSON-LD and < 200 chars of text) is a heuristic. A page that renders some text server-side but puts dates in JS would not trigger the browser.
- **Check:** Eval logs `FetchMethod`; the data pool will show how often HTTP alone was enough.

## D7 · Honest User-Agent `PlannrBot/0.1 (+github url)` · review
- **Why:** BCS/ethics section; easy to defend.
- **Cost:** some sites block unknown bots. That shows up as `ContentUnavailableException` (403) in Eval, which is itself a reportable finding.
- **Undo:** `Retrieval:UserAgent` in config.

## D8 · SSRF guard · review
- **What:** `PublicAddressGuard` refuses private, loopback, link-local (AWS metadata), CGNAT and reserved addresses at connection time.
- **Known gap:** Playwright resolves DNS itself, so for browser fetches the check and the connection are two separate lookups. A DNS-rebinding attack could get between them. **Close it at the network level in AWS** (the Worker's security group/egress rules, IMDSv2) in Milestone 4.
- **Also:** a host that resolves to *both* a public and a private address is refused outright.

## D9 · Place/LocalBusiness JSON-LD kept for location · agreed

## D10 · Rate limiting and caching deferred to the Worker · review
- **Spec says:** "Requests will be rate-limited and cached".
- **Why deferred:** they protect sites from repeated fetches across many users; Eval fetches each link once and the data pool saves snapshots. **Must be done in Milestone 4**, especially for TikTok.

## D11 · Instagram as a subclass of `UnsupportedLinkException` · agreed

## D12 · Scraping moves into its own project at M4 · agreed
- **Now:** Plannr.Api references Infrastructure, which references Playwright, so the API's build output carries Playwright's ~130 MB driver though the API never fetches a page.
- **At M4,** when the Worker arrives: page fetching (HTTP + Playwright, the SSRF guard, the retrievers) moves into `Plannr.Infrastructure.Scraping`, referenced by the Worker and Eval only. The API keeps `LinkClassifier` (Core) for its instant "unsupported link" answer.
- **Why:** a smaller, faster-to-deploy API image with fewer packages to keep patched; and the plan's rule that a crashed browser mustn't take the API down becomes something the compiler enforces, since the API can't call the browser code at all.

## D14 · Refit instead of the OpenAI .NET SDK for Groq · review
- **Spec/plan said:** check whether the OpenAI SDK works against Groq with a base-URL change.
- **Chosen:** Refit, as in Applr. Applr already proves it works against Groq, and Refit's `ApiException` exposes what the extractor needs: status code, the `json_validate_failed` body, and `Retry-After` on 429s. The SDK hides those.
- **Cost:** request/response DTOs are ours to maintain (small; ported from Applr).

## D15 · One shared prompt; fixed category list; anti-injection line · review
- **One prompt for every source type**, so G3 compares link types, not prompts. If a type does badly, per-type hints could be added later; that would need re-measuring all types.
- **Fixed categories** (`ActivityCategories`): music, nightlife, food-drink, markets, arts-culture, comedy, film-tv, sports-fitness, outdoors, workshops-classes, community, other. My choice, not from the spec or the requirements survey; revisit after P2.
- **Prompt injection:** shared pages are written by strangers. The system message tells the model to treat content as information, never instructions. That reduces, but doesn't eliminate, the risk; the worst case is a wrong card the user can edit, since the model's answer is only ever parsed as data.

## D16 · `SourceUrl` is nullable · review
A screenshot has no URL, so `RetrievedContent.SourceUrl` (and `CardDraft.SourceUrl` in change 6) are `Uri?`. The `ActivityCards.SourceUrl` column will need to be nullable too.

## D17 · Refit 16 source-generated client (`AddRefitGeneratedClient`) · review
- **Found:** Refit 16 no longer builds clients by reflection unless you add `Refit.Reflection`; Applr's `AddRefitClient` pattern fails at runtime with `NotSupportedException`.
- **Chosen:** the generated client (request code written at build time). Alternative: add `Refit.Reflection` and keep Applr's call.
- **Also found:** the global `ConfigureHttpClientDefaults` hook doesn't reach this client, because Refit sets its own primary handler. My first test using it **sent one request to the real Groq API** with the dummy key `test-key` (401; no real key, only a sample prompt). Fixed by an explicit `configureClient` parameter on `AddPlannrLlm`; the test now provably goes through a stub. CI (change 9) should consider blocking outbound network for unit tests.

## D18 · JSON-LD mapped by code; the LLM only fills gaps · agreed in principle, details review
- **What:** `JsonLdEventMapper` maps well-formed JSON-LD (name, ISO dates, numeric prices with a currency, Place name/address) straight onto the card. This follows the spec's wording ("reads the event details many sites embed; the LLM fills gaps") more closely than my first design did.
- **Strict on purpose:** a price written as text (`"£5 - £10"`) or without `priceCurrency` is **not** mapped; it's left for the LLM, so a guess never gets the "structured, certain" label.
- **Several events on one page:** the first that hasn't started yet is chosen. A "what's on" page with many events may pick the wrong one; the user can edit it, and Eval will show how often it happens.

## D19 · Date-only times are kept as "date, no time" · review
- `CardDraft.StartTimeKnown` / `EndTimeKnown`: a bare date becomes midnight UK time with the flag off, so the app shows "Sat 14 Dec" rather than "00:00". The `ActivityCards` table will need these two columns.
- **Clock changes:** a time that doesn't exist (spring forward) moves an hour later; one that happens twice (fall back) is taken as the first, BST.

## D20 · Price-reading rules · review
- Pence as integers; "From £x" gives a minimum with no maximum.
- Numbers without a currency are ignored when any number has one ("2 for £10" → £10; "Doors 7pm, £5" → £5).
- "Free before 10pm, £5 after" → £0–£5.
- A number with no currency anywhere is assumed GBP **and flagged** (`CurrencyAssumed`).

## D21 · Which fields get flagged, and when · review
- **Missing** is only raised for the fields a plan needs: **title, start, place, price**. Description, end, address and category are often legitimately absent (a hobby, a park), and flagging them would bury the flags that matter.
- **Uncertain** below confidence **0.6** (`Extraction:FlagBelowConfidence`). A field the LLM gave without a confidence counts as **0.5**, so it's flagged.
- **A start date in the past** is flagged Uncertain (an expired listing, or the LLM guessed the wrong year). Note: saved data-pool pages will often be past by the time they're evaluated, which adds flags but doesn't change G3's "fully correct" scoring.
- **Page `<title>` standing in for a title** is flagged (it's often "Event | Venue | Tickets").
- **A price over £2,000** is treated as a misread (`Invalid`).

## D22 · Skip the LLM when JSON-LD is complete · review
- When the page's JSON-LD alone gives a title, a start **with its time**, a place and a price, Groq isn't called. Saves tokens and removes a source of error on the best-structured pages.
- **Cost:** those cards have no `category` (only the LLM sets it). Turn off with `Extraction:SkipLlmWhenStructuredComplete=false`; Eval can compare both.

## D23 · An LLM failure doesn't fail the capture if the page gave something · review
- If Groq fails (`ExtractionFailedException`) but the page had JSON-LD or a title, the pipeline logs a Warning with a reference and returns a partial card with flags. Only when there's nothing at all (or it's a screenshot) does the failure propagate.
- **Why:** the spec says the user completes the card by hand when extraction fails; a half-filled card is a better start than an empty one.
- **For G3:** these cards will mostly score "not fully correct", which is honest. Eval should report LLM failures separately so a Groq outage isn't mistaken for poor extraction.

## D24 · Core takes `Microsoft.Extensions.Logging.Abstractions` · review
- Core was meant to have no packages. The pipeline needs to write its one summary line and the LLM-failure warning; the abstractions package is interfaces only (no logging implementation), the standard choice for domain libraries.
- **Alternative:** move `ExtractionPipeline` to Infrastructure, keeping Core package-free; it would then live away from the logic it orchestrates.

## D25 · Screenshots are identified by their bytes · review
`ImageSignature` checks PNG/JPEG/WebP magic numbers and ignores the declared type. GIF, HEIC (iPhone's default) and PDF are refused with a message asking for PNG/JPEG/WebP. **iPhone screenshots are PNG**, so the iOS Could isn't blocked, but photos (HEIC) would be.

## D26 · Pages that list several activities · review (worth a paragraph in the report)
- A listicle ("10 cosy things to do in York") or a venue's "what's on" page holds many activities; a card holds one.
- **Now:** the LLM picks one (the prompt says the first upcoming); with JSON-LD, the mapper picks the first event not yet past. If the LLM gives no description, the page's `og:description` is used, and on a listicle that describes the **article**, not the activity (seen in the blog fixture test).
- **Possible later:** detect list pages and offer the user a choice of activities, or one card per activity. Not in the spec; would be a Could.

## D27 · Synthetic fixtures, not saved real pages, in the repo · review
- The repo is public, and a saved page's text and images belong to its site. The fixtures copy **structure** (checked against a live Eventbrite page) with invented content; `Fixtures/Pages/README.md` says what each models.
- **Real snapshots** belong in the data pool (Milestone 2), stored privately, e.g. a gitignored folder or a private repo. Decide where when building the data-pool tooling.

## D28 · Eventbrite's `lowPrice: "0.0"` · to check in the data pool
The live Eventbrite page (21 Oct) had an AggregateOffer from `"0.0"` to `"79.0"`, so the card says £0–£79. That's either a real free tier or Eventbrite's default for the low end. If the data pool shows the latter is common, the mapper should treat a `0.0` low price beside a non-zero high price as unknown.

## D29 · Groq waits: Applr's JobAnalyser rules, with a ceiling per program · agreed
- **Ported from `CvOptimiser/JobAnalyser.py`:** a 429's wait comes from `Retry-After`, else Groq's message ("try again in 1m30s"), else one minute; daily (TPD/RPD) and per-minute limits are told apart; network failures, our timeout and 5xx are retried with growing waits; a 404 for the model is a setup error naming `Groq:TextModel`/`VisionModel`.
- **Different from JobAnalyser:** it waits until a stop time; Plannr has a ceiling, `Groq:MaxRateLimitWaitSeconds`. A known wait over it fails at once with when the limit resets.
  - Default **30 s**, 5 s/10 s/15 s transient retries: for the Worker, where a person is waiting for a card.
  - **Eval: 900 s (15 min), 10 rate-limit waits, 15 s/30 s/45 s retries** (`src/Plannr.Eval/appsettings.json`): data-pool runs have nobody waiting, like JobAnalyser's overnight runs.
- **Found while doing it:** retry waits on a fake clock hang a test forever unless something moves the clock; the extractor tests now drive it through a helper that *fails* after a simulated limit instead of hanging. Also: Plannr.Api and Plannr.Eval both copy an `appsettings.json` into the test output, one overwriting the other; a test reading Eval's settings now reads its source file.

## D30 · CI as staged jobs, each building for itself · agreed
- **Agreed:** build → unit tests → integration tests → deploy, chained with `needs:` (GitLab-style stages); every push plus PRs into `main`; lock files with `--locked-mode`; SDK pinned by `backend/global.json`; read-only token; timeouts plus `--blame-hang-timeout 2m`.
- **Review:** each job checks out and builds again rather than receiving the build job's output. The test project's output is ~130 MB (mostly Playwright's driver), so moving it between jobs would be slower than the ~30 s cached rebuild. The cost is that the "Build" job is a gate ("does it compile?") rather than the thing that's tested. When deploying arrives, the deployable thing (Docker images) should be built once and passed on: "build once, deploy many".
- **First run** (`39766496866`, 22 Oct): all four jobs green on Linux, 34 s + 34 s + 1 m 10 s + 3 s.

## D31 · CI runner pinned to `ubuntu-24.04` · agreed
GitHub's notice on every job: *"The ubuntu-latest label will migrate to Ubuntu 26 beginning October 19, 2026."* Staying on `ubuntu-latest` means the runner's OS changes underneath us at some point (Playwright's `--with-deps` is the step most likely to notice). Pinning `runs-on: ubuntu-24.04` keeps it fixed until we choose to move. **Done in 9e**; moving to 26 later is a one-line change tried on a branch first.

## D32 · The structure manifest and its test · review
- `docs/structure.json` lists every planned file (stub on disk, generated later, or folder) with milestone, purpose, connections and open questions; `StructureManifestTests` keeps it and the disk in step (see `docs/structure.md`).
- **Its reach:** a backend test reads the whole repository (mobile/, infra/, data-pool/). Simple and already in CI; the alternative is a separate script and CI step.
- **Real files aren't listed**, only planned ones; git tracks the rest. So the list shrinks as things get built.

## D33 · EF Core 9 with Pomelo 9, as in Applr · agreed
Pomelo's MySQL provider for EF Core 10 is the open risk; Applr already runs EF Core 9 + Pomelo 9 on .NET 10. Revisit only if a stable Pomelo 10 is out and something needs it.

## D34 · One repository class per table, as in Applr · agreed
`ICardRepository`, `ICaptureRepository`, ... in Core, EF implementations in Infrastructure. A service that needs two tables uses two repositories.

## D35 · AWS region eu-west-2 (London) · agreed
Meets the spec's "stored in the UK" for user data (UK GDPR section).

## D36 · `qwen/qwen3.8-27b` for both text and screenshots · review
Groq's model list (22 Oct) has no model labelled for images. Qwen 3.8 was tested with a drawn event flyer and read every field correctly in ~1 s, so it is Eval's `Groq:VisionModel` as well as its text model. Re-check with `models` if Groq changes its line-up; a 404 now names the setting to change.

## D37 · How the Worker tells the app a card is ready · question (recommendation changed)
The phone's live connection (SignalR) is to the **API**; the card is made by the **Worker**, a separate program. Options:
- **(a)** the Worker calls a private API endpoint ("capture 42 done");
- **(b)** the API checks the Captures table every ~2 s for newly finished captures and tells the right user;
- **(c)** a message broker (Redis) that both connect to.

**I now recommend (b)** (I first suggested (a)): nothing new to secure (an internal endpoint on a public API needs its own authentication), it survives either program restarting, and it keeps working with more than one API instance, because each instance tells its own connected phones. The cost is up to ~2 s of delay on top of the extraction itself, and one small query every 2 s.

## D38 · No Docker on this laptop (yet) · question
Checked 22 Oct: Docker isn't installed, WSL isn't installed, and the firmware reports hardware virtualisation **disabled**, which Docker Desktop needs. `MySQL80` is installed and running (Applr's).
- **Recommendation:** develop against the local MySQL80 (a separate `plannr` database); in CI, database tests use a MySQL **service container** (GitHub's runners have Docker). Docker locally becomes optional; Docker images are built by CI in T2.
- If you want Docker anyway: enable virtualisation in the BIOS, then install WSL 2 and Docker Desktop (see the walkthrough in chat / progress.md).

## D39 · Branches: decided in principle, not yet in use · agreed
Feature branches with pull requests once branch protection is on (you set it in GitHub's settings). Until then, and as you chose on 22 Oct, work continues on `main`. Opening PRs from here also needs the GitHub CLI (`gh`), which isn't installed.

## D40 · Every outside connection behind an interface · agreed
Your rule, as in Applr: anything that reaches outside the program (web pages, Chromium, Groq, DNS, MySQL, the job queue, push, TikTok, YouTube, the clock) is used through an interface, registered in `Program.cs` through dependency injection, so tests swap in a fake through the same DI.
- **Already true** for page fetching, Chromium, Groq and the clock (see `docs/architecture.md` §5).
- **One gap in built code:** `PublicAddressGuard` and `PlaywrightPageFetcher` call `Dns` directly. Planned `IHostResolver` (M4, with the scraping split).
- **Planned Refit interfaces** added to `structure.json`: `IExpoPushApi` (M7), `ITikTokOEmbedApi` (M6), `IYouTubeDataApi` (T2).

## D13 · Secrets never live in tracked files · agreed
This repo is public. Keys and passwords go in `dotnet user-secrets` locally (stored in the user profile, outside the repo) and in SSM Parameter Store on AWS, never in a committed file. `.gitignore` also ignores every `appsettings.Development.json` and `appsettings.*.local.json` as a safety net. Test code uses obviously fake values that don't look like real key formats (so secret scanners don't false-alarm).

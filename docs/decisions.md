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

## D12 · Plannr.Api references Playwright (through Infrastructure) · review
- **Cost:** the API's build output contains the Playwright library (not the browser). Harmless now.
- **Plan:** when the Worker arrives, consider splitting scraping into `Plannr.Infrastructure.Scraping` so the API's Docker image stays small.

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

## D13 · ⚠️ Groq key exposed in Applr's git history · action for you
`Applr/src/Applr.API/appsettings.Development.json` is tracked in git and holds a Groq key; the repo has a GitHub remote. **Revoke that key in the Groq console** and create a new one for Plannr (stored with `dotnet user-secrets`, see `docs/progress.md`).

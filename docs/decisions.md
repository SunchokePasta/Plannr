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

## D13 · ⚠️ Groq key exposed in Applr's git history · action for you
`Applr/src/Applr.API/appsettings.Development.json` is tracked in git and holds a Groq key; the repo has a GitHub remote. **Revoke that key in the Groq console** and create a new one for Plannr (stored with `dotnet user-secrets`, see `docs/progress.md`).

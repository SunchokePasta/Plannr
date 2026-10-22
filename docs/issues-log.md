# Issues log

Every error or surprise met while building Plannr that changed the code,
the design, or how something is done: what happened, why, what changed,
and what caught it. Kept alongside [`decisions.md`](decisions.md) (choices)
and [`progress.md`](progress.md) (what's done). Newest at the bottom; the
change number says where in the history to look.

**Caught by:** *build* (compiler/MSBuild), *test* (a test failed), *review*
(spotted reading the code before running it), *live* (running against a real
site or service), *CI* (GitHub Actions), *tooling* (a command or script).

## Code and design

| # | Change | What happened | Why | What changed | Caught by |
|---|---|---|---|---|---|
| 1 | 1 | `MSB4025: The project file could not be loaded` for Plannr.Core, so it was also left out of the solution | I used Applr's `--` dash style inside an XML comment in the `.csproj`; XML forbids `--` in comments | Rewrote the comment; added Core to the solution afterwards. Rule: `--` is fine in C# comments, never in `.csproj`/`.props` | build |
| 2 | 4d | Playwright's install script failed: `Could not find file ... Microsoft.Playwright.dll` | Ran it from the class library's `bin`; class libraries don't copy their package DLLs there | Run it from an executable project's output (`Plannr.Eval`); README and progress notes say so | tooling |
| 3 | 4d | Test project didn't compile: no `UseUrls` / `GetRequiredFeature` | Missing `using Microsoft.AspNetCore.Hosting` and `...Http.Features` | Added the usings | build |
| 4 | 4d | Disposing the DI container threw `type only implements IAsyncDisposable` | `PlaywrightPageFetcher` (a singleton) could only be disposed asynchronously; a synchronously disposed container can't do that | It now implements `IDisposable` too | test |
| 5 | 5b | The prompt needed a source address for screenshots, which have none | `RetrievedContent.SourceUrl` was required | Made it nullable (D16) | review |
| 6 | 5d | `NotSupportedException: This interface needs the reflection request builder` | Refit 16 no longer builds clients by reflection without the extra `Refit.Reflection` package; Applr's older Refit did | Switched to Refit's source-generated client, `AddRefitGeneratedClient` (D17) | test |
| 7 | 5d | A test got a **real 401 from Groq** | `ConfigureHttpClientDefaults` doesn't reach a Refit client (Refit sets its own primary handler), so the stub was never used and the request went to the real API with a dummy key | `AddPlannrLlm` takes a `configureClient` hook; the test now provably uses a stub (D17) | test |
| 8 | 6a | `ChooseEvent` compared a `DateTimeOffset` with `now.Date` (a `DateTime`) | C# converts the `DateTime` using the **machine's** time zone, so the result would differ between your laptop (UK) and CI (UTC) | Builds UK midnight explicitly with `UkTime` | review |
| 9 | 6a | `PriceParser` would read "2 for £10" as 2p and "Free entry before 10pm" as £10 | Every number was treated as a price | Numbers without a currency are ignored when any number has one; text starting "free" is a free minimum (D20) | review |
| 10 | 6a | A pointless `UkTime.Zone == null` check | My own slip while fixing #8 | Removed | review |
| 11 | 7a | Eval's top-level log lines had an empty SourceContext (`[ERR]  [ref]`) | The static `Log` outside any class has no context | `Log.ForContext("SourceContext", "Plannr.Eval")` | live |
| 12 | 7b | A live Eventbrite address came out as "..., London, W6 0RF, London" | Eventbrite puts the whole address in `streetAddress` and repeats the town in `addressLocality` | The mapper skips parts already in the address | live |
| 13 | 7b | The pipeline's summary line ended "0 flag(s) ." | Joining an empty list | Says "none" | live |
| 14 | 8b | The blog test expected every field from the LLM, but the description came from `og:description` | With no LLM description the page's own description is used, and on a listicle that describes the article, not the activity | The test asserts the real behaviour; the limitation is D26 | test |
| 15 | 9a | My earlier claim that Applr's Groq key was in its pushed history was **wrong**: it was only an uncommitted local change | I warned before checking | D13 rewritten before Plannr's first public push; the fake test key lost its `gsk_` prefix so GitHub's secret scanning wouldn't flag it | review |
| 16 | 5e | The test run **hung** and had to be killed | Retry waits now ran on the fake clock, and nothing moved the clock forward | Tests drive the clock through a helper that fails after a simulated limit; CI got job timeouts and `--blame-hang-timeout 2m` | test |
| 17 | 5e | `CS9007` in two tests | A `$$"""` raw string whose JSON ends in `}}` clashes with `$$` interpolation | `$$$"""` with `{{{...}}}` | build |
| 18 | 5e | `GroqRateLimit` used generated regexes, which need a `partial` type | Declared as a plain `record` | `partial record` | review |
| 19 | 5e | A test reading Eval's settings passed only by luck | Plannr.Api and Plannr.Eval both copy an `appsettings.json` into the test output; one overwrites the other | The test reads Eval's file from its source folder | review |
| 20 | 9b | CI's `.trx` result files would have been committable | `.gitignore` covered `TestResult.xml` but not a `TestResults/` folder | Added `TestResults/` | review |
| 21 | 9b | Passing the build between CI jobs would mean moving ~127 MB | The test project's output includes Playwright's Node driver | Each CI job builds for itself (~30 s with the cache) (D30) | review |
| 22 | 10a | The backend-only manifest slice failed its own test | `SnapshotStore` (backend) connects to `data-pool/README.md`, which wasn't in that slice | Data pool committed with the backend (10a), mobile (10b), infra (10c) | test |
| 23 | 5f | `models` failed with **401**; the stored key was `<gsk_...>` | My example command showed `"<your key>"`, and the angle brackets were kept | `Validate()` now refuses a key with `< >`, surrounding spaces or quotes (without showing the key); every example says `"gsk_..."` | live |
| 24 | 5f | `models` printed Refit's raw 401 stack trace | Only the extractor turned 401 into "Groq refused the API key" | `models` does too | live |
| 25 | 5f | Groq's model list showed **no obviously image-capable model**, and screenshots are a Must | Groq retires and renames models; nothing in the list is labelled for vision | Tested `qwen/qwen3.8-27b` with a drawn flyer: it read every field correctly. Set as Eval's `VisionModel` (D36); `models` now marks a model used for both jobs | live |

## Tooling hiccups (no code change, but worth knowing)

| # | When | What happened | What was done |
|---|---|---|---|
| T1 | 4 | `grep --include` patterns weren't applied (wrong argument order) | Re-ran the check another way |
| T2 | 8a | A shell heredoc writing the fixture pages broke on quoting (`unexpected EOF`); nothing was written | Wrote each file with the file tool |
| T3 | 9a | A commit command returned an empty tool error and didn't run | Checked `git status`, committed again |
| T4 | 9d | My first CI watcher never saw the run finish (it pulled the `status` field out wrong) and a second script mis-split job names | Stopped it; rewrote the watcher in Python |
| T5 | 10b | I silenced git's line-ending warnings (`2>/dev/null`) on one commit, against the "don't hide warnings" rule | Called out; not repeated |
| T6 | 10c | The 10c commit message says "106 entries"; there are 104 | Left as is: correcting it would mean rewriting public history |
| T7 | docs | A background CI watcher crashed on a DNS lookup failure on the laptop (network blip), so it never reported | Checked the run directly (green); watchers should retry network errors |
| T8 | docs | Couldn't check the architecture page's diagrams render: the built-in browser isn't signed in to Claude, and I don't sign in for you | Opened it in your side panel to check by eye |

## Findings to carry into the data pool (behaviour, not bugs)

- **Inferred dates can get high confidence.** Notting Hill Carnival (Wikipedia): the model inferred next year's August bank holiday weekend and wasn't flagged. Watch for this in G3.
- **Concessions:** a flyer saying "Entry £3, students free" became £3-£3, not £0-£3. The labelling rules need to say which is right.
- **Eventbrite `lowPrice: "0.0"`** beside a real high price (D28).

# Plannr architecture

One big overview with labelled parts, then a detail diagram per topic. The
labels (**P1**, **A4**, **W3** ...) and step numbers (**①** ...) are what the
other docs and our conversations point at. Yellow boxes are the parts the
open questions are about.

GitHub draws these diagrams (they're written in Mermaid); the same page is
also published as an artifact for reading in the app.

## §1 · Overview: the whole system

Two kinds of boxes matter:
- **Programs** run on their own: the phone app, `Plannr.Api`, `Plannr.Worker`, `Plannr.Eval`, MySQL, Chromium.
- **Libraries** (`Plannr.Core`, `Plannr.Infrastructure`, the planned `Plannr.Infrastructure.Scraping`) never run on their own; they're compiled into programs (§4).

```mermaid
flowchart TB
  subgraph P["P · Phone app (Expo)"]
    P1["P1 · Share sheet<br/>a link or screenshot comes in"]
    P2["P2 · Screens<br/>wishlist, card, groups, plans"]
    P3["P3 · API client<br/>HTTPS + JSON"]
    P4["P4 · Live updates<br/>SignalR connection"]
    P5["P5 · Push notifications<br/>when the app is closed"]
  end

  subgraph A["A · Plannr.Api (AWS)"]
    A1["A1 · Error boundary<br/>exception middleware"]
    A2["A2 · Endpoints<br/>auth, captures, cards, groups, plans"]
    A3["A3 · Updates hub<br/>SignalR"]
    A4["A4 · Completion watcher<br/>the Q8 part, D37"]
  end

  subgraph D["D · MySQL database"]
    D1[("D1 · Users")]
    D2[("D2 · Captures<br/>also the job queue")]
    D3[("D3 · Cards + flags")]
    D4[("D4 · Groups, plans,<br/>responses")]
    D5[("D5 · Push tokens")]
  end

  subgraph W["W · Plannr.Worker (AWS)"]
    W1["W1 · Job loop<br/>claims the next Pending capture"]
    W2["W2 · ExtractionPipeline<br/>from the Plannr.Core library"]
    W3["W3 · Scraping library<br/>HTTP, Chromium, retrievers, SSRF guard<br/>D12"]
    W4["W4 · Groq client<br/>from Plannr.Infrastructure"]
  end

  subgraph X["X · Outside services"]
    X1["X1 · Shared web pages"]
    X2["X2 · TikTok"]
    X3["X3 · Groq LLM"]
    X4["X4 · Expo push service"]
  end

  subgraph L["L · Your laptop"]
    E1["E1 · Plannr.Eval<br/>extract, snapshot, score"]
    E2[("E2 · Data pool<br/>saved pages + labels")]
  end

  subgraph G["G · GitHub"]
    G1["G1 · CI<br/>build, tests, deploy"]
  end

  P1 --> P2
  P2 --> P3
  P3 -->|"① POST /captures"| A1
  A1 --> A2
  A2 -->|"② new capture, Pending"| D2
  A2 <-->|"cards, groups, plans"| D3
  A2 <--> D4
  A2 <--> D1
  W1 -->|"③ claim it"| D2
  W1 --> W2
  W2 --> W3
  W3 -->|"④ fetch the page"| X1
  W3 --> X2
  W2 --> W4
  W4 -->|"⑤ extract"| X3
  W1 -->|"⑥ save card, mark Done"| D3
  A4 -->|"⑦ notice Done"| D2
  A4 --> A3
  A3 -->|"⑧ CaptureCompleted"| P4
  A4 -->|"⑧ app closed: push"| X4
  X4 --> P5
  A2 <--> D5
  E1 -.->|"same pipeline"| W2
  E1 <--> E2
  G1 -.->|"deploy, Term 2"| A
  G1 -.-> W

  classDef question fill:#fde68a,stroke:#b45309,stroke-width:3px,color:#1f2937
  class A4,W3 question
```

| Step | What happens |
|---|---|
| ① | You share a link; the app sends it to the API. |
| ② | The API checks it's a link Plannr can read (instant), stores it as a **Pending capture**, and answers straight away. The app shows a "processing" card. |
| ③ | The Worker, checking every second or two, claims the capture. |
| ④ | It fetches the page (plain HTTP, or Chromium for JavaScript pages). |
| ⑤ | It maps the page's own event data by code and asks Groq only for what's missing. |
| ⑥ | It saves the card and its flags, and marks the capture **Done** (or **Failed**, with a message and an error reference). |
| ⑦ | The API finds out the capture is done. **How is question 8** (§3). |
| ⑧ | The API tells your phone: instantly over the live connection if the app is open, or as a push notification if it's closed. |

## §2 · Sharing a link, end to end

The same journey as §1 ①-⑧, in time order. The yellow band is the part
question 8 is about.

```mermaid
sequenceDiagram
  autonumber
  actor U as You
  participant App as Phone app (P)
  participant Api as Plannr.Api (A)
  participant Db as MySQL (D)
  participant Wk as Plannr.Worker (W)
  participant Web as Shared page (X1)
  participant Groq as Groq (X3)

  U->>App: Share a link from TikTok or the browser (P1)
  App->>Api: POST /api/v1/captures with the URL (A2)
  Api->>Api: LinkClassifier checks the link. Instagram or ftp gets a 400 here
  Api->>Db: Save the capture as Pending (D2)
  Api-->>App: 202 Accepted with the capture's id
  App-->>U: A "processing" card appears in the wishlist
  loop every 1-2 seconds
    Wk->>Db: Claim the oldest Pending capture (W1)
  end
  Wk->>Web: Fetch the page, Chromium only if it needs JavaScript (W3)
  Web-->>Wk: HTML
  Wk->>Wk: Read JSON-LD, Open Graph and text. Map the JSON-LD by code (W2)
  Wk->>Groq: Ask only for the fields still missing (W4)
  Groq-->>Wk: JSON answer with a confidence per field
  Wk->>Db: Save the card and its flags, mark the capture Done (D3)
  rect rgba(245, 158, 11, 0.18)
    Note over Api,Wk: Question 8: the Worker is finished, but the phone is connected to the API, not the Worker
    loop every ~2 seconds (option b)
      Api->>Db: Any captures finished since I last looked? (A4)
    end
    Db-->>Api: Capture 42, for user 7, is Done
    Api-->>App: CaptureCompleted over SignalR (A3 to P4)
  end
  App->>Api: GET /api/v1/cards/{id}
  Api-->>App: The card, flags included
  App-->>U: Card shown, uncertain fields highlighted
```

If anything fails (the page won't load, Groq is down), the Worker marks the
capture **Failed** with a message for you and an error reference, and the
same yellow-band path tells the phone.

## §3 · Question 8: how the API learns a card is ready

**The problem in one line:** the card is finished **inside the Worker**
(§1 W), but your phone's live connection is to the **API** (§1 A3), and the
two are separate programs, usually on separate machines. Something has to
carry "capture 42 is done" from one to the other (§1 ⑦).

```mermaid
flowchart LR
  subgraph OA["(a) The Worker calls the API"]
    a1["Worker"] -->|"POST /internal/captures/42/done<br/>with a shared secret"| a2["Plannr.Api"]
    a2 --> a3["Your phone"]
  end
  subgraph OB["(b) The API checks the database: recommended"]
    b1["Worker"] -->|"marks Done"| b2[("Captures table")]
    b3["Plannr.Api"] -->|"every ~2 s: anything new?"| b2
    b3 --> b4["Your phone"]
  end
  subgraph OC["(c) A message broker in the middle"]
    c1["Worker"] -->|"publish"| c2[("Redis")]
    c2 -->|"subscribe"| c3["Plannr.Api"]
    c3 --> c4["Your phone"]
  end

  classDef pick fill:#fde68a,stroke:#b45309,stroke-width:3px,color:#1f2937
  class b3 pick
```

| | (a) Worker calls the API | **(b) API checks the database** | (c) Message broker |
|---|---|---|---|
| Delay | Instant | Up to ~2 s (the card itself takes several seconds anyway) | Instant |
| New things to build | An internal endpoint, and a secret to stop anyone else calling it on a public API | A small background loop in the API | A Redis server to run and pay for |
| If the API restarts | The Worker's call fails: retry logic needed | Nothing lost: it picks up where it left off | Messages can be lost unless stored |
| Two copies of the API (AWS scaling) | Only the copy that got the call tells its phones; others miss it | **Works:** each copy checks and tells its own connected phones | Works |

## §4 · Programs and libraries: where scraping lives (D12)

**Short answer: the scraping project is a C# library, not a separate service,
so nothing talks to it through an API.** A library is a `.dll` of code that's
**compiled into** a program. The Worker calls it with ordinary method calls
inside its own process, as fast as calling any other function. Separate
programs (the API and the Worker) can't call each other's methods; they
communicate through something shared, here the database.

```mermaid
flowchart TB
  subgraph WP["Plannr.Worker: one running program (one process)"]
    w1["Worker's own code: the job loop"]
    subgraph LIBS["Libraries compiled into it: plain method calls, no network"]
      core["Plannr.Core<br/>ExtractionPipeline, JSON-LD mapper, normaliser"]
      scrape["Plannr.Infrastructure.Scraping<br/>page fetchers, retrievers, SSRF guard<br/>new at M4"]
      infra["Plannr.Infrastructure<br/>Groq client, database, logging"]
    end
    w1 -->|"pipeline.ExtractFromLinkAsync(url)"| core
    core -->|"retriever.RetrieveAsync(url)"| scrape
    core -->|"llm.ExtractAsync(content)"| infra
  end

  chrome["Chromium: its own process,<br/>started and driven by Playwright"]
  scrape -->|"Playwright's protocol"| chrome

  subgraph AP["Plannr.Api: a different program"]
    api["API's own code: endpoints, hub"]
    core2["Plannr.Core"]
    infra2["Plannr.Infrastructure"]
    api --> core2
    api --> infra2
  end

  db[("MySQL")]
  WP -.-|"the only link between<br/>the two programs"| db
  AP -.- db

  classDef question fill:#fde68a,stroke:#b45309,stroke-width:3px,color:#1f2937
  class scrape question
```

**Why split scraping out of `Plannr.Infrastructure` at all:** today the API
references Infrastructure, which contains the Playwright code, so the API's
build carries Playwright's ~130 MB driver though it never opens a page. Once
scraping is its own library that **only the Worker and Eval reference**:
- the API's Docker image is smaller and quicker to deploy, with fewer packages to keep patched;
- the API **can't** call browser code even by mistake (the compiler won't find it), which enforces the plan's rule that a crashed browser mustn't take the API down.

Which project may use which (arrows point at what's used):

```mermaid
flowchart BT
  core["Plannr.Core<br/>domain: no outside world"]
  infra["Plannr.Infrastructure<br/>Groq, database, logging"]
  scrape["Plannr.Infrastructure.Scraping<br/>M4"]
  api["Plannr.Api"]
  worker["Plannr.Worker<br/>M4"]
  eval["Plannr.Eval"]

  infra --> core
  scrape --> core
  scrape --> infra
  api --> core
  api --> infra
  worker --> core
  worker --> infra
  worker --> scrape
  eval --> core
  eval --> infra
  eval --> scrape
  api -.->|"not allowed"| scrape

  classDef question fill:#fde68a,stroke:#b45309,stroke-width:3px,color:#1f2937
  class scrape question
```

## §5 · Outside connections behind interfaces (D40)

Your rule, as in Applr: **everything that talks to the outside world sits
behind an interface**, so `Program.cs` registers the real thing and tests
register a fake through the same dependency injection. The code that uses it
only ever sees the interface.

```mermaid
flowchart LR
  pipe["ExtractionPipeline<br/>only knows the interface"] --> iface{{"ILlmExtractor"}}
  iface -->|"Program.cs: AddPlannrLlm(...)"| real["GroqLlmExtractor<br/>real Groq over HTTPS"]
  iface -->|"tests: new FakeLlm(...)"| fake["FakeLlm<br/>canned answers, no network"]
```

| Outside thing | Interface | Real (Program.cs) | Fake (tests) | Status |
|---|---|---|---|---|
| Web pages over HTTP | `IPageFetcher` | `HttpPageFetcher` | `FixtureFetcher`, stub HTTP handler | built |
| Chromium | `IBrowserPageFetcher` | `PlaywrightPageFetcher` | `FakeFetcher` | built |
| Groq | `IGroqApi` (Refit), `ILlmExtractor` | Refit client, `GroqLlmExtractor` | `FakeGroqApi`, `FakeLlm` | built |
| The clock | `TimeProvider` | `TimeProvider.System` | `FakeTimeProvider` | built |
| DNS (the SSRF guard's lookup) | none yet: calls `Dns` directly | | the address check is tested directly | **gap: add `IHostResolver`** |
| MySQL | one repository interface per table (D34) | EF Core repositories | in-memory fakes / test database | M3+ |
| Job queue | `ICaptureQueue` | `MySqlCaptureQueue` | in-memory queue | M4 |
| Telling the phone | `INotificationService` | SignalR + Expo push | recording fake | M4 |
| Expo push | `IExpoPushApi` (Refit) | Refit client | fake | M7 |
| TikTok oEmbed | `ITikTokOEmbedApi` (Refit) | Refit client | fake | M6 |
| YouTube Data API | `IYouTubeDataApi` (Refit) | Refit client | fake | T2 |

## §6 · Getting the app onto your phone (question 13, from first principles)

**Three layers.** We write the app in **TypeScript**. **React Native** turns
our screens into real Android screens (not a web page in a browser).
**Expo** is a toolkit on top: ready-made modules (notifications, secure
storage, calendar, share handling) and **EAS**, a free cloud service that
builds the installable Android app (an **APK**) so you don't need Android
Studio.

An Android app has two parts:
- a **native part**, fixed at the moment the app is built. It includes the app's *manifest*, which tells Android what the app can do, such as "I can receive shared links";
- a **JavaScript part**: our code, which can be swapped while developing.

```mermaid
flowchart TB
  ts["Our TypeScript code<br/>screens, API client, share handling"]
  rn["React Native<br/>turns our screens into real Android views"]
  mods["Expo modules<br/>notifications, secure storage, share intent"]
  subgraph APK["The installable app (APK)"]
    native["Native part: fixed when built<br/>its manifest says 'I accept shared links'"]
    js["JavaScript part: our code"]
  end
  eas["EAS Build<br/>Expo's servers build the APK"]
  ts --> rn
  rn --> js
  mods --> native
  eas --> APK
```

**Why the share sheet decides everything.** When you tap *Share* in TikTok,
Android looks through every installed app's manifest for "I accept shared
text" and lists those apps. Plannr only appears if **its own** manifest
says so:

```mermaid
sequenceDiagram
  actor U as You
  participant TT as TikTok
  participant OS as Android
  participant PL as Plannr app
  U->>TT: Tap Share on a video
  TT->>OS: Share this text: https://vm.tiktok.com/...
  OS->>OS: Which installed apps declare they accept shared text?
  OS-->>U: Share sheet lists them, Plannr included
  U->>OS: Tap Plannr
  OS->>PL: Open Plannr with the link
  PL->>PL: expo-share-intent hands the link to our code (P1)
  PL-->>U: A "saving" card, at once
```

**Three ways to run the app:**

```mermaid
flowchart LR
  subgraph GO["1 · Expo Go, from the Play Store"]
    go1["Expo's own app, with Expo's manifest"]
    go2["Loads our JavaScript from the laptop"]
    go3["Can't be in the share sheet:<br/>the manifest is Expo's, not ours"]
  end
  subgraph DEV["2 · Development build: what we need"]
    dev1["Our own APK, built once by EAS<br/>with our manifest: share target"]
    dev2["Still loads JavaScript live from the laptop:<br/>edit, save, see it on the phone in seconds"]
    dev3["Rebuilt only when native settings change"]
  end
  subgraph REL["3 · Release build, for the user trial"]
    rel1["Everything inside: no laptop needed"]
    rel2["Sent to trial users as a download link"]
  end

  classDef pick fill:#fde68a,stroke:#b45309,stroke-width:3px,color:#1f2937
  class dev1 pick
```

**How the development build reaches your phone:**

```mermaid
flowchart LR
  laptop["Your laptop<br/>eas build --profile development"] -->|"uploads the project"| cloud["EAS: Expo's build servers<br/>builds the APK"]
  cloud -->|"download link or QR code"| phone["Your Android phone<br/>asks: allow installing apps from this source?"]
  laptop -->|"while developing: our JavaScript over Wi-Fi"| phone
```

**So the three things I asked for are:**
1. **A free account at expo.dev**: EAS builds run under it.
2. **Your Android version** (Settings → About phone): to check the share-intent module supports it.
3. **Whether your phone lets you install an app from a download link**: Android asks for permission ("install unknown apps"); some work or school-managed phones block it.

## §7 · Your approval before anything reaches `main`

```mermaid
flowchart LR
  b["Feature branch<br/>I push changes here"] --> pr["Pull request into main"]
  pr --> ci{"CI passes?<br/>Build, Unit tests, Integration tests"}
  ci -->|"no"| fix["I fix it on the branch"]
  fix --> pr
  ci -->|"yes"| you{"You review the pull request"}
  you -->|"you click Merge"| main["main"]
  you -->|"you ask for changes"| fix
  main --> dep["CI on main, deploy job: Term 2"]

  classDef pick fill:#fde68a,stroke:#b45309,stroke-width:3px,color:#1f2937
  class you pick
```

**It isn't set up yet;** it needs the ruleset from our chat, which only you
can create. One GitHub detail decides how: **GitHub never lets the author of
a pull request approve it**, and my commits are pushed with your git login,
so the pull requests are yours. A rule of "1 approval required" would
therefore block every merge. Instead:
- the ruleset requires a **pull request** and **passing CI** for `main`, and no direct pushes;
- **your click on Merge is the approval**: I never merge, so nothing reaches `main` without you.

If you later want formal "Approve" reviews, the usual way is a second GitHub
account for automated changes.

## §8 · CI pipeline

```mermaid
flowchart LR
  push["Push or pull request"] --> build["Build"]
  build --> unit["Unit tests"]
  unit --> integ["Integration tests<br/>real Chromium"]
  integ --> deploy["Deploy<br/>placeholder, main only"]
```

Each stage waits for the one before and is skipped if it fails. The runner
is pinned to Ubuntu 24.04 (D31).

## §9 · The data pool and scoring (M2, next)

```mermaid
flowchart LR
  links["100 links you collect<br/>events, web, TikTok, screenshots"] --> snap["Eval: snapshot<br/>save each page once"]
  snap --> pool[("Data pool<br/>saved pages + labels")]
  label["You and a second checker<br/>write the expected fields"] --> pool
  pool --> score["Eval: score<br/>the same ExtractionPipeline as the Worker, W2"]
  score --> report["Report for P4<br/>accuracy per link type vs G3"]
```

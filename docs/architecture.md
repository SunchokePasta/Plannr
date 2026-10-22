# Plannr architecture

How Plannr's parts fit together. Section 1 explains the words; section 2
is the whole system at a glance; section 3 walks through every step of
sharing a link; the sections after that zoom in on one topic each. On
every diagram, **each arrow says what it does**.

**Numbering:** the five **groups** of the system are numbered **1-5**
(1 phone, 2 server, 3 database, 4 worker, 5 outside services), and the
parts inside a group are **1a, 2c** and so on. The steps of sharing a link
are **① to ⑰**, in order. **Yellow** marks an open design question.

## §1 · Words used on this page

| Word | What it means here |
|---|---|
| **AI client** | The part of the worker that sends text or a screenshot to Groq and reads back its answer. |
| **API** | The set of addresses the server answers on. The phone app uses it to send and fetch things over the internet. "The server" and "the API" are used to mean the same program. |
| **APK** | The installable app file for Android. |
| **Branch** | A separate line of changes in git, kept apart from `main` until it's merged. |
| **Capture** | **One thing you shared** (a link or a screenshot) and its progress towards becoming a card: *waiting*, *being processed*, *done* or *failed*. Each capture is a row in the captures table. |
| **Card** | **The result of a capture:** an activity with its title, date and time, place, price and description. You can edit it, keep it, or send it to a group. |
| **Chromium** | A real web browser with no window, used for pages that only show their content after running JavaScript. Playwright is the library that drives it. |
| **CI** | Automatic checks that GitHub runs on every change: build the code and run the tests. |
| **Completion checker** | The part of the server that looks for captures the worker has finished, so it can tell your phone. |
| **Data pool** | A saved set of real links, with the correct answers written down by hand, used to measure how accurate Plannr's reading is. |
| **Database** | Where everything is stored, in tables (MySQL). |
| **Dependency injection** | The program is told at start-up which real or fake version of each part to use, instead of each part creating its own. This is what lets tests swap in fakes. |
| **Development build** | Your own copy of the app, installed on your phone, that still loads its code live from the laptop while we work on it. |
| **Endpoint** | One address on the API that does one job, such as "accept a shared link". |
| **Error reference** | A short code shown with an error, such as `7f3a9c21:03:01`, that finds the matching line in the log. |
| **Eval** | A command-line tool on the laptop that runs the same extraction as the worker, for testing and measuring accuracy. |
| **Expo** | A toolkit for building phone apps with React Native: ready-made modules plus a build service (EAS). |
| **Expo push service** | Expo's servers, which deliver notifications to phones. |
| **Extraction pipeline** | The fixed sequence of steps that turns a capture into a card: fetch the content, read its hidden details, ask the AI for what's missing, check and tidy the answer, add flags. |
| **Fake** | A stand-in used in tests in place of something outside, for example an AI that returns prepared answers without using the internet. |
| **Flag** | A marker on one field of a card that Plannr isn't sure about (missing, uncertain or unreadable), highlighted for you to check. |
| **Groq** | The company whose servers run the AI (the LLM) Plannr uses. |
| **Group** | Friends who plan together in Plannr. |
| **Interface** | A description of what a part must be able to do, without saying how. Code that uses an interface works with any part that fits it, real or fake. |
| **Job loop** | The worker's main loop: take the next waiting capture, process it, repeat. |
| **JSON-LD** | Event details that many websites hide in their pages for search engines: name, date, venue, price, in a standard machine-readable form. |
| **Library** | Code that never runs on its own; it's built into programs that use it. |
| **LLM** | Large language model: an AI that reads text (or images) and answers questions about it. |
| **Live updates hub** | The part of the server that keeps a connection open to each running app, so it can tell the app about changes instantly (SignalR). |
| **main** | The main branch: the version of the code that counts. |
| **Open Graph tags** | A title, description and image hidden in a web page so link previews look right. |
| **Plan** | A card sent to a group to agree a date. |
| **Program** | Code that runs on its own: the phone app, the server, the worker, Eval, the database. |
| **Pull request** | A request on GitHub to merge a branch into `main`, where the changes can be checked and approved. |
| **Push notification** | A message that appears on your phone when the Plannr app isn't open. |
| **React Native** | A way to write a phone app in TypeScript that turns into real Android screens. |
| **Response** | A group member's yes or no to a plan. |
| **Retriever** | The part that fetches the content for one kind of link: web pages, TikTok, YouTube. |
| **Safety guard (SSRF)** | A check that stops a shared link from pointing the server at its own private network. |
| **Scraping** | Reading a web page's content automatically. |
| **Server** | A computer on the internet, and the program running on it, that the app talks to. Plannr's server program is `Plannr.Api`. |
| **Share sheet** | The list of apps your phone shows when you tap Share. |
| **Wishlist** | All your saved cards. |
| **Worker** | A second server program that does the slow work (fetching pages, asking the AI) in the background, so the server can always answer quickly. |

## §2 · The system at a glance

The five groups, and what passes between them. Section 3 opens each group
up into its parts and walks through every step.

```mermaid
flowchart LR
  G1["1 · Your phone<br/>the Plannr app"]
  G2["2 · Plannr server<br/>the API"]
  G3[("3 · Database")]
  G4["4 · Background worker"]
  G5["5 · Outside services<br/>websites, Groq, Expo push"]

  G1 -->|"sends shared links,<br/>asks for finished cards"| G2
  G2 -->|"replies, and sends live<br/>'card ready' messages"| G1
  G2 -->|"saves new captures,<br/>reads finished cards"| G3
  G4 -->|"claims waiting captures,<br/>saves finished cards"| G3
  G4 -->|"downloads shared pages,<br/>asks Groq to read them"| G5
  G2 -->|"asks Expo to send<br/>notifications"| G5
  G5 -->|"delivers notifications"| G1
```

## §3 · Sharing a link, step by step

Every part of every group has its own column, under its group's heading.
The steps run **top to bottom, ① to ⑰**; each one starts where the one
before it ended. Solid arrows are requests, dashed arrows are the replies.

```mermaid
sequenceDiagram
  actor You
  box rgba(127,178,236,0.12) 1 · Your phone, the Plannr app
    participant N1a as 1a · Share button
    participant N1b as 1b · Wishlist screen
    participant N1c as 1c · Notifications
  end
  box rgba(160,160,160,0.12) 2 · Plannr server, the API
    participant N2a as 2a · Captures endpoint
    participant N2c as 2c · Completion checker
    participant N2d as 2d · Live updates hub
    participant N2b as 2b · Cards endpoint
  end
  box rgba(110,200,150,0.12) 3 · Database
    participant N3a as 3a · Captures table
    participant N3b as 3b · Cards table
  end
  box rgba(200,150,240,0.12) 4 · Background worker
    participant N4a as 4a · Job loop
    participant N4b as 4b · Extraction pipeline
    participant N4c as 4c · Scraping library
    participant N4d as 4d · AI client
  end
  box rgba(245,158,11,0.12) 5 · Outside services
    participant N5a as 5a · Shared website
    participant N5b as 5b · Groq AI
    participant N5c as 5c · Expo push
  end

  You->>N1a: Tap Share in TikTok and pick Plannr
  N1a->>N2a: ① sends the link
  N2a->>N3a: ② saves it as a new capture, status waiting
  N2a-->>N1b: ③ replies at once, so a "processing" card shows
  N4a->>N3a: ④ every second, claims the next waiting capture
  N3a-->>N4a: hands back the capture's link
  N4a->>N4b: ⑤ hands the link to the pipeline
  N4b->>N4c: ⑥ asks for the page's content
  N4c->>N5a: ⑦ downloads the page
  N5a-->>N4c: sends back the page
  N4c-->>N4b: returns the page's hidden details and text
  N4b->>N4d: ⑧ sends the details still missing
  N4d->>N5b: ⑨ asks the AI to read them
  N5b-->>N4d: answers each field, with how sure it is
  N4d-->>N4b: returns the fields
  N4b-->>N4a: returns the finished card and its flags
  N4a->>N3b: ⑩ saves the card and flags, marks the capture done
  rect rgba(245, 158, 11, 0.18)
    Note over N2c,N3a: Open question, see section 4. How the server learns the worker has finished
    N2c->>N3a: ⑪ every 2 seconds, looks for newly finished captures
    N3a-->>N2c: your capture is done
    N2c->>N2d: ⑫ passes on, card ready for this person
    N2d->>N1b: ⑬ tells the open app the card is ready
  end
  N1b->>N2b: ⑭ asks for the finished card
  N2b->>N3b: ⑮ reads the card and its flags
  N3b-->>N2b: the card and its flags
  N2b-->>N1b: sends the card, unsure fields highlighted
  opt Your app is closed
    N2c->>N5c: ⑯ asks Expo to send a notification
    N5c->>N1c: ⑰ the notification appears
  end
```

| Step | Arrow | What happens |
|---|---|---|
| ① | 1a → 2a | You tap Share in TikTok (or a browser, or your photos) and pick Plannr. The app sends the link to the server. |
| ② | 2a → 3a | The server checks it's a kind of link Plannr can read, then saves it as a new **capture** with the status *waiting*. |
| ③ | 2a → 1b | The server replies straight away, without waiting for the slow part, so your wishlist shows a "processing" card at once. |
| ④ | 4a → 3a | The worker checks every second for waiting captures and claims the oldest one, so no other worker takes it too. |
| ⑤ | 4a → 4b | The job loop hands the link to the extraction pipeline. |
| ⑥ | 4b → 4c | The pipeline asks the scraping library for the page's content. |
| ⑦ | 4c → 5a | The scraping library downloads the page, using the hidden browser only if the page needs JavaScript, and returns the page's hidden event details and text. |
| ⑧ | 4b → 4d | The pipeline reads the hidden event details itself, then sends whatever is still missing to the AI client. |
| ⑨ | 4d → 5b | The AI client asks Groq to read the content and gets back each field with how sure it is. The pipeline checks and tidies the answer and adds flags. |
| ⑩ | 4a → 3b | The finished card and its flags are saved, and the capture is marked *done* (or *failed*, with a message and an error reference). |
| ⑪ | 2c → 3a | The server's completion checker looks every 2 seconds for captures that have just finished. **This is the open question in section 4.** |
| ⑫ | 2c → 2d | It passes "card ready for this person" to the live updates hub. |
| ⑬ | 2d → 1b | The hub tells your app, if it's open, that the card is ready. |
| ⑭ | 1b → 2b | The app asks the server for the finished card. |
| ⑮ | 2b → 3b | The server reads the card and its flags and sends them back; the app shows the card with unsure fields highlighted. |
| ⑯ | 2c → 5c | If your app is closed, the server asks Expo's push service to send a notification instead. |
| ⑰ | 5c → 1c | The notification appears on your phone. |

If anything fails (the page won't load, Groq is down), step ⑩ marks the
capture *failed* with a message for you and an error reference, and steps
⑪-⑰ tell your phone in the same way.

## §4 · Open question: how the server finds out a card is ready

**The problem:** the card is finished **inside the worker** (group 4), but
your phone's live connection is to the **server** (group 2). They are two
separate programs, usually on separate machines, so something has to carry
"this capture is done" from one to the other. In section 3 this is step ⑪.

```mermaid
flowchart LR
  subgraph OA["Option A · The worker tells the server directly"]
    a1["4 · Worker"] -->|"finishes a card, then calls a<br/>private server address:<br/>capture 42 is done"| a2["2 · Server"]
    a2 -->|"tells the open app"| a3["1 · Phone"]
  end
  subgraph OB["Option B · The server checks the database: recommended"]
    b1["4 · Worker"] -->|"marks capture 42 done"| b2[("3a · Captures table")]
    b3["2 · Server"] -->|"every 2 seconds:<br/>anything newly done?"| b2
    b3 -->|"tells the open app"| b4["1 · Phone"]
  end
  subgraph OC["Option C · A message service in the middle"]
    c1["4 · Worker"] -->|"posts: capture 42 is done"| c2[("Redis message service")]
    c2 -->|"passes the message on"| c3["2 · Server"]
    c3 -->|"tells the open app"| c4["1 · Phone"]
  end

  classDef pick fill:#fde68a,stroke:#b45309,stroke-width:3px,color:#1f2937
  class b3 pick
```

| | Option A: worker tells the server | **Option B: server checks the database** | Option C: message service |
|---|---|---|---|
| Delay | None | Up to 2 seconds (the card itself takes several seconds anyway) | None |
| Extra things to build | A private address on the server, plus a password so nobody else can call it | A small loop in the server | A Redis server to run and pay for |
| If the server restarts | The worker's call fails, so it needs retrying | Nothing is lost: it carries on where it left off | Messages can be lost unless stored |
| If two copies of the server run | Only the copy that got the call tells its phones | **Works:** each copy checks and tells its own phones | Works |

## §5 · Programs and libraries: where scraping lives

**The scraping library is a library, not a separate service, so nothing
talks to it through an API.** It's code built into the worker program.
The worker calls it like any other function, inside the same running
program. Separate programs, like the server and the worker, can't call each
other's functions; they meet only through the database.

```mermaid
flowchart TB
  subgraph WP["4 · Background worker: one program, running on its own"]
    w1["4a · Job loop<br/>the worker's own code"]
    subgraph LIBS["Libraries built into the worker: plain function calls, no network"]
      core["Core library<br/>the extraction pipeline"]
      scrape["Scraping library<br/>fetches pages, runs the safety guard"]
      infra["Infrastructure library<br/>AI client, database access, logging"]
    end
    w1 -->|"starts a capture:<br/>ExtractFromLinkAsync(link)"| core
    core -->|"gets the page's content:<br/>RetrieveAsync(link)"| scrape
    core -->|"asks for missing details:<br/>ExtractAsync(content)"| infra
  end

  chrome["Chromium: the hidden browser,<br/>a separate program"]
  scrape -->|"opens pages that need JavaScript in it"| chrome

  subgraph AP["2 · Server (API): a different program"]
    api["The server's own code"]
    core2["Core library"]
    infra2["Infrastructure library"]
    api -->|"checks shared links with"| core2
    api -->|"reads and writes the database with"| infra2
  end

  db[("3 · Database")]
  WP -.-|"the only place the two<br/>programs meet: both read<br/>and write the database"| db
  AP -.- db
```

**Why scraping is its own library:** the browser code is large (about
130 MB with its driver) and the server never opens a web page. Kept in its
own library that **only the worker and Eval use**:
- the server stays small and quick to deploy, with less to keep up to date;
- the server **can't** run browser code even by mistake, because it doesn't contain it, so a crashed browser can never take the server down.

## §6 · Which part may use which

Arrows point at the code being used.

```mermaid
flowchart BT
  core["Core library<br/>Plannr's rules: no outside connections"]
  infra["Infrastructure library<br/>AI, database, logging"]
  scrape["Scraping library<br/>fetching web pages"]
  api["Server program (API)"]
  worker["Worker program"]
  eval["Eval tool"]

  infra -->|"uses"| core
  scrape -->|"uses"| core
  scrape -->|"uses"| infra
  api -->|"uses"| core
  api -->|"uses"| infra
  worker -->|"uses"| core
  worker -->|"uses"| infra
  worker -->|"uses"| scrape
  eval -->|"uses"| core
  eval -->|"uses"| infra
  eval -->|"uses"| scrape
  api -.->|"not allowed to use"| scrape
```

## §7 · Outside connections behind interfaces

**The rule:** everything that reaches outside the program sits behind an
interface. The real program is set up to use the real thing; tests are set
up to use a fake, through the same dependency injection. The code in the
middle only knows the interface, so it works with either.

```mermaid
flowchart LR
  pipe["4b · Extraction pipeline<br/>knows only the interface"] -->|"asks for the missing fields through"| iface{{"ILlmExtractor<br/>the interface"}}
  iface -->|"in the real program,<br/>answered by"| real["GroqLlmExtractor<br/>calls Groq over the internet"]
  iface -->|"in tests,<br/>answered by"| fake["FakeLlm<br/>returns prepared answers,<br/>no internet"]
```

| Outside thing | Interface | Real version | Test version |
|---|---|---|---|
| Web pages | `IPageFetcher` | `HttpPageFetcher` | serves saved pages |
| The hidden browser | `IBrowserPageFetcher` | `PlaywrightPageFetcher` | returns prepared HTML |
| Groq | `IGroqApi`, `ILlmExtractor` | Groq over HTTPS | prepared answers |
| The clock | `TimeProvider` | the real clock | a clock the test moves |
| Address lookups (DNS) | `IHostResolver`, still to be added: the safety guard calls DNS directly | the real DNS | prepared addresses |
| The database | one interface per table | MySQL | in-memory tables |
| The list of waiting captures | `ICaptureQueue` | the captures table | an in-memory list |
| Telling the phone | `INotificationService` | live updates hub + Expo push | records what was sent |
| Expo push | `IExpoPushApi` | Expo over HTTPS | records what was sent |
| TikTok | `ITikTokOEmbedApi` | TikTok over HTTPS | prepared captions |
| YouTube | `IYouTubeDataApi` | YouTube over HTTPS | prepared descriptions |

## §8 · Getting the app onto your phone

**Three layers.** We write the app in TypeScript. **React Native** turns
our screens into real Android screens. **Expo** adds ready-made modules
(notifications, secure storage, share handling) and **EAS**, its free build
service, which makes the installable app file (the **APK**) so you don't
need Android Studio.

An Android app has two parts:
- a **native part**, fixed at the moment the app file is built. It includes the app's *manifest*, which tells Android what the app can do, such as "I accept shared links";
- a **JavaScript part**: our code, which can be swapped while developing.

```mermaid
flowchart TB
  ts["Our TypeScript code<br/>screens, server client, share handling"]
  rn["React Native<br/>turns our screens into real Android screens"]
  mods["Expo modules<br/>notifications, secure storage, share handling"]
  subgraph APK["The installable app file (APK)"]
    native["Native part: fixed when the file is built<br/>includes the line: I accept shared links"]
    js["JavaScript part: our code"]
  end
  eas["EAS: Expo's build servers"]
  ts -->|"is run on the phone by"| rn
  rn -->|"is packed into the file as"| js
  mods -->|"add their native pieces to"| native
  eas -->|"builds the whole file"| APK
```

**Why the share sheet decides everything.** When you tap Share in TikTok,
Android looks through every installed app's manifest for "I accept shared
text" and lists those apps. Plannr only appears if **its own** manifest
says so.

```mermaid
sequenceDiagram
  actor You
  participant TT as TikTok
  participant OS as Android
  participant PL as Plannr app
  You->>TT: Tap Share on a video
  TT->>OS: Share this text, a TikTok link
  OS->>OS: Look for installed apps whose manifest accepts shared text
  OS-->>You: Show the share sheet, Plannr included
  You->>OS: Tap Plannr
  OS->>PL: Open Plannr and hand it the link
  PL->>PL: The share handling module passes the link to our code (1a)
  PL-->>You: Show a "saving" card at once
```

**Three ways to run the app:**

```mermaid
flowchart LR
  subgraph GO["Expo Go, from the Play Store"]
    go1["Expo's own app, with Expo's manifest"]
    go2["Loads our JavaScript from the laptop"]
    go3["Can't be in the share sheet:<br/>the manifest is Expo's, not ours"]
  end
  subgraph DEV["Development build: what we need"]
    dev1["Our own app file, built once by EAS<br/>with our manifest: accepts shared links"]
    dev2["Still loads JavaScript live from the laptop:<br/>edit, save, see it on the phone in seconds"]
    dev3["Rebuilt only when native settings change"]
  end
  subgraph REL["Release build, for other people"]
    rel1["Everything inside: no laptop needed"]
    rel2["Sent to testers as a download link"]
  end

  classDef pick fill:#fde68a,stroke:#b45309,stroke-width:3px,color:#1f2937
  class dev1 pick
```

**How the development build reaches your phone:**

```mermaid
flowchart LR
  laptop["Your laptop"] -->|"eas build: uploads the project"| cloud["EAS: Expo's build servers"]
  cloud -->|"builds the app file and gives<br/>a download link or QR code"| phone["Your Android phone"]
  phone -->|"asks your permission to install<br/>an app from this source"| installed["Plannr installed"]
  laptop -->|"while we work: sends the latest<br/>JavaScript over Wi-Fi"| installed
```

## §9 · Approving changes before they reach `main`

```mermaid
flowchart LR
  b["A branch with my changes"] -->|"I open a pull request into main"| pr["Pull request"]
  pr -->|"GitHub runs the automatic checks"| ci{"Checks pass?"}
  ci -->|"no: I fix it on the branch"| b
  ci -->|"yes: waits for you"| you{"You review the changes"}
  you -->|"you ask for changes"| b
  you -->|"you click Merge: your approval"| main["main"]
  main -->|"GitHub runs the checks again,<br/>then the deploy step"| dep["Deploy step"]

  classDef pick fill:#fde68a,stroke:#b45309,stroke-width:3px,color:#1f2937
  class you pick
```

**How it's switched on:** a rule on GitHub (a *ruleset*) for `main` that
requires a pull request and passing checks, and blocks direct pushes. Only
you can create it. **GitHub never lets the author of a pull request approve
it**, and my changes go up under your GitHub login, so the pull requests
are yours. A rule requiring "1 approval" would block every merge. Instead,
**your click on Merge is the approval**: I never merge, so nothing reaches
`main` without you. Formal "Approve" reviews would need a second GitHub
account for my changes.

## §10 · Automatic checks on every change

```mermaid
flowchart LR
  push["A change is pushed<br/>or a pull request opened"] -->|"GitHub starts"| build["Build<br/>compile everything"]
  build -->|"if it compiles"| unit["Unit tests<br/>fast checks, no internet"]
  unit -->|"if they pass"| integ["Integration tests<br/>with the real hidden browser"]
  integ -->|"if they pass, on main only"| deploy["Deploy step<br/>a placeholder"]
```

Each stage runs only if the one before passed. They run on a fresh Ubuntu
24.04 machine each time.

## §11 · Measuring how accurate extraction is

```mermaid
flowchart LR
  links["Real links you collect<br/>events, web pages, TikToks, screenshots"] -->|"Eval saves each page once,<br/>because events expire"| pool[("Data pool<br/>saved pages and the right answers")]
  label["You and a second checker"] -->|"write down the right answer<br/>for each link"| pool
  pool -->|"Eval runs the same extraction<br/>as the worker on every saved page"| score["Eval: score"]
  score -->|"compares Plannr's cards with the<br/>right answers, per kind of link"| report["Accuracy report"]
```

# Repository structure

The whole of Plannr as currently understood: what exists, and what's
planned. **File-level detail for everything planned is in
[`structure.json`](structure.json)**, and is the list to move, rename or
delete from. This page is the overview.

## How the tracking works

| Kind | On disk? | What it is |
|---|---|---|
| **real** | yes | Built code. Not listed in `structure.json`; git tracks it. |
| **stub** | yes | A placeholder whose first line is `PLANNED (M#): purpose`. Comment-only, so `.cs` stubs compile to nothing. |
| **generated** | **no**, on purpose | Created later by a tool (`dotnet new`, `npx create-expo-app`, `dotnet ef`). A placeholder there would block the tool or break the build (an empty `.csproj` or `package.json` is invalid). |
| **folder** | yes | A planned folder that holds stubs. |

`StructureManifestTests` (in the normal unit-test run, so in CI) fails when:
- a file has a `PLANNED` marker but no entry (a **hanging placeholder**);
- a listed stub is missing, or its marker is gone or names a different milestone;
- a `generated` path now exists (the tool ran: remove the entry);
- an entry's `connects` points at something that's neither listed nor in the repo.

**To move, rename or delete something planned:** change the file and its
entry together (and any `connects` that name it); the test confirms nothing
was left behind. **When a stub gets built:** delete its `PLANNED` header
lines and its entry.

**Never put a placeholder in `.github/workflows/`:** GitHub reports a
comment-only workflow file as invalid on every push. The deploy stage is
the placeholder job at the end of `ci.yml`.

## The tree

`●` built · `◐` skeleton: shape built, logic marked TODO (see `guides/hand-offs.md`) · `○` planned (stubs or generated) · milestone in brackets.

```
.github/workflows/ci.yml          ● build → unit tests → integration tests → deploy (placeholder)

backend/                          .NET 10 solution (Plannr.slnx, global.json, Directory.Build.props)
  src/
    Plannr.Services/              shared by every program (Applr.Services' folders); no database, no browser
      APIs/                       ● IGroqApi, IPlannrInternalApi, IWorkerWakeApi (Refit)   ○ IExpoPushApi [M7]: the only folder that makes network calls
      DTOs/                       ● CardDraft, RetrievedContent, LlmCardFields, FieldFlag, Groq's request/response, the hand-off messages, PlannrJson
      Interfaces/                 ● retriever, LLM, pipeline, link classifier
      Services/                   ● ExtractionPipeline, GroqLlmExtractor, LinkClassifier   ○ ExpoPushClient [M7]
      Extraction/ Llm/            ● JSON-LD mapper, normaliser, date/price parsers · prompt, answer parser, rate-limit rules
      Diagnostics/ Options/       ● ErrorReference, Serilog setup, options classes, GetValidated
      Exceptions/                 ● CaptureException family   ○ EntityNotFound, NotAllowed [M3]
      DependencyInjection/        ● AddPlannrLlm, AddPlannrExtraction, the hand-off clients
      HandOffs/                   ◐ WakeSignal, SharedSecret (skeleton: TODO H1, H2)
      Planning/                   ○ DateSelector [T2]
    Plannr.Scraping/              page fetching; the Worker and Eval only, never the API
      Retrieval/                  ● HTTP + Playwright fetching, SSRF guard, HTML parser
                                  ○ rate limiter, cache [M4] · TikTok/ [M6] · BioLinkResolver, YouTube/ [T2]
      Options/ DependencyInjection/ ● RetrievalOptions · AddPlannrRetrieval, AddPlannrCapture (the whole pipeline)
    Plannr.Data/                  MySQL; the API only (the Worker goes through the API)
      Entities/                   ● Capture, CaptureStatus   ○ ActivityCard, CardFieldFlag, PlannrUser [M3] · Group, Plan, responses, DeviceToken [M7] · date options, Reminder [T2]
      Repositories/               ● ICaptureRepository, ICardRepository   ○ the EF Core repositories, CaptureRepository's claim (TODO H7.1) [M3-M7]
      Configurations/             ○ PlannrDbContext, EF mappings [M3-M4] · Migrations/ (generated)
      DependencyInjection/        ○ AddPlannrData [M3]
    Plannr.Api/                   ● exception middleware, /health, HandOffOptions   ◐ HandOffs/, HandOffsController, CaptureService (skeleton: TODO H2-H6)
                                  ○ Controllers/, Contracts/ [M3-M7] · Hubs/, SignalR notifier [M4] · JwtTokenService [M3], PlanService [M7]
    Plannr.Worker/                ◐ second program: Program.cs, WorkerOptions, HandOffs/ (skeleton: TODO H4, H6)   ○ reminders [T2]
    Plannr.Eval/                  ● extract, models   ○ snapshot, score, DataPool/ [M2]
  tests/
    Plannr.Tests/                 ● unit + browser integration tests, fixture pages, structure check, architecture tests
    Plannr.Api.IntegrationTests/  ○ generated [M3]: API + real MySQL via Testcontainers

mobile/                           Expo app (React Native, TypeScript), Android first
  app/                            ○ login, (tabs)/wishlist [M5] · (tabs)/groups, plans, join/[code] [M7] · card/[id]/ [M5]
  src/                            ○ api/ (client, ApiError; schema.d.ts generated), auth/, share/, realtime/, diagnostics/ [M5]
                                  ○ notifications/ [M7] · calendar/ [T2] · components/ [M5]
  package.json, app.json, ...     ○ generated by create-expo-app / eas [M5]

infra/                            ○ docker-compose.yml [M3] · docker/api, worker Dockerfiles [M4] · aws/README.md [T2]
data-pool/                        ○ README.md [M2]: the labelled data pool (where snapshots live is an open question)
docs/                             ● progress.md, decisions.md, structure.md (this), structure.json
```

## The programs

| Program | Kind | Runs where | Talks to |
|---|---|---|---|
| **Plannr.Api** | ASP.NET Core web API + SignalR hub | ECS Fargate (small) | MySQL (the only program that does), the phone, Expo push, the Worker (a nudge) |
| **Plannr.Worker** [M4] | .NET worker service with one private endpoint | ECS Fargate (~2 GB, Chromium) | the API's internal endpoints (claims jobs, hands back results), shared pages, Groq |
| **Plannr.Eval** | Console tool | Your laptop | Shared pages, Groq, the data pool |
| **mobile** [M5] | Expo / React Native app | Android phone | Plannr.Api (REST + SignalR) |
| **MySQL** [M3] | Database | Docker locally, RDS on AWS | |

Outside services: **Groq** (LLM), **Expo push**, **TikTok oEmbed** [M6], **YouTube Data API** [T2], **AWS** (ECR, ECS, RDS, ALB, SSM) [T2].

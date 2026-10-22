// PLANNED (M4): DNS lookups behind an interface (D40). Today PublicAddressGuard and
//   PlaywrightPageFetcher call Dns directly, the one outside connection in built code without an
//   interface; fix when scraping moves to its own project.
// Connects to: backend/src/Plannr.Infrastructure/Retrieval/PublicAddressGuard.cs,
//   backend/src/Plannr.Infrastructure/Retrieval/PlaywrightPageFetcher.cs
// Tracked in docs/structure.json; remove this header and the entry when it's built.

// PLANNED (M4): ICaptureRepository on EF Core and MySQL. Needs M3's PlannrDbContext.
// Connects to: backend/src/Plannr.Data/Repositories/ICaptureRepository.cs,
//   backend/src/Plannr.Data/Configurations/CaptureConfiguration.cs
// TODO(H7.1): TryClaimNextAsync as one atomic step, so two workers asking at once can never both
//   win. Two ways, both in docs/guides/hand-offs.md (H7): one UPDATE ... ORDER BY CreatedAt LIMIT 1
//   that marks the row as yours, then read it back; or, in a transaction, SELECT ... FOR UPDATE SKIP
//   LOCKED, then UPDATE that row. Test it against real MySQL with two claims racing.
// Tracked in docs/structure.json; remove this header and the entry when it's built.

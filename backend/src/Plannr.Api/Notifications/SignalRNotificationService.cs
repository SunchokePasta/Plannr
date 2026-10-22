// PLANNED (M4): INotificationService in the API process: hub message if the app is open, else Expo
//   push.
// Connects to: backend/src/Plannr.Api/Hubs/UpdatesHub.cs,
//   backend/src/Plannr.Infrastructure/Notifications/ExpoPushClient.cs
// Open question: How does the Worker get 'capture done' to the hub? Recommended (b): the API polls
//   Captures for newly finished rows every ~2 s (D37). Alternatives: (a) an internal API endpoint, (c)
//   a Redis backplane.
// Tracked in docs/structure.json; remove this header and the entry when it's built.

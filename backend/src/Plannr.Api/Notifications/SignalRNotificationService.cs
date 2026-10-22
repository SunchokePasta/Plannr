// PLANNED (M4): INotificationService in the API process: hub message if the app is open, else Expo
//   push.
// Connects to: backend/src/Plannr.Api/Hubs/UpdatesHub.cs,
//   backend/src/Plannr.Infrastructure/Notifications/ExpoPushClient.cs
// Open question: How does the Worker (a separate process) get 'capture done' to the hub? (a) Worker
//   calls an internal API endpoint, (b) the API polls Captures for newly Done rows, (c) a SignalR
//   backplane such as Redis.
// Tracked in docs/structure.json; remove this header and the entry when it's built.

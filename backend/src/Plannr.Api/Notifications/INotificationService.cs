using Plannr.Data.Entities;

namespace Plannr.Api.Notifications;

/// <summary>
/// Tells a user something happened: a SignalR message when their app is
/// open, an Expo push when it's closed. The real version
/// (SignalRNotificationService) comes with the hub in M4; plans and
/// responses add their own methods in M7.
/// </summary>
public interface INotificationService
{
    /// <summary>The capture finished, as a card (Done) or not (Failed).</summary>
    Task CaptureFinishedAsync(Capture capture, CancellationToken cancellationToken = default);
}

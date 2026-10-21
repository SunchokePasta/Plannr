namespace Plannr.Core.Exceptions;

/// <summary>
/// A shared item couldn't be turned into a card at all. (A card with doubtful
/// fields is not an exception -- it's a CardDraft with flags.)
///
/// Two messages, as in Applr's ApplrApiException: <see cref="Exception.Message"/>
/// is for the log and may name URLs, status codes, Groq's reply;
/// <see cref="UserMessage"/> is fixed per type and safe to put on the phone
/// as-is. The API middleware and the Worker only ever show UserMessage.
/// </summary>
public abstract class CaptureException(string message, string userMessage, Exception? innerException)
    : Exception(message, innerException)
{
    /// <summary>Safe to show a user, as-is.</summary>
    public string UserMessage { get; } = userMessage;
}

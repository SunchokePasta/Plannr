namespace Plannr.Api.Models;

/// <summary>
/// The single shape every failed request returns, written by
/// ExceptionHandlingMiddleware. The mobile app's error type reads exactly
/// these three properties -- rename one and the app falls back to a
/// generic message.
///
/// Message is the only part meant for a user. Nothing here ever carries
/// an exception message, a stack trace, a connection string or a
/// hostname -- those stay in the log line that Reference points at.
/// </summary>
public sealed class ApiErrorResponse
{
    /// <summary>Safe to show a user. Never an exception message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Matches the [Reference] tag on the logged line.</summary>
    public string Reference { get; init; } = string.Empty;

    public int Status { get; init; }
}

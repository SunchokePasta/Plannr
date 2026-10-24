using Plannr.Services.HandOffs;
using Plannr.Services.Options;

namespace Plannr.Api.Options;

/// <summary>
/// The API's side of the two hand-offs with the worker (D37), bound from
/// the "HandOffs" section. Secret never goes in a committed file: from
/// backend/src/Plannr.Api run
/// <c>dotnet user-secrets set "HandOffs:Secret" "..."</c>, with the same
/// value as the worker's "Worker:Secret".
/// </summary>
public sealed class HandOffOptions : AppOptionsBase
{
    public const string SectionName = "HandOffs";

    /// <summary>Shared with the worker; at least <see cref="SharedSecret.MinimumLength"/> characters.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Where the worker listens for its wake call.</summary>
    public string WorkerUrl { get; set; } = "http://localhost:5044";

    /// <summary>
    /// How long a nudge may take before it's given up on. Short: a nudge
    /// that doesn't arrive is covered by the worker's sweep, and the phone's
    /// request must never wait for it.
    /// </summary>
    public int NudgeTimeoutSeconds { get; set; } = 2;

    /// <summary>
    /// How long a worker may hold a capture before it's assumed to have gone
    /// quiet and the capture is claimed again. Longer than the slowest
    /// normal job (Groq's rate-limit waits included), or busy jobs would be
    /// run twice.
    /// </summary>
    public int LeaseMinutes { get; set; } = 5;

    /// <summary>How many claims a capture gets before an expired lease fails it instead.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>How often the announcer checks for finished captures nobody was told about, in case a nudge was lost.</summary>
    public int AnnouncementSweepSeconds { get; set; } = 30;

    /// <summary>The most captures announced in one go, so a backlog after an outage is worked through in steps.</summary>
    public int AnnouncementBatchSize { get; set; } = 50;

    public TimeSpan Lease => TimeSpan.FromMinutes(LeaseMinutes);

    public TimeSpan AnnouncementSweep => TimeSpan.FromSeconds(AnnouncementSweepSeconds);

    public override void Validate()
    {
        if (Secret.Length < SharedSecret.MinimumLength)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(Secret)} must be at least {SharedSecret.MinimumLength} characters (it's " +
                $"{Secret.Length}). From backend/src/Plannr.Api run: dotnet user-secrets set \"HandOffs:Secret\" \"...\" " +
                "with the same value as the worker's Worker:Secret.");
        }

        if (!Uri.TryCreate(WorkerUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(WorkerUrl)} must be an absolute http(s) URL, not '{WorkerUrl}'.");
        }

        Require(NudgeTimeoutSeconds, nameof(NudgeTimeoutSeconds), 1, 30);
        Require(LeaseMinutes, nameof(LeaseMinutes), 1, 60);
        Require(MaxAttempts, nameof(MaxAttempts), 1, 10);
        Require(AnnouncementSweepSeconds, nameof(AnnouncementSweepSeconds), 1, 600);
        Require(AnnouncementBatchSize, nameof(AnnouncementBatchSize), 1, 1000);
    }

    private static void Require(int value, string name, int min, int max)
    {
        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{SectionName}:{name} must be between {min} and {max}, not {value}.");
        }
    }
}

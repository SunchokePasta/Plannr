using Plannr.Services.HandOffs;
using Plannr.Services.Options;

namespace Plannr.Worker.Options;

/// <summary>
/// The worker's side of the two hand-offs (D37), bound from the "Worker"
/// section. Secret never goes in a committed file: from
/// backend/src/Plannr.Worker run
/// <c>dotnet user-secrets set "Worker:Secret" "..."</c>, with the same
/// value as the API's "HandOffs:Secret".
/// </summary>
public sealed class WorkerOptions : AppOptionsBase
{
    public const string SectionName = "Worker";

    /// <summary>Shared with the API; at least <see cref="SharedSecret.MinimumLength"/> characters.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Where the API's /internal endpoints are.</summary>
    public string ApiUrl { get; set; } = "http://localhost:5043";

    /// <summary>
    /// How many captures run at once. Each may open a Chromium page, so this
    /// is what decides the worker's memory use: 2 suits a 2 GB machine.
    /// </summary>
    public int Slots { get; set; } = 2;

    /// <summary>How often the worker asks for work without being nudged, in case a nudge was lost.</summary>
    public int SweepSeconds { get; set; } = 45;

    /// <summary>The longest one call to the API may take (a claim, or handing back a result).</summary>
    public int ApiTimeoutSeconds { get; set; } = 10;

    /// <summary>How many times handing back a result is tried before giving up (the lease then runs out and the capture runs again).</summary>
    public int ReportAttempts { get; set; } = 4;

    /// <summary>The wait before the first retry of handing back a result; each later one waits that much longer.</summary>
    public int ReportRetryBaseSeconds { get; set; } = 2;

    /// <summary>Stored on each capture this worker claims, so the logs say who had it.</summary>
    public string WorkerId { get; set; } = $"{Environment.MachineName}-{Environment.ProcessId}";

    public TimeSpan Sweep => TimeSpan.FromSeconds(SweepSeconds);

    public override void Validate()
    {
        if (Secret.Length < SharedSecret.MinimumLength)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(Secret)} must be at least {SharedSecret.MinimumLength} characters (it's " +
                $"{Secret.Length}). From backend/src/Plannr.Worker run: dotnet user-secrets set \"Worker:Secret\" \"...\" " +
                "with the same value as the API's HandOffs:Secret.");
        }

        if (!Uri.TryCreate(ApiUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(ApiUrl)} must be an absolute http(s) URL, not '{ApiUrl}'.");
        }

        if (string.IsNullOrWhiteSpace(WorkerId))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(WorkerId)} must be set.");
        }

        Require(Slots, nameof(Slots), 1, 16);
        Require(SweepSeconds, nameof(SweepSeconds), 1, 600);
        Require(ApiTimeoutSeconds, nameof(ApiTimeoutSeconds), 1, 120);
        Require(ReportAttempts, nameof(ReportAttempts), 1, 10);
        Require(ReportRetryBaseSeconds, nameof(ReportRetryBaseSeconds), 0, 60);
    }

    private static void Require(int value, string name, int min, int max)
    {
        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{SectionName}:{name} must be between {min} and {max}, not {value}.");
        }
    }
}

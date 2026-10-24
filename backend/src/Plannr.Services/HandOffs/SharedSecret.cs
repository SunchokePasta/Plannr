using System.Security.Cryptography;
using System.Text;

namespace Plannr.Services.HandOffs;

/// <summary>
/// Guards the private endpoints the API and the worker call on each other
/// (/internal/...). Both programs are configured with the same long random
/// value; every internal request carries it in <see cref="HeaderName"/>,
/// and the receiver refuses a request without it. It never goes in a
/// committed file (D13): user-secrets locally, SSM on AWS.
/// </summary>
public static class SharedSecret
{
    public const string HeaderName = "X-Plannr-Internal-Secret";

    /// <summary>The shortest secret either program accepts at startup.</summary>
    public const int MinimumLength = 32;

    /// <summary>
    /// True when <paramref name="presented"/> is exactly <paramref name="expected"/>.
    /// Compared in constant time: an ordinary == stops at the first wrong
    /// character, so how long it takes to say "no" would leak how much of a
    /// guess was right.
    /// </summary>
    public static bool Matches(string? presented, string expected)
    {
        // TODO(H2.1): false if either is null or empty (an unset secret must never match).
        //   Otherwise turn both into bytes (Encoding.UTF8.GetBytes) and compare them with
        //   CryptographicOperations.FixedTimeEquals, which takes the same time however many bytes agree.
        throw new NotImplementedException("TODO(H2.1): see docs/guides/hand-offs.md");
    }
}

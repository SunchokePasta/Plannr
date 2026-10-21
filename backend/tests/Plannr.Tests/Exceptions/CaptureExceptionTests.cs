using Plannr.Core.Exceptions;

namespace Plannr.Tests.Exceptions;

public sealed class CaptureExceptionTests
{
    public static TheoryData<CaptureException> AllTypes() =>
    [
        new UnsupportedLinkException("instagram.com/p/abc is not supported"),
        new ContentUnavailableException("GET https://example.com/e/1 returned 404"),
        new ExtractionFailedException("Groq returned 500: upstream error")
    ];

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void UserMessage_IsSeparateFromTheDetailedMessage(CaptureException exception)
    {
        Assert.False(string.IsNullOrWhiteSpace(exception.UserMessage));
        Assert.NotEqual(exception.Message, exception.UserMessage);

        // The detail (URL, status, Groq's reply) is only in Message, for the log.
        Assert.DoesNotContain("http", exception.UserMessage);
        Assert.DoesNotContain("Groq", exception.UserMessage);
    }

    [Fact]
    public void InnerException_IsKeptForTheLog()
    {
        var cause = new HttpRequestException("Connection refused");

        var exception = new ContentUnavailableException("GET https://example.com failed", cause);

        Assert.Same(cause, exception.InnerException);
    }
}

namespace Plannr.Core.Exceptions;

/// <summary>The LLM couldn't be reached, or its answer was unusable, after retries.</summary>
public sealed class ExtractionFailedException(string message, Exception? innerException = null)
    : CaptureException(message, "We couldn't read the details from that. You can fill the card in yourself.", innerException);

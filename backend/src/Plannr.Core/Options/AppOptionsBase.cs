namespace Plannr.Core.Options;

/// <summary>
/// Base for bindable configuration-section options classes (GroqOptions,
/// TimeoutOptions, ...). Each derived class declares its own
/// <c>public const string SectionName</c> and is bound through
/// Plannr.Infrastructure's <c>GetValidated</c>, which calls
/// <see cref="Validate"/> straight after binding.
/// </summary>
public abstract class AppOptionsBase
{
    /// <summary>
    /// Called immediately after binding, so a bad value fails at startup next
    /// to the section name rather than much later at the point of use. Throw
    /// an <see cref="InvalidOperationException"/> naming the key
    /// ("Groq:Model must be set."). Override when there's something worth
    /// checking; the base no-op is the right answer for a section whose
    /// values are all independently valid.
    /// </summary>
    public virtual void Validate()
    {
    }
}

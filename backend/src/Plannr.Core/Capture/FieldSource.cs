namespace Plannr.Core.Capture;

/// <summary>
/// Where a card field's value came from. Recorded per field so Eval can
/// report how much of each card the page's own data supplied and how much
/// the LLM did -- and so a wrong field can be traced to the step that got it wrong.
/// </summary>
public enum FieldSource
{
    /// <summary>Mapped by code from the page's schema.org JSON-LD.</summary>
    StructuredData,

    /// <summary>Read by the LLM.</summary>
    Llm,

    /// <summary>A last resort from the page's title or description tags.</summary>
    PageMetadata
}

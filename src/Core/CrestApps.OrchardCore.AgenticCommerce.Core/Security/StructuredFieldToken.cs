namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// A structured field token (RFC 8941 section 3.3.4) used as a parameter value, kept apart from an sf-string so it
/// serializes back without quotes.
/// </summary>
/// <param name="Value">The token text.</param>
public sealed record StructuredFieldToken(string Value)
{
    /// <summary>
    /// Returns the token text.
    /// </summary>
    /// <returns>The token text.</returns>
    public override string ToString()
    {
        return Value;
    }
}

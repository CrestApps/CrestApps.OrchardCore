namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// Represents a managed currency that editors can select for product and subscription pricing.
/// </summary>
public sealed class CurrencyDefinition
{
    /// <summary>
    /// Gets or sets the ISO-4217 currency code.
    /// </summary>
    public string CurrencyCode { get; init; }

    /// <summary>
    /// Gets or sets the friendly display name shown to editors.
    /// </summary>
    public string DisplayName { get; init; }
}

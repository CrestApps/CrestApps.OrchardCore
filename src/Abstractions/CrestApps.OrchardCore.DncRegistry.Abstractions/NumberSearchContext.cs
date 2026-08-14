namespace CrestApps.OrchardCore.DncRegistry;

/// <summary>
/// Provides additional filtering criteria when querying a do-not-call registry.
/// </summary>
public sealed class NumberSearchContext
{
    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code to filter by.
    /// When <see langword="null"/> or empty, all countries are searched.
    /// </summary>
    public string CountryCode { get; set; }
}

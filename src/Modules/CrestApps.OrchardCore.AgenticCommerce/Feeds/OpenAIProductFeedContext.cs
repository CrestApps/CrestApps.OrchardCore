namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// What every item of the OpenAI product feed says about the store.
/// </summary>
public sealed class OpenAIProductFeedContext
{
    /// <summary>
    /// Gets or sets the two-letter code of the store's country, or <see langword="null"/>.
    /// </summary>
    public string StoreCountry { get; set; }

    /// <summary>
    /// Gets or sets the two-letter codes of the countries the store sells to through agents.
    /// </summary>
    public IReadOnlyList<string> TargetCountries { get; set; } = [];
}

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// An agent platform that hosts an Agentic Commerce Protocol (ACP) feed API, which the store pushes its products to.
/// </summary>
public sealed class AcpFeedTarget
{
    /// <summary>
    /// Gets or sets the target's id, which its pushes are recorded by.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name the store knows the platform by.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the base address of the platform's feed API, such as <c>https://api.example.com/acp</c>.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the platform's bearer key, protected with <see cref="AgentFeedSettings.ProtectionPurpose"/>.
    /// </summary>
    public string BearerToken { get; set; }

    /// <summary>
    /// Gets or sets the id of the feed on the platform, or none to have the store create one.
    /// </summary>
    public string FeedId { get; set; }

    /// <summary>
    /// Gets or sets the two-letter code of the country the feed is for, or none.
    /// </summary>
    public string TargetCountry { get; set; }

    /// <summary>
    /// Gets or sets the ISO 4217 code of the currency the products are priced in, or none for each product's own.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets whether products are pushed to the platform.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

namespace CrestApps.OrchardCore.Markets;

public static class MarketsConstants
{
    public const string Feature = "CrestApps.OrchardCore.Markets";

    /// <summary>
    /// The YesSql collection markets live in.
    /// </summary>
    public const string CollectionName = "Markets";

    /// <summary>
    /// The cookie that keeps the market a shopper picked.
    /// </summary>
    public const string CookieName = "orchard-market";

    /// <summary>
    /// The query string value and request header an app names a market with, by its id or a country code.
    /// </summary>
    public const string QueryName = "market";

    public const string HeaderName = "X-Market";
}

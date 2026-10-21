namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Why an item would be refused, or was left out of the feed.
/// </summary>
public sealed record ProductFeedProblem(string ItemId, string Title, string Problem, bool LeftOut);

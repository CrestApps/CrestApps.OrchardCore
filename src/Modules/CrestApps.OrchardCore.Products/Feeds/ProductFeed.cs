namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// A feed and why the products left out of it were.
/// </summary>
public sealed record ProductFeed(IReadOnlyList<ProductFeedItem> Items, IReadOnlyList<ProductFeedProblem> Problems, DateTime BuiltUtc);

using CrestApps.OrchardCore.Products.Core.Feeds;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// The OpenAI product feed: the records in it, and why the items left out of it were.
/// </summary>
/// <param name="Records">The records in the feed.</param>
/// <param name="Problems">Why items were left out: the store's product feed's reasons, then OpenAI's rules.</param>
/// <param name="BuiltUtc">When the store's product feed it comes from was built.</param>
public sealed record OpenAIProductFeed(IReadOnlyList<OpenAIProductFeedRecord> Records, IReadOnlyList<ProductFeedProblem> Problems, DateTime BuiltUtc);

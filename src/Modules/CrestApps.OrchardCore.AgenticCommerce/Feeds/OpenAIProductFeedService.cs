using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Products.Core.Feeds;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Makes the OpenAI product feed from the store's product feed, which it is kept and rebuilt with: OpenAI's fields for
/// every item, less the items OpenAI's rules would reject.
/// </summary>
public sealed class OpenAIProductFeedService
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(15);

    private readonly IProductFeedService _productFeeds;
    private readonly ISiteService _siteService;
    private readonly IMemoryCache _cache;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIProductFeedService"/> class.
    /// </summary>
    /// <param name="productFeeds">The store's product feed.</param>
    /// <param name="siteService">The site service, for the store profile and the agent feed settings.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="shellSettings">The tenant's settings.</param>
    public OpenAIProductFeedService(
        IProductFeedService productFeeds,
        ISiteService siteService,
        IMemoryCache cache,
        ShellSettings shellSettings)
    {
        _productFeeds = productFeeds;
        _siteService = siteService;
        _cache = cache;
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Gets the feed priced in a currency.
    /// </summary>
    /// <param name="currency">The ISO 4217 code of the currency, or <see langword="null"/> for each product's own.</param>
    /// <param name="fresh">Whether to build the store's product feed now instead of reading the copy kept.</param>
    /// <param name="cancellationToken">A token that cancels the work.</param>
    public async Task<OpenAIProductFeed> GetAsync(string currency, bool fresh = false, CancellationToken cancellationToken = default)
    {
        var feed = await _productFeeds.GetAsync(currency, fresh, cancellationToken);
        var context = await GetContextAsync();
        var key = $"OpenAIProductFeed:{_shellSettings.Name}:{currency?.ToUpperInvariant()}:{context.StoreCountry}:{string.Join(',', context.TargetCountries)}";

        // The store's feed is kept and dropped on its own; this one is made again whenever that one was built again.
        if (_cache.TryGetValue(key, out OpenAIProductFeed cached) && cached.BuiltUtc == feed.BuiltUtc)
        {
            return cached;
        }

        var built = Build(feed, context);

        _cache.Set(key, built, _lifetime);

        return built;
    }

    /// <summary>
    /// Gets what every item says about the store: its country, and the countries it sells to through agents.
    /// </summary>
    public async Task<OpenAIProductFeedContext> GetContextAsync()
    {
        var profile = await _siteService.GetStoreProfileAsync();
        var settings = await _siteService.GetSettingsAsync<AgentFeedSettings>();
        var storeCountry = profile.Address?.Country?.Trim().ToUpperInvariant();
        var targets = (settings.TargetCountries ?? [])
            .Where(country => !string.IsNullOrWhiteSpace(country))
            .Select(country => country.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (targets.Count == 0 && !string.IsNullOrEmpty(storeCountry))
        {
            targets.Add(storeCountry);
        }

        return new OpenAIProductFeedContext
        {
            StoreCountry = string.IsNullOrEmpty(storeCountry) ? null : storeCountry,
            TargetCountries = targets,
        };
    }

    /// <summary>
    /// Makes the OpenAI feed from the store's product feed: the items OpenAI's rules accept, and why the others, and
    /// the ones the product feed left out, were left out.
    /// </summary>
    /// <param name="feed">The store's product feed.</param>
    /// <param name="context">What every item says about the store.</param>
    public static OpenAIProductFeed Build(ProductFeed feed, OpenAIProductFeedContext context)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(context);

        var records = new List<OpenAIProductFeedRecord>();
        var problems = feed.Problems.Where(problem => problem.LeftOut).ToList();

        foreach (var item in feed.Items)
        {
            var record = OpenAIProductFeedWriter.ToRecord(item, context);
            var found = OpenAIProductFeedValidator.Validate(record);

            if (found.Count > 0)
            {
                problems.AddRange(found.Select(problem => new ProductFeedProblem(item.Id, item.Title, problem, LeftOut: true)));

                continue;
            }

            records.Add(record);
        }

        return new OpenAIProductFeed(records, problems, feed.BuiltUtc);
    }
}

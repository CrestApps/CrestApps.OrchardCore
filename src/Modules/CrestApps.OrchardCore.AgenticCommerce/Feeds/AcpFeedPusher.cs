using CrestApps.Core.Support;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Products.Core.Feeds;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Documents;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Keeps the agent platforms that host an ACP feed API up to date: it creates the store's feed on a platform the first
/// time, then upserts complete products, all of them in the daily push and the changed ones after an edit or a stock
/// change. Only the items AI agents may show are sent.
/// </summary>
public sealed class AcpFeedPusher
{
    /// <summary>
    /// The most products one upsert carries.
    /// </summary>
    public const int BatchSize = 100;

    private readonly ISiteService _siteService;
    private readonly IProductFeedService _productFeeds;
    private readonly AcpFeedApiClient _client;
    private readonly IDocumentManager<AgentFeedStatusDocument> _statusManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AcpFeedPusher"/> class.
    /// </summary>
    /// <param name="siteService">The site service, for the agent feed settings.</param>
    /// <param name="productFeeds">The store's product feed.</param>
    /// <param name="client">The ACP feed API client.</param>
    /// <param name="statusManager">The document pushes and created feeds are recorded in.</param>
    /// <param name="dataProtectionProvider">The tenant's data protection, for the stored bearer keys.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AcpFeedPusher(
        ISiteService siteService,
        IProductFeedService productFeeds,
        AcpFeedApiClient client,
        IDocumentManager<AgentFeedStatusDocument> statusManager,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        ILogger<AcpFeedPusher> logger)
    {
        _siteService = siteService;
        _productFeeds = productFeeds;
        _client = client;
        _statusManager = statusManager;
        _dataProtectionProvider = dataProtectionProvider;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Returns whether any feed API target is enabled.
    /// </summary>
    public async Task<bool> HasTargetsAsync()
        => GetTargets(await _siteService.GetSettingsAsync<AgentFeedSettings>()).Any();

    /// <summary>
    /// Pushes every product AI agents may show to every enabled target, and returns how each push went, by target id.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the push.</param>
    public async Task<IReadOnlyDictionary<string, AgentFeedPushResult>> PushAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, AgentFeedPushResult>(StringComparer.Ordinal);

        foreach (var target in GetTargets(await _siteService.GetSettingsAsync<AgentFeedSettings>()))
        {
            var feed = await _productFeeds.GetAsync(target.Currency, fresh: true, cancellationToken);

            results[target.Id] = await PushAsync(target, feed.Items, cancellationToken);
        }

        return results;
    }

    /// <summary>
    /// Pushes some products, such as the ones just published or whose stock changed, to every enabled target.
    /// </summary>
    /// <param name="contentItemIds">The content item ids of the products.</param>
    /// <param name="cancellationToken">A token that cancels the push.</param>
    public async Task PushProductsAsync(IReadOnlyCollection<string> contentItemIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentItemIds);

        if (contentItemIds.Count == 0)
        {
            return;
        }

        foreach (var target in GetTargets(await _siteService.GetSettingsAsync<AgentFeedSettings>()))
        {
            var feed = await _productFeeds.BuildAsync(contentItemIds, target.Currency, cancellationToken);

            if (feed.Items.Count > 0)
            {
                await PushAsync(target, feed.Items, cancellationToken);
            }
        }
    }

    private async Task<AgentFeedPushResult> PushAsync(AcpFeedTarget target, IEnumerable<ProductFeedItem> items, CancellationToken cancellationToken)
    {
        var result = new AgentFeedPushResult { StartedUtc = _clock.UtcNow };

        try
        {
            var token = _dataProtectionProvider.UnprotectSettingOrDefault(AgentFeedSettings.ProtectionPurpose, target.BearerToken, _logger, "ACP feed API key");

            if (string.IsNullOrEmpty(token))
            {
                result.Message = "The feed API key isn't set.";

                return result;
            }

            var feedId = await GetFeedIdAsync(target, token, cancellationToken);
            var shown = items.Where(item => item.ShowToAgents).ToList();
            var products = AcpFeedProductMapper.ToProducts(shown, _clock.UtcNow);

            foreach (var batch in products.Chunk(BatchSize))
            {
                // The key comes from what is sent, so a retry of the same batch is the same request.
                var body = AcpFeedApiClient.CreateUpsertBody(batch);
                var key = AcpFeedApiClient.CreateIdempotencyKey("upsert", target.Id, feedId, body.ToJsonString());

                await _client.UpsertProductsAsync(target.BaseUrl, token, feedId, batch, key, cancellationToken);
            }

            result.ItemCount = shown.Count;
            result.Succeeded = true;
            result.Message = $"{products.Count} products, {shown.Count} variants.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The products couldn't be pushed to the ACP feed API '{Target}'.", target.Name.SanitizeLogValue());
            result.Message = exception.Message;
        }
        finally
        {
            result.FinishedUtc = _clock.UtcNow;

            var status = await _statusManager.GetOrCreateMutableAsync();

            status.FeedApiPushes ??= new(StringComparer.Ordinal);
            status.FeedApiPushes[target.Id] = result;

            await _statusManager.UpdateAsync(status);
        }

        return result;
    }

    private async Task<string> GetFeedIdAsync(AcpFeedTarget target, string token, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(target.FeedId))
        {
            return target.FeedId.Trim();
        }

        var status = await _statusManager.GetOrCreateImmutableAsync();

        if (status.FeedApiFeedIds?.TryGetValue(target.Id, out var created) == true && !string.IsNullOrEmpty(created))
        {
            return created;
        }

        var feedId = await _client.CreateFeedAsync(target.BaseUrl, token, target.TargetCountry, AcpFeedApiClient.CreateIdempotencyKey("create-feed", target.Id, target.BaseUrl), cancellationToken);
        var mutable = await _statusManager.GetOrCreateMutableAsync();

        mutable.FeedApiFeedIds ??= new(StringComparer.Ordinal);
        mutable.FeedApiFeedIds[target.Id] = feedId;

        await _statusManager.UpdateAsync(mutable);

        return feedId;
    }

    private static IEnumerable<AcpFeedTarget> GetTargets(AgentFeedSettings settings)
        => (settings.FeedApiTargets ?? [])
            .Where(target => target.Enabled && !string.IsNullOrEmpty(target.Id) && !string.IsNullOrWhiteSpace(target.BaseUrl));
}

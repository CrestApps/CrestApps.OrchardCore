using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Locking.Distributed;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Synchronizes subscription content item prices and products with Stripe.
/// </summary>
public sealed class StripePriceSyncService
{
    private const int _batchSize = 500;

    // Bounds how many Stripe write calls run at once so a large catalog does not fire hundreds of
    // simultaneous requests and trip Stripe's per-second rate limits. Combined with the client's
    // automatic retry/backoff this keeps the sync reliable.
    private const int _maxConcurrency = 8;

    private const string _syncLockKey = "STRIPE_PRICE_SYNC_LOCK";

    private readonly ISiteService _siteService;
    private readonly IStripeProductService _stripeProductService;
    private readonly IStripePriceService _stripePriceService;
    private readonly ISession _session;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IDistributedLock _distributedLock;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripePriceSyncService"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to load subscription settings.</param>
    /// <param name="stripeProductService">The Stripe product service used to read and create products.</param>
    /// <param name="stripePriceService">The Stripe price service used to read, create, and update prices.</param>
    /// <param name="session">The YesSql session used to query content item indexes.</param>
    /// <param name="contentDefinitionManager">The content definition manager used to load subscription content type definitions.</param>
    /// <param name="distributedLock">The distributed lock service used to serialize full price synchronization.</param>
    public StripePriceSyncService(
        ISiteService siteService,
        IStripeProductService stripeProductService,
        IStripePriceService stripePriceService,
        ISession session,
        IContentDefinitionManager contentDefinitionManager,
        IDistributedLock distributedLock)
    {
        _siteService = siteService;
        _stripeProductService = stripeProductService;
        _stripePriceService = stripePriceService;
        _session = session;
        _contentDefinitionManager = contentDefinitionManager;
        _distributedLock = distributedLock;
    }

    /// <summary>
    /// Creates or updates the Stripe price for a subscription content item using its content type definition.
    /// </summary>
    /// <param name="contentItem">The subscription content item to synchronize with Stripe.</param>
    /// <returns>A task that represents the asynchronous synchronization operation.</returns>
    public async Task UpdateOrCreateAsync(ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        if (definition == null)
        {
            return;
        }

        await UpdateOrCreateAsync(contentItem, definition);
    }

    /// <summary>
    /// Creates or updates the Stripe price for a subscription content item when the supplied definition supports subscriptions.
    /// </summary>
    /// <param name="contentItem">The subscription content item to synchronize with Stripe.</param>
    /// <param name="definition">The content type definition for the content item.</param>
    /// <param name="currency">The ISO currency code to use, or <see langword="null"/> to use the site subscription settings.</param>
    /// <returns>A task that represents the asynchronous synchronization operation.</returns>
    public async Task UpdateOrCreateAsync(ContentItem contentItem, ContentTypeDefinition definition, string currency = null)
    {
        ArgumentNullException.ThrowIfNull(contentItem);
        ArgumentNullException.ThrowIfNull(definition);

        if (!definition.StereotypeEquals(SubscriptionConstants.Stereotype) ||
            !contentItem.TryGet<SubscriptionPart>(out var subscriptionPart) ||
            !contentItem.TryGet<ProductPart>(out var productPart))
        {
            return;
        }

        var price = await _stripePriceService.GetAsync(contentItem.ContentItemVersionId);

        if (price != null)
        {
            await _stripePriceService.UpdateAsync(contentItem.ContentItemVersionId, new UpdatePriceRequest()
            {
                Title = contentItem.DisplayText,
                IsActive = true,
            });

            return;
        }

        var product = await CreateProductIfNotExistsAsync(definition);

        if (string.IsNullOrEmpty(currency))
        {
            var settings = await _siteService.GetSettingsAsync<SubscriptionSettings>();
            currency = settings.Currency;
        }

        var priceRequest = new CreatePriceRequest()
        {
            LookupKey = contentItem.ContentItemVersionId,
            ProductId = product.Id,
            Title = contentItem.DisplayText,
            Amount = productPart.Price,
            Currency = currency,
            IntervalCount = subscriptionPart.BillingDuration,
            Interval = subscriptionPart.DurationType.ToString().ToLowerInvariant(),
        };

        await _stripePriceService.CreateAsync(priceRequest);
    }

    /// <summary>
    /// Marks the Stripe price for the specified content item as inactive when the price exists.
    /// </summary>
    /// <param name="contentItem">The content item whose Stripe price should be inactivated.</param>
    /// <returns>A task that represents the asynchronous unpublish operation.</returns>
    public async Task UnpublishAsync(ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        var price = await _stripePriceService.GetAsync(contentItem.ContentItemVersionId);

        if (price == null)
        {
            return;
        }

        await _stripePriceService.UpdateAsync(contentItem.ContentItemVersionId, new UpdatePriceRequest()
        {
            Title = contentItem.DisplayText,
            IsActive = false,
        });
    }

    /// <summary>
    /// Synchronizes all published subscription content item prices with Stripe.
    /// </summary>
    /// <param name="currency">The ISO currency code to use, or <see langword="null"/> to use the site subscription settings.</param>
    /// <returns>A task that represents the asynchronous synchronization operation.</returns>
    public async Task CreateOrUpdateAllAsync(string currency = null)
    {
        // Only one full synchronization may run at a time across all instances. Two concurrent syncs
        // would read each other's partial Stripe state and recreate or deactivate prices incorrectly.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
            _syncLockKey,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(30));

        if (!locked)
        {
            // Another synchronization is already in progress; skip rather than duplicate work.
            return;
        }

        await using (locker)
        {
            await CreateOrUpdateAllInternalAsync(currency);
        }
    }

    private async Task CreateOrUpdateAllInternalAsync(string currency)
    {
        var existingPrices = await _stripePriceService.ListAsync();

        var lookupIds = existingPrices.Where(x => !string.IsNullOrEmpty(x.LookupKey))
            .Select(x => x.LookupKey)
            .ToArray();

        if (lookupIds.Length > 0)
        {
            await InactivateOldPriceItemsAsync(lookupIds);
        }

        var definitions = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
           .Where(x => x.StereotypeEquals(SubscriptionConstants.Stereotype))
           .ToArray();

        if (definitions.Length == 0)
        {
            return;
        }

        await RunBoundedAsync(definitions, definition => CreateProductIfNotExistsAsync(definition));

        var contentTypes = definitions
            .Select(x => x.Name)
            .ToArray();

        await CreateMissingPriceItemsAsync(lookupIds, contentTypes, currency);
    }

    /// <summary>
    /// Schedules synchronization of all subscription prices with Stripe after the current HTTP request ends.
    /// </summary>
    /// <returns>A task that represents scheduling the background synchronization work.</returns>
    public static Task SyncAllPricesInBackground()
    {
        return HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("sync-content-items-with-stripe", (scope) =>
        {
            var stripePriceSyncService = scope.ServiceProvider.GetService<StripePriceSyncService>();

            if (stripePriceSyncService == null)
            {
                return Task.CompletedTask;
            }

            return stripePriceSyncService.CreateOrUpdateAllAsync();
        });
    }

    private async Task InactivateOldPriceItemsAsync(string[] lookupIds)
    {
        // Retrieve indexes where the versionId matches the price ID and the version is still published.
        // Any lookup ID not found in this list indicates it was deactivated.
        var existingIndexes = (await _session.QueryIndex<ContentItemIndex>(x => x.ContentItemVersionId.IsIn(lookupIds) && x.Published).ListAsync())
            .ToDictionary(x => x.ContentItemVersionId);

        var toDeactivate = lookupIds
            .Where(lookupId => !existingIndexes.ContainsKey(lookupId))
            .ToArray();

        await RunBoundedAsync(toDeactivate, lookupId => _stripePriceService.UpdateAsync(lookupId, new UpdatePriceRequest()
        {
            IsActive = false,
        }));
    }

    private async Task CreateMissingPriceItemsAsync(string[] existingLookupIds, string[] contentTypes, string currency)
    {
        if (string.IsNullOrEmpty(currency))
        {
            var settings = await _siteService.GetSettingsAsync<SubscriptionSettings>();
            currency = settings.Currency;
        }

        var batchCount = 0;

        while (true)
        {
            // Retrieve published content items that do not exist in Stripe.
            var contentItems = await _session.Query<ContentItem, ContentItemIndex>(x => x.ContentType.IsIn(contentTypes) && x.ContentItemVersionId.IsNotIn(existingLookupIds) && x.Published)
                .OrderBy(x => x.Id)
                .Skip(_batchSize * batchCount++)
                .Take(_batchSize)
                .ListAsync();

            if (!contentItems.Any())
            {
                break;
            }

            var priceItems = new List<(ContentItem ContentItem, SubscriptionPart Subscription, ProductPart Product)>();

            foreach (var contentItem in contentItems)
            {
                if (contentItem.TryGet<SubscriptionPart>(out var subscriptionPart) &&
                    contentItem.TryGet<ProductPart>(out var productPart))
                {
                    priceItems.Add((contentItem, subscriptionPart, productPart));
                }
            }

            await RunBoundedAsync(priceItems, item => _stripePriceService.CreateAsync(new CreatePriceRequest()
            {
                LookupKey = item.ContentItem.ContentItemVersionId,
                ProductId = item.ContentItem.ContentType,
                Title = item.ContentItem.DisplayText,
                Amount = item.Product.Price,
                Currency = currency,
                IntervalCount = item.Subscription.BillingDuration,
                Interval = item.Subscription.DurationType.ToString().ToLowerInvariant(),
            }));
        }
    }

    // Runs an asynchronous action over a set of items with a bounded degree of parallelism so bulk
    // Stripe writes never exceed the provider's rate limits. The first failure is surfaced to the caller.
    private static async Task RunBoundedAsync<T>(IEnumerable<T> items, Func<T, Task> action)
    {
        using var semaphore = new SemaphoreSlim(_maxConcurrency);
        var tasks = new List<Task>();

        foreach (var item in items)
        {
            await semaphore.WaitAsync();

            tasks.Add(ProcessAsync(item));
        }

        await Task.WhenAll(tasks);

        async Task ProcessAsync(T item)
        {
            try
            {
                await action(item);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    private async Task<ProductResponse> CreateProductIfNotExistsAsync(ContentTypeDefinition definition)
    {
        var product = await _stripeProductService.GetAsync(definition.Name);

        if (product != null)
        {
            return product;
        }

        var productPartSettings = definition.Parts.FirstOrDefault(x => x.Name == nameof(ProductPart))?.GetSettings<ProductPartSettings>();

        var productRequest = new CreateProductRequest()
        {
            Id = definition.Name,
            Title = definition.DisplayName,
            Description = definition.GetDescription(),
            Type = productPartSettings?.Type ?? ProductType.Service,
        };

        return await _stripeProductService.CreateAsync(productRequest);
    }
}

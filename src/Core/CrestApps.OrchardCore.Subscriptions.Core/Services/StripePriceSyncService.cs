using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

public class StripePriceSyncService
{
    private const int _batchSize = 500;

    private readonly ISiteService _siteService;
    private readonly IStripeProductService _stripeProductService;
    private readonly IStripePriceService _stripePriceService;
    private readonly ISession _session;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public StripePriceSyncService(
        ISiteService siteService,
        IStripeProductService stripeProductService,
        IStripePriceService stripePriceService,
        ISession session,
        IContentDefinitionManager contentDefinitionManager)
    {
        _siteService = siteService;
        _stripeProductService = stripeProductService;
        _stripePriceService = stripePriceService;
        _session = session;
        _contentDefinitionManager = contentDefinitionManager;
    }

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

    public async Task UpdateOrCreateAsync(ContentItem contentItem, ContentTypeDefinition definition, string currency = null)
    {
        ArgumentNullException.ThrowIfNull(contentItem);
        ArgumentNullException.ThrowIfNull(definition);

        if (!definition.StereotypeEquals(SubscriptionConstants.Stereotype) ||
            !contentItem.TryGet<SubscriptionPart>(out var subscriptionPart))
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
            Amount = subscriptionPart.BillingAmount,
            Currency = currency,
            IntervalCount = subscriptionPart.BillingDuration,
            Interval = subscriptionPart.DurationType.ToString().ToLowerInvariant(),
        };

        await _stripePriceService.CreateAsync(priceRequest);
    }

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

    public async Task CreateOrUpdateAllAsync(string currency = null)
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
           .Where(x => x.StereotypeEquals(SubscriptionConstants.Stereotype));

        if (!definitions.Any())
        {
            return;
        }

        var productTasks = new List<Task>();

        foreach (var definition in definitions)
        {
            productTasks.Add(CreateProductIfNotExistsAsync(definition));
        }

        await Task.WhenAll(productTasks);

        var contentTypes = definitions
            .Select(x => x.Name)
            .ToArray();

        await CreateMissingPriceItemsAsync(lookupIds, contentTypes, currency);
    }

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

        var priceUpdateTasks = new List<Task>();

        foreach (var lookupId in lookupIds)
        {
            if (existingIndexes.ContainsKey(lookupId))
            {
                continue;
            }

            var task = _stripePriceService.UpdateAsync(lookupId, new UpdatePriceRequest()
            {
                IsActive = false,
            });

            priceUpdateTasks.Add(task);
        }

        await Task.WhenAll(priceUpdateTasks);
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

            var createPriceTasks = new List<Task>();

            foreach (var contentItem in contentItems)
            {
                if (!contentItem.TryGet<SubscriptionPart>(out var subscriptionPart))
                {
                    continue;
                }

                var createPriceTask = _stripePriceService.CreateAsync(new CreatePriceRequest()
                {
                    LookupKey = contentItem.ContentItemVersionId,
                    ProductId = contentItem.ContentType,
                    Title = contentItem.DisplayText,
                    Amount = subscriptionPart.BillingAmount,
                    Currency = currency,
                    IntervalCount = subscriptionPart.BillingDuration,
                    Interval = subscriptionPart.DurationType.ToString().ToLowerInvariant(),
                });

                createPriceTasks.Add(createPriceTask);
            }

            await Task.WhenAll(createPriceTasks);
        }
    }

    private async Task<ProductResponse> CreateProductIfNotExistsAsync(ContentTypeDefinition definition)
    {
        var product = await _stripeProductService.GetAsync(definition.Name);

        if (product != null)
        {
            return product;
        }

        var productRequest = new CreateProductRequest()
        {
            Id = definition.Name,
            Title = definition.DisplayName,
            Description = definition.GetDescription(),
            Type = "service",
        };

        return await _stripeProductService.CreateAsync(productRequest);
    }
}

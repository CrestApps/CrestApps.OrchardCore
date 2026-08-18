using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Shared builders for exercising the subscription payment pipeline against real, in-memory
/// infrastructure rather than mocks, so the caching/serialization behavior is covered as well.
/// </summary>
internal static class PaymentTestHelpers
{
    /// <summary>
    /// Creates a real <see cref="DefaultProductSnapshotResolver"/> over a content definition that attaches a
    /// <see cref="ProductPart"/> carrying the supplied default currency, so a plan content item resolves to a
    /// snapshot with its own unit price and a product-owned currency, exercising the pricing seam end to end.
    /// </summary>
    /// <param name="defaultCurrency">The content type default currency applied when a product declares none.</param>
    public static IProductSnapshotResolver CreateProductSnapshotResolver(string defaultCurrency = "USD")
    {
        var settings = new JsonObject
        {
            [nameof(ProductPartSettings)] = JsonSerializer.SerializeToNode(new ProductPartSettings
            {
                Type = ProductType.Service,
                DefaultCurrency = defaultCurrency,
            }),
        };

        var partDefinition = new ContentTypePartDefinition(
            nameof(ProductPart),
            new ContentPartDefinition(nameof(ProductPart), [], []),
            settings);

        var typeDefinition = new ContentTypeDefinition("Plan", "Plan", [partDefinition], []);
        partDefinition.ContentTypeDefinition = typeDefinition;

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(x => x.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync(typeDefinition);

        return new DefaultProductSnapshotResolver(contentDefinitionManager.Object);
    }

    /// <summary>
    /// Creates a <see cref="SubscriptionPaymentSession"/> backed by an in-memory distributed cache and a
    /// lock that is always granted, which is representative of the single-instance (local lock) deployment.
    /// </summary>
    public static SubscriptionPaymentSession CreatePaymentSession(string tenantName = "Default")
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        var options = Options.Create(new SubscriptionPaymentSessionOptions
        {
            MaxLiveSession = TimeSpan.FromMinutes(30),
        });

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        var shellSettings = new ShellSettings
        {
            Name = tenantName,
        };

        return new SubscriptionPaymentSession(cache, options, distributedLock.Object, shellSettings);
    }

    private sealed class NoopLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}

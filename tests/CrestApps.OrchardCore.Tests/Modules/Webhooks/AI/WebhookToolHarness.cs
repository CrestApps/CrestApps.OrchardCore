using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Models;
using CrestApps.OrchardCore.Webhooks.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks.AI;

/// <summary>
/// Builds what a webhook tool needs: the subscription and delivery stores in memory, the real dispatcher with a
/// throw-away data protection key and a sender that records what it was asked to post, the real admin commands, an event
/// catalog with two order events, and a production host.
/// </summary>
internal sealed class WebhookToolHarness
{
    public static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private readonly EphemeralDataProtectionProvider _dataProtection = new();

    public WebhookToolHarness()
    {
        Dispatcher = new WebhookDispatcher(
            Sender().Object,
            DeliveryStore().Object,
            SubscriptionStore().Object,
            [],
            _dataProtection,
            Clock(),
            NullLogger<WebhookDispatcher>.Instance);
    }

    public Dictionary<string, WebhookSubscription> Subscriptions { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, WebhookDelivery> Deliveries { get; } = new(StringComparer.Ordinal);

    public List<WebhookSendRequest> Sent { get; } = [];

    public WebhookSendResult SendResult { get; set; } = new() { Succeeded = true, StatusCode = 200 };

    public WebhookDispatcher Dispatcher { get; }

    public FakeAuthorizationService Authorization { get; private set; }

    /// <summary>
    /// Adds a webhook for order.paid with a signing secret.
    /// </summary>
    public WebhookSubscription AddWebhook(string id = "w1", string url = "https://erp.example.com/hooks")
    {
        var webhook = new WebhookSubscription
        {
            ItemId = id,
            Name = "ERP",
            Url = url,
            ProtectedSecret = Dispatcher.NewProtectedSecret(),
            Events = ["order.paid"],
            CreatedUtc = Now.AddDays(-1),
            ModifiedUtc = Now.AddDays(-1),
        };

        Subscriptions[id] = webhook;

        return webhook;
    }

    public WebhookDelivery AddDelivery(string id, string webhookId, WebhookDeliveryStatus status)
    {
        var delivery = new WebhookDelivery
        {
            ItemId = id,
            SubscriptionId = webhookId,
            EventId = "evt_" + id,
            EventName = "order.paid",
            Payload = "{\"secretPayload\":true}",
            Status = status,
            Attempts = status == WebhookDeliveryStatus.Pending ? 0 : 8,
            LastStatusCode = status == WebhookDeliveryStatus.Failed ? 500 : null,
            LastError = status == WebhookDeliveryStatus.Failed ? "Server error" : null,
            CreatedUtc = Now.AddHours(-1),
        };

        Deliveries[id] = delivery;

        return delivery;
    }

    /// <summary>
    /// Builds the services of a tool call.
    /// </summary>
    /// <param name="signedIn">Whether the caller is signed in.</param>
    /// <param name="granted">Whether the caller holds the webhooks permission.</param>
    public ServiceProvider Build(bool signedIn = true, bool granted = true)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, (permission, _) => granted && permission.Name == WebhookPermissions.ManageWebhooks.Name);
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(environment => environment.EnvironmentName).Returns(Environments.Production);

        services.AddSingleton(SubscriptionStore().Object);
        services.AddSingleton(DeliveryStore().Object);
        services.AddSingleton(Dispatcher);
        services.AddSingleton(Events());
        services.AddSingleton(hostEnvironment.Object);
        services.AddSingleton(Clock());
        services.AddSingleton(typeof(Microsoft.Extensions.Localization.IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton<WebhookSubscriptionCommands>();
        services.AddSingleton(LinkGenerator());

        var provider = services.BuildServiceProvider();

        Authorization = provider.GetRequiredService<FakeAuthorizationService>();

        return provider;
    }

    private static IClock Clock()
        => Mock.Of<IClock>(clock => clock.UtcNow == Now);

    private static IWebhookEventCatalog Events()
    {
        var events = new List<WebhookEventDescriptor>
        {
            new("order.created", "Order created", "Orders"),
            new("order.paid", "Order paid", "Orders"),
        };
        var catalog = new Mock<IWebhookEventCatalog>();

        catalog.Setup(value => value.GetEvents()).Returns(events);
        catalog.Setup(value => value.Find(It.IsAny<string>())).Returns((string name) => events.Find(entry => entry.Name == name));

        return catalog.Object;
    }

    private Mock<IWebhookSender> Sender()
    {
        var sender = new Mock<IWebhookSender>();

        sender
            .Setup(value => value.SendAsync(It.IsAny<WebhookSendRequest>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookSendRequest, CancellationToken>((request, _) => Sent.Add(request))
            .ReturnsAsync(() => SendResult);

        return sender;
    }

    private Mock<IWebhookSubscriptionStore> SubscriptionStore()
    {
        var store = new Mock<IWebhookSubscriptionStore>();

        store.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(id is not null && Subscriptions.TryGetValue(id, out var found) ? found : null));
        store.Setup(value => value.CreateAsync(It.IsAny<WebhookSubscription>(), It.IsAny<CancellationToken>()))
            .Returns((WebhookSubscription entry, CancellationToken _) =>
            {
                Subscriptions[entry.ItemId] = entry;

                return ValueTask.CompletedTask;
            });
        store.Setup(value => value.UpdateAsync(It.IsAny<WebhookSubscription>(), It.IsAny<CancellationToken>()))
            .Returns((WebhookSubscription entry, CancellationToken _) =>
            {
                Subscriptions[entry.ItemId] = entry;

                return ValueTask.CompletedTask;
            });
        store.Setup(value => value.DeleteAsync(It.IsAny<WebhookSubscription>(), It.IsAny<CancellationToken>()))
            .Returns((WebhookSubscription entry, CancellationToken _) => ValueTask.FromResult(Subscriptions.Remove(entry.ItemId)));
        store.Setup(value => value.PageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int skip, int take, CancellationToken _) =>
            {
                var all = Subscriptions.Values.OrderByDescending(entry => entry.CreatedUtc).ToList();

                return (all.Count, (IReadOnlyList<WebhookSubscription>)all.Skip(skip).Take(take).ToList());
            });

        return store;
    }

    private Mock<IWebhookDeliveryStore> DeliveryStore()
    {
        var store = new Mock<IWebhookDeliveryStore>();

        store.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(id is not null && Deliveries.TryGetValue(id, out var found) ? found : null));
        store.Setup(value => value.CreateAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Returns((WebhookDelivery entry, CancellationToken _) =>
            {
                Deliveries[entry.ItemId] = entry;

                return ValueTask.CompletedTask;
            });
        store.Setup(value => value.UpdateAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Returns((WebhookDelivery entry, CancellationToken _) =>
            {
                Deliveries[entry.ItemId] = entry;

                return ValueTask.CompletedTask;
            });
        store.Setup(value => value.PageAsync(It.IsAny<string>(), It.IsAny<WebhookDeliveryStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string subscriptionId, WebhookDeliveryStatus? status, int skip, int take, CancellationToken _) =>
            {
                var all = Deliveries.Values
                    .Where(entry => entry.SubscriptionId == subscriptionId && (status is null || entry.Status == status))
                    .OrderByDescending(entry => entry.CreatedUtc)
                    .ToList();

                return (all.Count, (IReadOnlyList<WebhookDelivery>)all.Skip(skip).Take(take).ToList());
            });
        store.Setup(value => value.DeleteBySubscriptionAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string subscriptionId, int take, CancellationToken _) =>
            {
                var ids = Deliveries.Values.Where(entry => entry.SubscriptionId == subscriptionId).Take(take).Select(entry => entry.ItemId).ToList();

                ids.ForEach(id => Deliveries.Remove(id));

                return ids.Count;
            });

        return store;
    }

    private static LinkGenerator LinkGenerator()
    {
        var linkGenerator = new Mock<LinkGenerator>();

        linkGenerator
            .Setup(generator => generator.GetPathByAddress(
                It.IsAny<HttpContext>(),
                It.IsAny<RouteValuesAddress>(),
                It.IsAny<RouteValueDictionary>(),
                It.IsAny<RouteValueDictionary>(),
                It.IsAny<PathString?>(),
                It.IsAny<FragmentString>(),
                It.IsAny<LinkOptions>()))
            .Returns((HttpContext _, RouteValuesAddress address, RouteValueDictionary _, RouteValueDictionary _, PathString? _, FragmentString _, LinkOptions _) =>
                address.RouteName == "WebhooksEdit" ? $"/Admin/webhooks/Edit/{address.ExplicitValues["itemId"]}" : null);

        return linkGenerator.Object;
    }
}

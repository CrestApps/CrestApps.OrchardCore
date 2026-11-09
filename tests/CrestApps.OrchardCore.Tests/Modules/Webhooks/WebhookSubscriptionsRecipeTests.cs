using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Deployments;
using CrestApps.OrchardCore.Webhooks.Recipes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks;

/// <summary>
/// Webhook subscriptions travel between sites through a deployment plan and the <c>WebhookSubscriptions</c> recipe step.
/// The protected secret never leaves the site: it is protected with the site's own keys.
/// </summary>
public sealed class WebhookSubscriptionsRecipeTests
{
    private static readonly DateTime _now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Deployment_ExportsTheSubscription_WithoutItsSecret()
    {
        // Arrange
        var store = new Mock<IWebhookSubscriptionStore>();
        store.Setup(entry => entry.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new WebhookSubscription { ItemId = "s1", Name = "ERP", Url = "https://erp.example.com/hooks", Events = ["order.paid"], ProtectedSecret = "protected", IsEnabled = true },
        ]);
        var source = new WebhookSubscriptionsDeploymentSource(store.Object);
        var result = new DeploymentPlanResult(null, new RecipeDescriptor());

        // Act
        await source.ProcessDeploymentStepAsync(new WebhookSubscriptionsDeploymentStep(), result);

        // Assert
        var step = Assert.Single(result.Steps);
        Assert.Equal("WebhookSubscriptions", step["name"]?.GetValue<string>());
        var entry = Assert.Single(step[WebhookSubscriptionsStep.EntriesName]!.AsArray())!.AsObject();
        Assert.Equal("s1", entry["ItemId"]!.GetValue<string>());
        Assert.Equal("https://erp.example.com/hooks", entry["Url"]!.GetValue<string>());
        Assert.False(entry.ContainsKey(nameof(WebhookSubscription.ProtectedSecret)));
    }

    [Fact]
    public async Task Recipe_CreatesTheSubscription_WithTheSecretItCarries_AndRefusesAPrivateHost()
    {
        // Arrange
        var created = new List<WebhookSubscription>();
        var store = new Mock<IWebhookSubscriptionStore>();
        store.Setup(entry => entry.CreateAsync(It.IsAny<WebhookSubscription>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookSubscription, CancellationToken>((subscription, _) => created.Add(subscription))
            .Returns(ValueTask.CompletedTask);
        var protection = new EphemeralDataProtectionProvider();
        var dispatcher = new WebhookDispatcher(Mock.Of<IWebhookSender>(), Mock.Of<IWebhookDeliveryStore>(), store.Object, protection, Mock.Of<IClock>(clock => clock.UtcNow == _now), NullLogger<WebhookDispatcher>.Instance);
        var step = new WebhookSubscriptionsStep(store.Object, dispatcher, Mock.Of<IHostEnvironment>(environment => environment.EnvironmentName == "Production"), Mock.Of<IClock>(clock => clock.UtcNow == _now));
        var context = new RecipeExecutionContext
        {
            Name = WebhookSubscriptionsStep.Name,
            Step = JsonNode.Parse("""
                {
                  "name": "WebhookSubscriptions",
                  "Subscriptions": [
                    { "Name": "ERP", "Url": "https://erp.example.com/hooks", "Events": [ "order.paid" ], "Secret": "whsec_known" },
                    { "Name": "Inside", "Url": "https://10.0.0.5/hooks", "Events": [ "*" ] }
                  ]
                }
                """)!.AsObject(),
        };

        // Act
        await step.ExecuteAsync(context);

        // Assert
        var subscription = Assert.Single(created);
        Assert.Equal("ERP", subscription.Name);
        Assert.Equal(["order.paid"], subscription.Events);
        Assert.Equal("whsec_known", dispatcher.GetSecret(subscription));
        Assert.Single(context.Errors);
        Assert.Equal("Webhooks", WebhookStorage.CollectionName);
        Assert.Equal("*", WebhookEvents.All);
    }
}

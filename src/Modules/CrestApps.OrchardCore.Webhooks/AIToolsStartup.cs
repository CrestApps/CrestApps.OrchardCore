using CrestApps.Core.AI;
using CrestApps.OrchardCore.Webhooks.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Registers the outbound webhook AI tools whenever the AI feature is on. Every tool needs <c>ManageCommerceWebhooks</c>.
/// Saving, rotating, pinging, resending and deleting ask for approval, because a webhook sends site data to another
/// system; no tool takes or returns a signing secret.
/// </summary>
[Feature(WebhooksConstants.Feature.Webhooks)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListWebhooksTool>(ListWebhooksTool.TheName)
            .WithTitle(S["List Webhooks"])
            .WithDescription(S["Lists the outbound webhook subscriptions with name, URL, events and enabled state, with paging or one webhook by id. The signing secret is never returned."])
            .WithCategory(S["Webhooks"])
            .Selectable();

        services.AddCoreAITool<ListWebhookEventsTool>(ListWebhookEventsTool.TheName)
            .WithTitle(S["List Webhook Events"])
            .WithDescription(S["Lists the events the site can send to webhooks, grouped, with their names and descriptions. Use it to choose the events of a webhook."])
            .WithCategory(S["Webhooks"])
            .Selectable();

        services.AddCoreAITool<SaveWebhookTool>(SaveWebhookTool.TheName)
            .WithTitle(S["Save Webhook"])
            .WithDescription(S["Creates a webhook that posts signed JSON for chosen events, or all events, to an https URL, or updates one by id, including enabling or disabling it. A new webhook gets a signing secret that the administrator copies from its admin page."])
            .WithCategory(S["Webhooks"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<RotateWebhookSecretTool>(RotateWebhookSecretTool.TheName)
            .WithTitle(S["Rotate Webhook Secret"])
            .WithDescription(S["Gives a webhook a new signing secret. Deliveries are signed with it from then on, so the receiver rejects them until the new secret is entered there."])
            .WithCategory(S["Webhooks"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteWebhookTool>(DeleteWebhookTool.TheName)
            .WithTitle(S["Delete Webhook"])
            .WithDescription(S["Deletes a webhook and its delivery log. Nothing more is sent to it."])
            .WithCategory(S["Webhooks"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<SendWebhookPingTool>(SendWebhookPingTool.TheName)
            .WithTitle(S["Send Webhook Ping"])
            .WithDescription(S["Sends a test ping event to a webhook now and returns the receiver's status code or the error. Use it to check that a receiver is reachable and accepts the signature."])
            .WithCategory(S["Webhooks"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListWebhookDeliveriesTool>(ListWebhookDeliveriesTool.TheName)
            .WithTitle(S["List Webhook Deliveries"])
            .WithDescription(S["Lists the deliveries of one webhook with event, status, attempts, last status code, last error and times, filtered by status, with paging. Use it to find out why a receiver is not getting events."])
            .WithCategory(S["Webhooks"])
            .Selectable();

        services.AddCoreAITool<ResendWebhookDeliveryTool>(ResendWebhookDeliveryTool.TheName)
            .WithTitle(S["Resend Webhook Delivery"])
            .WithDescription(S["Sends one logged delivery to its webhook again and returns the receiver's status code or the error."])
            .WithCategory(S["Webhooks"])
            .RequireApproval()
            .Selectable();
    }
}

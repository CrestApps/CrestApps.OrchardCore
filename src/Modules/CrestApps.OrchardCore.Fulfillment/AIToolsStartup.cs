using CrestApps.Core.AI;
using CrestApps.OrchardCore.Fulfillment.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Fulfillment;

/// <summary>
/// Registers the Fulfillment Partners AI tools when the Fulfillment Partners and AI features are on. Both need the
/// Shipping <c>FulfillOrders</c> permission, as the order's Fulfillment partners card and the Commerce › Fulfillment
/// requests page do. <c>sendOrderToFulfillmentPartners</c> asks for approval: what it queues leaves the site, and the
/// partner makes and ships it. The partners' settings are a site settings group, which the generic settings tools cover.
/// </summary>
[Feature(FulfillmentConstants.Features.Fulfillment)]
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
        services.AddCoreAITool<ListFulfillmentRequestsTool>(ListFulfillmentRequestsTool.TheName)
            .WithTitle(S["List Fulfillment Requests"])
            .WithDescription(S["Lists orders sent, or waiting to be sent, to fulfillment partners: each partner's status and why, its message and order id, the lines and times; for one order, also whether it can be sent now. Tracking is in getOrderFulfillment."])
            .WithCategory(S["Fulfillment Partners"])
            .Selectable();

        services.AddCoreAITool<SendOrderToFulfillmentPartnersTool>(SendOrderToFulfillmentPartnersTool.TheName)
            .WithTitle(S["Send Order To Fulfillment Partners"])
            .WithDescription(S["Queues a paid order for the fulfillment partners that don't have it yet, and again where a request is waiting or needs attention; or queues one request again by its id. It is sent within about a minute and the partner then makes and ships the items, which cannot be undone here."])
            .WithCategory(S["Fulfillment Partners"])
            .RequireApproval()
            .Selectable();
    }
}

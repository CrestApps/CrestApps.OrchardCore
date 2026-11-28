using CrestApps.Core.AI;
using CrestApps.OrchardCore.Orders.AI;
using CrestApps.OrchardCore.Orders.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders;

/// <summary>
/// Registers the return AI tools when the Returns and AI features are on. Every tool needs <c>ManageOrders</c>, as
/// Commerce › Returns. Approving, declining and closing an exchange email the buyer, so they ask for approval. A return
/// is refunded with <c>refundOrder</c> and its <c>returnId</c>, not by a tool of its own.
/// </summary>
[Feature(OrderConstants.Features.Returns)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class ReturnsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReturnsAIToolsStartup(IStringLocalizer<ReturnsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListOrderReturnsTool>(ListOrderReturnsTool.TheName)
            .WithTitle(S["List Order Returns"])
            .WithDescription(S["Lists buyers' return requests with number, order, customer, status, whether a refund or an exchange is wanted, and quantity. Filters by text and status (open by default), with paging."])
            .WithCategory(S["Orders"])
            .Selectable();

        services.AddCoreAITool<GetOrderReturnTool>(GetOrderReturnTool.TheName)
            .WithTitle(S["Get Order Return"])
            .WithDescription(S["Returns one return request with its lines and reasons, the buyer's comment, its order, what the order can still refund and the suggested refund for the returned items. Use it before deciding, receiving or refunding a return."])
            .WithCategory(S["Orders"])
            .Selectable();

        services.AddCoreAITool<UpdateOrderReturnStatusTool>(UpdateOrderReturnStatusTool.TheName)
            .WithTitle(S["Update Order Return Status"])
            .WithDescription(S["Approves a return, declines it (a reason is required), or closes an exchange once the replacement is on its way. Each of these emails the buyer, with the message given."])
            .WithCategory(S["Orders"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<ReceiveOrderReturnTool>(ReceiveOrderReturnTool.TheName)
            .WithTitle(S["Receive Order Return"])
            .WithDescription(S["Records that a return's items came back and puts the chosen lines back into stock, at a named location or where they were sold from. It does not refund."])
            .WithCategory(S["Orders"])
            .Selectable();
    }
}

using CrestApps.Core.AI;
using CrestApps.OrchardCore.Orders.AI;
using CrestApps.OrchardCore.Orders.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders;

/// <summary>
/// Registers <c>diagnoseOrder</c> whenever the Orders and AI features are on. It needs <c>ManageOrders</c>; the payment
/// ledger part of its answer needs <c>ManageRefunds</c> as well. It only reads.
/// </summary>
[Feature(OrderConstants.Features.Orders)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class OrderDiagnosticsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderDiagnosticsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OrderDiagnosticsAIToolsStartup(IStringLocalizer<OrderDiagnosticsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<DiagnoseOrderTool>(DiagnoseOrderTool.TheName)
            .WithTitle(S["Diagnose Order"])
            .WithDescription(S["Explains why an order is stuck, unpaid or not shipped: what is owed and refundable, its fulfillment status, open disputes and payment risk and, for people who may see the payment ledgers, its payment attempts, refunds waiting to be settled by hand and open payment issues, with a list of findings. It changes nothing."])
            .WithCategory(S["Orders"])
            .Selectable();
    }
}

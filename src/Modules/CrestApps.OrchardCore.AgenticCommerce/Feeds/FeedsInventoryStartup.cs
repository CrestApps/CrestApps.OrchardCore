using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Pushes a product to the ACP feed APIs after its stock changes, when Inventory is on.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Feeds)]
[RequireFeatures(InventoryConstants.Features.Inventory)]
public sealed class FeedsInventoryStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IStockLevelHandler, AgentFeedStockHandler>();
}

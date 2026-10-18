using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Inventory.Workflows;

/// <summary>
/// The <see cref="LowStockEvent"/> has no settings, so it only renders its thumbnail and design shapes.
/// </summary>
public sealed class LowStockEventDisplayDriver : ActivityDisplayDriver<LowStockEvent>
{
}

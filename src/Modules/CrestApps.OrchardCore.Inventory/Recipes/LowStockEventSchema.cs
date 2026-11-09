using CrestApps.OrchardCore.Inventory.Workflows;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Inventory.Recipes;

/// <summary>
/// Describes the <c>LowStockEvent</c> workflow event to CrestApps Recipes.
/// </summary>
internal sealed class LowStockEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => nameof(LowStockEvent);

    protected override string Category => "Commerce";

    protected override string DisplayText => "Low Stock";

    protected override string Description => "Starts when a product, or one of its variants, runs low or sells out. The input carries the product (as ContentItem), the stock item, and its SKU, title, units available, threshold and whether it sold out.";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

using System.Text.Json;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgenticCommerce.AI;

/// <summary>
/// Returns one of the store's products for the site's AI assistant.
/// </summary>
public sealed class GetStoreProductTool : AgentCommerceToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "getStoreProduct";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "id": { "type": "string", "description": "The product's id, or one of its variants' item ids." }
          },
          "required": ["id"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Returns one of the store's products with its description, photos and variants, each with its item id, price and availability.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var id = Read(arguments, "id");

        if (id is null)
        {
            return "Give the product's id.";
        }

        var services = arguments.Services;
        var contentItemId = (await services.GetRequiredService<IAgentItemResolver>().ResolveAsync(id, cancellationToken))?.ContentItemId ?? id;
        var products = await services.GetRequiredService<UcpCatalog>().WriteAsync([contentItemId], currency: null, cancellationToken);

        return products.Count == 0 ? "There is no such product." : products[0].ToJsonString(JsonOptions);
    }
}

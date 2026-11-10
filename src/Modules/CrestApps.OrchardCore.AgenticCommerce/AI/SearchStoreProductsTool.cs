using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgenticCommerce.AI;

/// <summary>
/// Searches the store's products for the site's AI assistant.
/// </summary>
public sealed class SearchStoreProductsTool : AgentCommerceToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "searchStoreProducts";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "description": "The words to look for, such as a product name, brand or kind." },
            "limit": { "type": "integer", "description": "How many products to return, 1 to 20. Defaults to 10." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Searches the store's products. Returns each product with its variants, the item id to buy each with, its price and whether it is in stock.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var limit = int.TryParse(Read(arguments, "limit"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, 1, 20) : 10;
        var (products, total) = await arguments.Services.GetRequiredService<UcpCatalog>().SearchAsync(Read(arguments, "query"), 0, limit, currency: null, cancellationToken);

        return new JsonObject
        {
            ["total"] = total,
            ["products"] = products,
        }.ToJsonString(JsonOptions);
    }
}

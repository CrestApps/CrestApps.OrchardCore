using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Carts.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgenticCommerce.AI;

/// <summary>
/// Adds an item to the storefront cart of the visitor who calls it, such as the visitor whose in-browser agent runs
/// the tool, with the same rules as the store's own add to cart button. It takes no cart id, so it can only change the
/// caller's own cart, and it never checks out or pays.
/// </summary>
public sealed class AddToStoreCartTool : AgentCommerceToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "addToStoreCart";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "id": { "type": "string", "description": "The item id, as searchStoreProducts returns it for a variant." },
            "quantity": { "type": "integer", "description": "How many to add. Defaults to 1." }
          },
          "required": ["id"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Adds an item to the buyer's own cart in this store and returns the cart. The buyer checks out from the cart page; never ask for card details.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var id = Read(arguments, "id");

        if (id is null)
        {
            return "Give the item id.";
        }

        var quantity = int.TryParse(Read(arguments, "quantity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Max(1, value) : 1;
        var services = arguments.Services;
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;

        if (httpContext is null)
        {
            return NoVisitorMessage;
        }

        var item = await services.GetRequiredService<IAgentItemResolver>().ResolveAsync(id, cancellationToken);

        if (item is null)
        {
            return $"The store doesn't sell an item with the id '{id}'.";
        }

        var carts = services.GetRequiredService<ICartService>();

        // A guest's first item starts their cart and gives them its cookie. A request whose response has started, such
        // as a chat connection, can't send one, and the cart would belong to nobody.
        if (httpContext.User?.Identity?.IsAuthenticated != true &&
            httpContext.Response.HasStarted &&
            await carts.GetCurrentAsync(cancellationToken) is null)
        {
            return "The buyer has no cart yet. Ask them to add the first item from the product's page.";
        }

        // The cart service finds the caller's cart from the request: the signed-in user's, or the guest's from their
        // cart cookie.
        var result = await carts.AddAsync(item.ContentItemId, priceId: null, item.VariantId, quantity, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Message?.Value ?? "The item couldn't be added to the cart.";
        }

        var json = await StoreCartJson.WriteAsync(services, httpContext, result.Cart, cancellationToken);

        if (result.Message is { } message)
        {
            json["message"] = message.Value;
        }

        return json.ToJsonString(JsonOptions);
    }
}

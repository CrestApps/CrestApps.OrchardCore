using System.Text.Json;
using CrestApps.OrchardCore.Carts.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgenticCommerce.AI;

/// <summary>
/// Returns the storefront cart of the visitor who calls it, such as the visitor whose in-browser agent runs the tool.
/// It takes no cart id, so it can only read the caller's own cart.
/// </summary>
public sealed class GetStoreCartTool : AgentCommerceToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "getStoreCart";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Returns the buyer's own cart in this store: each item with its item id, quantity, price and any problem, the subtotal, and the link to the cart page where the buyer checks out.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var services = arguments.Services;
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;

        if (httpContext is null)
        {
            return NoVisitorMessage;
        }

        // The cart service finds the caller's cart from the request: the signed-in user's, or the guest's from their
        // cart cookie.
        var cart = await services.GetRequiredService<ICartService>().GetCurrentAsync(cancellationToken);

        return (await StoreCartJson.WriteAsync(services, httpContext, cart, cancellationToken)).ToJsonString(JsonOptions);
    }
}

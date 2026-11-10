using CrestApps.Core.AI;
using CrestApps.OrchardCore.AgenticCommerce.AI;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.Products.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers the commerce AI tools, which administrators can pick on AI profiles and expose over MCP or WebMCP, whenever
/// the AI feature and the product listing and feeds are on, and marks them safe for anonymous storefront visitors.
/// </summary>
[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
[RequireFeatures(AIConstants.Feature.Area, ProductConstants.Feature.Listing, ProductConstants.Feature.Feeds)]
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

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<UcpCatalog>();
        services.AddCoreAITool<SearchStoreProductsTool>(SearchStoreProductsTool.ToolName).WithTitle(S["Search store products"]).WithDescription(S["Searches the store's products, with their variants, prices and stock."]).WithCategory(S["Commerce"]).Selectable();
        services.AddCoreAITool<GetStoreProductTool>(GetStoreProductTool.ToolName).WithTitle(S["Get store product"]).WithDescription(S["Returns one of the store's products with its variants."]).WithCategory(S["Commerce"]).Selectable();
        services.AddCoreAITool<StartStoreCheckoutTool>(StartStoreCheckoutTool.ToolName).WithTitle(S["Start store checkout"]).WithDescription(S["Starts a checkout for the buyer's chosen items and returns the link where they pay."]).WithCategory(S["Commerce"]).Selectable();
        services.AddCoreAITool<AddToStoreCartTool>(AddToStoreCartTool.ToolName).WithTitle(S["Add to store cart"]).WithDescription(S["Adds an item to the buyer's own cart and returns the cart."]).WithCategory(S["Commerce"]).Selectable();
        services.AddCoreAITool<GetStoreCartTool>(GetStoreCartTool.ToolName).WithTitle(S["Get store cart"]).WithDescription(S["Returns the buyer's own cart with its items, subtotal and the cart page link."]).WithCategory(S["Commerce"]).Selectable();

        // Anyone may run these: they read the public catalog, or act on the caller's own cart and checkout, and none of
        // them pays. WebMCP offers them to storefront visitors when its storefront tools are on.
        services
            .MarkAIToolAnonymousSafe(SearchStoreProductsTool.ToolName)
            .MarkAIToolAnonymousSafe(GetStoreProductTool.ToolName)
            .MarkAIToolAnonymousSafe(StartStoreCheckoutTool.ToolName)
            .MarkAIToolAnonymousSafe(AddToStoreCartTool.ToolName)
            .MarkAIToolAnonymousSafe(GetStoreCartTool.ToolName);
    }
}

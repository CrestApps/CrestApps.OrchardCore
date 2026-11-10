using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Webhooks;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Agentic Commerce",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AgenticCommerceConstants.Features.AgenticCommerce,
    Name = "Agentic Commerce",
    Description = "Lets AI agents buy from the store for a buyer: the agent platforms allowed to, their credentials, the store's signing keys, and one agent checkout over the store's own prices, tax, shipping, discounts and stock.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        CommerceConstants.Features.StoreProfile,
        CheckoutConstants.Features.Area,
        OrderConstants.Features.Orders,
        ProductConstants.Feature.ModuleId,
        WebhooksConstants.Feature.Webhooks,
    ]
)]

[assembly: Feature(
    Id = AgenticCommerceConstants.Features.Ucp,
    Name = "Universal Commerce Protocol",
    Description = "Lets agents that speak the Universal Commerce Protocol (UCP), such as Google AI Mode and Gemini, find the store at /.well-known/ucp, check out with fulfillment and discount codes, pay through Stripe or Google Pay, and receive signed order updates.",
    Category = "Commerce",
    Dependencies =
    [
        AgenticCommerceConstants.Features.AgenticCommerce,
    ]
)]

[assembly: Feature(
    Id = AgenticCommerceConstants.Features.UcpIdentityLinking,
    Name = "UCP Account Linking",
    Description = "Lets a buyer link their store account to an agent through the store's OpenID server, so the agent checks out as them with their saved addresses and reads their orders.",
    Category = "Commerce",
    Dependencies =
    [
        AgenticCommerceConstants.Features.Ucp,
        OrchardCoreFeatureIds.OpenIdServer,
    ]
)]

[assembly: Feature(
    Id = AgenticCommerceConstants.Features.UcpCatalog,
    Name = "UCP Catalog and Cart",
    Description = "Lets UCP agents search the catalog, look products up priced for the buyer, and build a cart before checking out.",
    Category = "Commerce",
    Dependencies =
    [
        AgenticCommerceConstants.Features.Ucp,
        ProductConstants.Feature.Listing,
    ]
)]

[assembly: Feature(
    Id = AgenticCommerceConstants.Features.UcpMcp,
    Name = "UCP over MCP",
    Description = "Serves UCP's checkout as Model Context Protocol (MCP) tools at ucp/mcp, for agents that use UCP's MCP binding.",
    Category = "Commerce",
    Dependencies =
    [
        AgenticCommerceConstants.Features.Ucp,
    ]
)]

[assembly: Feature(
    Id = AgenticCommerceConstants.Features.UcpA2a,
    Name = "UCP over A2A",
    Description = "Serves UCP's checkout and orders through an Agent2Agent (A2A) agent, with its agent card at ucp/a2a/.well-known/agent-card.json, for agents that use UCP's A2A binding.",
    Category = "Commerce",
    Dependencies =
    [
        AgenticCommerceConstants.Features.Ucp,
    ]
)]

[assembly: Feature(
    Id = AgenticCommerceConstants.Features.Acp,
    Name = "Agentic Commerce Protocol",
    Description = "Lets agents that speak the Agentic Commerce Protocol (ACP), such as ChatGPT, check out and pay with a delegated payment token, and sends them signed order updates.",
    Category = "Commerce",
    Dependencies =
    [
        AgenticCommerceConstants.Features.AgenticCommerce,
    ]
)]

[assembly: Feature(
    Id = AgenticCommerceConstants.Features.AcpMcp,
    Name = "ACP over MCP",
    Description = "Serves ACP's checkout sessions as Model Context Protocol (MCP) tools at acp/mcp, for agents that use ACP's MCP binding, and lists the mcp transport in /.well-known/acp.json.",
    Category = "Commerce",
    Dependencies =
    [
        AgenticCommerceConstants.Features.Acp,
    ]
)]

[assembly: Feature(
    Id = AgenticCommerceConstants.Features.Feeds,
    Name = "Agent Product Feeds",
    Description = "Publishes the products for AI agents in OpenAI's product feed format, pushes a daily snapshot over SFTP, and keeps agents that host an ACP feed API up to date.",
    Category = "Commerce",
    Dependencies =
    [
        AgenticCommerceConstants.Features.AgenticCommerce,
        ProductConstants.Feature.Feeds,
    ]
)]

[assembly: Feature(
    Id = AgenticCommerceConstants.Features.Ap2,
    Name = "Agent Payments Protocol (Preview)",
    Description = "Adds the Agent Payments Protocol (AP2) mandates extension to UCP: the store signs every checkout, and an agent completes it only with a mandate the buyer signed for that exact checkout.",
    Category = "Commerce",
    Dependencies =
    [
        AgenticCommerceConstants.Features.Ucp,
    ]
)]

# CrestApps OrchardCore Agentic Commerce

The Agentic Commerce module lets AI agents, such as Google AI Mode, Gemini and ChatGPT, buy from the store for a buyer. It speaks the Universal Commerce Protocol (UCP `2026-08-25` and `2026-04-08`) and the Agentic Commerce Protocol (ACP `2026-04-17` and `2026-01-30`), with the AP2 mandates extension of UCP as a preview, and publishes the products in the feeds agent platforms read.

## Why this module exists

An agent checkout must be the store's checkout. If a protocol priced items, held stock or charged cards on its own, the store would sell at prices, taxes and stock levels its storefront doesn't know. Here, one protocol-neutral service, `IAgentCheckoutService`, applies what the agent asks for to an ordinary store checkout: the items are priced as a cart, an order on the `agent` sales channel holds the stock, the Contact and Shipping steps are filled through their own contracts, and the payment goes through `ICheckoutEngine` with the credential the agent brought. The UCP and ACP endpoints only map their JSON to that service and back.

## Features

| Feature | ID | What it adds |
| --- | --- | --- |
| Agentic Commerce | `CrestApps.OrchardCore.AgenticCommerce` | Agent platforms (**Commerce > AI agents**), settings (**Settings > Commerce > Agentic commerce**), the store's signing keys, the agent checkout, the hand-off link, order updates and payment handlers. |
| Universal Commerce Protocol | `CrestApps.OrchardCore.AgenticCommerce.Ucp` | `/.well-known/ucp`, `ucp/v1` checkout sessions and orders, signed order webhooks. |
| UCP Account Linking | `CrestApps.OrchardCore.AgenticCommerce.Ucp.IdentityLinking` | Buyers link their account through the OpenID server. |
| UCP Catalog and Cart | `CrestApps.OrchardCore.AgenticCommerce.Ucp.Catalog` | Catalog search and lookup, and the UCP cart. |
| UCP over MCP | `CrestApps.OrchardCore.AgenticCommerce.Ucp.Mcp` | UCP's checkout as MCP tools at `ucp/mcp`. |
| Agentic Commerce Protocol | `CrestApps.OrchardCore.AgenticCommerce.Acp` | `/.well-known/acp.json`, `acp` checkout sessions, optional request signatures, signed order webhooks. |
| ACP over MCP | `CrestApps.OrchardCore.AgenticCommerce.Acp.Mcp` | ACP's checkout sessions as MCP tools at `acp/mcp`. |
| Agent Product Feeds | `CrestApps.OrchardCore.AgenticCommerce.Feeds` | The OpenAI product feed, the daily SFTP push and the ACP feed API client. |
| Agent Payments Protocol (Preview) | `CrestApps.OrchardCore.AgenticCommerce.Ap2` | AP2 merchant authorizations and checkout mandates for UCP. |

## Dependencies

Commerce, Store Profile, Checkout, Orders, Products and Webhooks. Payments go through Stripe (shared payment tokens and Google Pay) or, on a test site, the Checkout module's Test payments feature. Account linking needs the OpenID server; the catalog needs Product Listing; the feeds need Product Feeds. The AI tools (`searchStoreProducts`, `getStoreProduct`, `startStoreCheckout`) are registered when the AI feature, Product Listing and Product Feeds are on.

## Getting started

1. Enable **Agentic Commerce** and the protocols of the platforms you sell through, or run the `AgenticCommerceSample` recipe on a test site.
2. Fill in **Website and policies** under **Settings > Commerce > Store**, and connect Stripe.
3. Add each platform under **Commerce > AI agents** and give it an API key.

## Extension points

`IAgentPaymentHandler` (a way agents pay), `IUcpProfileContributor` and `IUcpCheckoutExtension` (UCP capabilities and extensions), `IAgentOrderPublisher` with the Webhooks module's `IWebhookOutbox` and `IWebhookDeliveryFormatter` (a protocol's order updates), and `IAgentCheckoutService` (another binding). See the `crestapps-agentic-commerce` skill in `.agents/skills`.

## Documentation

- Technical Manual: [Agentic Commerce](https://orchardcore.crestapps.com/docs/modules/agentic-commerce)
- User Manual: [Sell through AI agents](https://orchardcore.crestapps.com/docs/user-manual/commerce/ai-agents)

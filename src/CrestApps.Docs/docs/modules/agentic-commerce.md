---
sidebar_label: Agentic Commerce
title: Agentic Commerce
description: Sell through AI agents with the Universal Commerce Protocol (UCP), the Agentic Commerce Protocol (ACP) and the AP2 mandates extension, on the store's own checkout, and publish the products in the feeds agent platforms read.
user_manual:
  - user-manual/commerce/ai-agents
  - user-manual/commerce/product-listings
---

The Agentic Commerce module lets AI agents buy from the store for a buyer. An agent, such as Google AI Mode, Gemini or ChatGPT, finds the store, builds a checkout, and pays with a payment credential its platform got from the buyer. The store keeps its own prices, tax, shipping, discounts and stock: every agent checkout is a store checkout, priced and paid through the [checkout engine](checkout.md#the-checkout-engine).

The module speaks two open protocols. The **Universal Commerce Protocol** (UCP) serves the versions `2026-08-25` and `2026-04-08`. The **Agentic Commerce Protocol** (ACP) serves the versions `2026-04-17` and `2026-01-30`. The **Agent Payments Protocol** (AP2) mandates extension of UCP is a preview. The module also publishes the products in the feeds agent platforms read, and gives the site's own AI assistant commerce tools.

The admin screens are described in the User Manual: [Sell through AI agents](../user-manual/commerce/ai-agents.md) for the platforms and the settings, and [Product listings](../user-manual/commerce/product-listings.md#feeds-for-ai-agents) for the agent feeds.

## Features

| Feature ID | Name | Depends on | What it adds |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.AgenticCommerce` | Agentic Commerce | Commerce, Store Profile, Checkout, Orders, Products, Webhooks | Agent platforms (**Commerce > AI agents**), the settings (**Settings > Commerce > Agentic commerce**), the store's signing keys, the protocol-neutral agent checkout, the hand-off link, order updates, and the payment handlers. |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp` | Universal Commerce Protocol | Agentic Commerce | The UCP profile at `/.well-known/ucp`, the REST binding for checkout and orders, the fulfillment, discount and buyer consent extensions, and signed order webhooks. |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.IdentityLinking` | UCP Account Linking | UCP, OpenID Server | The `dev.ucp.common.identity_linking` capability: a buyer links their store account to an agent through the tenant's OpenID server. |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.Catalog` | UCP Catalog and Cart | UCP, Product Listing | Catalog search and lookup, and the UCP cart. |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.Mcp` | UCP over MCP | UCP | UCP's checkout as Model Context Protocol tools at `ucp/mcp`. |
| `CrestApps.OrchardCore.AgenticCommerce.Acp` | Agentic Commerce Protocol | Agentic Commerce | The discovery document at `/.well-known/acp.json`, the ACP checkout REST binding, optional request signatures, and signed order webhooks. |
| `CrestApps.OrchardCore.AgenticCommerce.Acp.Mcp` | ACP over MCP | ACP | ACP's checkout sessions as Model Context Protocol tools at `acp/mcp`. See [ACP over MCP](#acp-over-mcp). |
| `CrestApps.OrchardCore.AgenticCommerce.Feeds` | Agent Product Feeds | Agentic Commerce, Product Feeds | The OpenAI product feed, the daily SFTP push and the ACP feed API client. See [Agent product feeds](#agent-product-feeds). |
| `CrestApps.OrchardCore.AgenticCommerce.Ap2` | Agent Payments Protocol (Preview) | UCP | The AP2 mandates extension of UCP. See [AP2 mandates (preview)](#ap2-mandates-preview). |

The code lives in three projects:

| Project | Contents |
| --- | --- |
| `CrestApps.OrchardCore.AgenticCommerce.Abstractions` | `AgenticCommerceConstants` (feature IDs, protocols, versions), the protocol-neutral checkout models (`AgentCheckoutInput`, `AgentCheckoutUpdate`, `AgentCheckoutView`, `AgentPayment`, `AgentCheckoutMessage`, `AgentMessageCodes`) and `IAgentPaymentHandler`. |
| `CrestApps.OrchardCore.AgenticCommerce.Core` | `AgentPlatform`, `AgentCheckout`, `AgenticCommerceSettings`, `IAgentCheckoutService`, `IAgentPlatformStore`, `IAgentCheckoutStore`, `IMerchantSigningKeyService`, the AP2 verifier and the signature helpers (RFC 9421 HTTP message signatures, RFC 9530 content digests, RFC 8785 JSON canonicalization, ES256 JWS). |
| `CrestApps.OrchardCore.AgenticCommerce` (module) | The UCP and ACP endpoints and mappers, `IUcpProfileContributor`, `IUcpCheckoutExtension`, `IAgentOrderPublisher`, the payment handlers, the admin, the recipes and the AI tools. |

## Permissions

| Permission | Name in the role editor | Granted by default to |
| --- | --- | --- |
| `ManageAgentPlatforms` | Manage AI agent platforms | Administrator, Store manager |
| `ManageAgenticCommerceSettings` | Manage agentic commerce settings | Administrator, Store manager |
| `ManageAgentFeeds` | Manage agent product feeds | Administrator, Store manager |

Agents themselves need no permission. An agent platform signs in with its own credentials (see [How a platform signs in](#how-a-platform-signs-in)), and its requests run as a principal of its own, `agent-platform:{platformId}`, so idempotency keys are kept for each platform.

## How it fits on the checkout engine

One protocol-neutral service, `IAgentCheckoutService` (`AgentCheckoutService`), does the work for every protocol. The UCP and ACP endpoints, the MCP tools and the AI tools only map their JSON to it and back.

1. **Create.** The agent sends items by the item ids of the product feed (`ProductFeedItemIds`, read back with `IAgentItemResolver`), the buyer, an address and discount codes. The service keeps them on an `AgentCheckout` record in the `AgenticCommerce` YesSql collection.
2. **Price.** The items are priced as a cart that the store keeps for this checkout only (`ICartService`), so every storefront rule applies: what can be bought, how many, and at which price. An item the store doesn't sell keeps the checkout from starting.
3. **Order and store checkout.** `IOrderService.CreateFromCartAsync` makes a draft order with the sales channel `agent` (`OrderChannels.Agent` in Orders.Core), and the stock it holds is committed with it. `ICheckoutEngine.StartAsync` starts a store checkout for that order. When the agent changes the items, the old store checkout is released and a new order replaces the old one, the same as a buyer who changes their cart.
4. **Steps.** The buyer's e-mail and billing address go through `ICheckoutContactStep` (the Customers module), and the address and shipping choice through `ICheckoutFulfillmentStep` (the Shipping module), with the same rules as the storefront's pages. Without a choice, the cheapest way to ship is picked. Discount codes go through the checkout's coupon service.
5. **Pay.** The agent's payment names a payment handler. The handler turns the credential into provider data (`CheckoutPaymentDataKeys.DelegatedCredential`), and `ICheckoutEngine.BeginPaymentAsync` charges it on the server through a provider that sets `PaymentProviderCapabilities.AcceptsDelegatedCredentials`. `ICheckoutEngine.TryCompleteAsync` then completes the checkout, which pays the order.

The totals an agent sees always add up: subtotal, minus discount, plus shipping, plus tax, plus fees equals the total. Tax already in the prices isn't added again, and what the invoice charges beyond those parts is shown as a fee. Every protocol writes money in the currency's minor units, through `CurrencyScale`.

Requests for one checkout are taken one at a time, under a distributed lock (`agent-checkout:{id}`, 10 seconds to get it). A second request gets `409` with `Retry-After: 1`. A platform only sees its own checkouts.

### Checkout statuses

| `AgentCheckoutStatus` | UCP | ACP | When |
| --- | --- | --- | --- |
| `Incomplete` | `incomplete` | `not_ready_for_payment` | Something is missing or wrong, such as the buyer's e-mail or the address. The messages say what. |
| `RequiresEscalation` | `requires_escalation` | `requires_escalation` | The buyer must act in the store's checkout. See [When the buyer must act](#when-the-buyer-must-act). |
| `ReadyForComplete` | `ready_for_complete` | `ready_for_payment` | Every step the store needs is done. |
| `CompleteInProgress` | `complete_in_progress` | `complete_in_progress` | The provider hasn't confirmed the payment yet. |
| `Completed` | `completed` | `completed` | The order is paid. |
| `Canceled` | `canceled` | `canceled` | The agent canceled it. |
| `Expired` | `canceled` | `expired` | It ran out of time. |

A checkout lasts **Checkout lifetime (hours)** (1 to 72, 6 by default) unless it is completed. The **Agent checkout expiry** background task runs every five minutes, expires the checkouts whose time ran out, and releases the stock they held. One platform may keep at most **Open checkouts per platform** (1,000 by default) open at once; one more gets `429`.

## Endpoints

The paths come after the tenant's base address.

| Method and path | Feature | What it does |
| --- | --- | --- |
| `GET /.well-known/ucp` | UCP | The store's UCP profile for `2026-08-25`, with the address of the older version's profile. Public, cached for 5 minutes, with an `ETag`. |
| `GET /.well-known/ucp/{version}` | UCP | The profile for `2026-08-25` or `2026-04-08`. |
| `POST ucp/v1/checkout-sessions` | UCP | Creates a checkout (`201`). Without `line_items`, a `cart_id` starts it from a UCP cart. |
| `GET ucp/v1/checkout-sessions/{id}` | UCP | Reads a checkout. |
| `PUT ucp/v1/checkout-sessions/{id}` | UCP | Changes a checkout. |
| `POST ucp/v1/checkout-sessions/{id}/complete` | UCP | Pays and completes a checkout. |
| `POST ucp/v1/checkout-sessions/{id}/cancel` | UCP | Cancels a checkout. |
| `GET ucp/v1/orders/{id}` | UCP | Reads an order the platform's agents placed, or, with a linked account and the `dev.ucp.shopping.order:read` scope, one of the buyer's orders. |
| `POST ucp/v1/catalog/search` | UCP Catalog and Cart | Searches the products. |
| `POST ucp/v1/catalog/lookup` | UCP Catalog and Cart | Returns up to 50 products by id. |
| `POST ucp/v1/catalog/product` | UCP Catalog and Cart | Returns one product. |
| `POST ucp/v1/carts`, `GET` and `PUT ucp/v1/carts/{id}`, `POST ucp/v1/carts/{id}/cancel` | UCP Catalog and Cart | The UCP cart. |
| `POST ucp/mcp` | UCP over MCP | UCP's MCP binding (JSON-RPC). |
| `GET /.well-known/oauth-authorization-server` | UCP Account Linking | Redirects to the OpenID server's `/.well-known/openid-configuration`. |
| `GET /.well-known/acp.json` | ACP | The ACP discovery document. Public, cached for an hour. |
| `POST acp/checkout_sessions` | ACP | Creates a checkout (`201`). |
| `GET acp/checkout_sessions/{id}` | ACP | Reads a checkout. |
| `POST acp/checkout_sessions/{id}` | ACP | Changes a checkout. |
| `POST acp/checkout_sessions/{id}/complete` | ACP | Pays and completes a checkout. |
| `POST acp/checkout_sessions/{id}/cancel` | ACP | Cancels a checkout. A checkout that can't be canceled gets `405`. |
| `POST acp/mcp` | ACP over MCP | ACP's MCP binding (JSON-RPC). |
| `GET agentic-commerce/continue/{token}` | Agentic Commerce | The hand-off link. See [When the buyer must act](#when-the-buyer-must-act). |

A request body is at most 1 MB; a larger one gets `413`. The body is read once, exactly as sent, so its signature and digest can be checked.

UCP answers a business outcome, such as changing a completed checkout, with HTTP `200` and the checkout with an error status and messages, as UCP asks. A missing checkout gets `404`, a busy one `409` and too many open checkouts `429`. ACP answers errors with a `4xx` status and an ACP error object (`type`, `code`, `message`, `param`).

### UCP profile and negotiation

`UcpProfileBuilder` builds the profile from what the enabled features offer (`IUcpProfileContributor`): the services and their transports (`rest`, and `mcp` with UCP over MCP), the capabilities and extensions, the payment handlers the store offers, and the store's public signing keys under `keys`.

| Capability | Offered by |
| --- | --- |
| `dev.ucp.shopping.checkout` with the extensions `dev.ucp.shopping.fulfillment`, `dev.ucp.shopping.discount` and `dev.ucp.shopping.buyer_consent` | UCP |
| `dev.ucp.shopping.order` | UCP |
| `dev.ucp.shopping.catalog.search`, `dev.ucp.shopping.catalog.lookup`, `dev.ucp.shopping.cart` | UCP Catalog and Cart |
| `dev.ucp.common.identity_linking` | UCP Account Linking |
| `dev.ucp.common.payment.ap2_mandate` (`dev.ucp.shopping.ap2_mandate` in `2026-04-08`) | AP2 (preview) |

An agent names its platform's profile in the `UCP-Agent` header (`profile="https://..."`, an RFC 8941 dictionary). `UcpProfileClient` fetches it through the egress-guarded HTTP client `AgenticCommerce.Profiles`, which refuses hosts on the site's own network and doesn't follow redirects. A profile is at most 256 KB, is fetched within 10 seconds, and is kept for its `Cache-Control` `max-age`, at least 1 minute and at most 1 hour. A header that names no profile gets `400 invalid_profile_url`, a profile that can't be fetched `424 profile_unreachable`, and one that isn't a UCP profile `422 profile_malformed`. Without the header, the profile address saved on the platform is used.

`UcpNegotiator` agrees the version and capabilities. The platform's `ucp.version` must be one the store serves, or the request gets `422 version_unsupported`. A capability is kept when both profiles offer it in the same version, and an extension only while its parent is kept. A platform without a profile gets the newest version and every capability the store offers.

### ACP discovery and versions

`/.well-known/acp.json` names the protocol, the supported versions, `api_base_url` (`{base}/acp`), the transports (`rest`, and `mcp` with ACP over MCP), the `checkout` and `orders` services and the `discount` extension. Features add to it with `IAcpDiscoveryContributor`. Every ACP request sends `API-Version`; a missing or unknown version gets `400` with `missing_api_version` or `unsupported_api_version` and the `supported_versions`. A `Request-Id` header is echoed back.

## How a platform signs in

Each business whose agents may buy is an `AgentPlatform`, kept with `IAgentPlatformStore` and edited under **Commerce > AI agents**. A request is signed in by one of these:

| Credential | How it is sent | Protocols |
| --- | --- | --- |
| API key | `Authorization: Bearer agk_...` or `X-API-Key: agk_...`. The admin makes the key with **New API key** and shows it once; only its SHA-256 hash and its last four characters are kept. | UCP, ACP |
| OAuth bearer token | `Authorization: Bearer {token}` from the tenant's OpenID server, validated with Orchard Core's `Api` authentication scheme. Its `client_id` (or `azp`) claim must be the platform's **OAuth client ID**. A token whose `sub` isn't the client is a linked buyer's token (see [Account linking](#account-linking-ucp)). | UCP, ACP |
| RFC 9421 HTTP message signature | `Signature-Input` and `Signature`, with `Content-Digest` when there is a body. The key named by `keyid` must be in the `keys` of the platform's UCP profile. | UCP |

Then the platform must be enabled (**Its agents may buy**), use the protocol of the request, and call from one of its **Allowed IP addresses** (single addresses or CIDR ranges; empty allows any). A platform with a **UCP profile** address may only act for that profile: a `UCP-Agent` header that names another profile gets `403`.

A signed request is checked even when an API key or a token also signed it in. A body that doesn't match its digest gets `400 digest_mismatch`, an algorithm the store doesn't verify `400 algorithm_unsupported`, and a signature that can't be verified `401`. When the signature alone signs the request in, the platform is found by its profile address. With **Let UCP platforms the store doesn't know buy** (`AgenticCommerceSettings.AllowUnregisteredUcpPlatforms`) on, a signed request from a profile no platform has is let in as an unregistered platform, whose checkouts are kept under `ucp-` and a hash of the profile address. Such a platform has no test mode, no allowed IP addresses and no payment handler limit.

A request without credentials gets `401` with `WWW-Authenticate: Bearer`.

## ACP request signatures

ACP lets a platform sign each request with a `Signature` header (a base64url detached signature) and a `Timestamp` header (RFC 3339). The specification leaves the algorithm, the signed bytes and the key to the merchant and the platform, so the store settles them per platform, in the **ACP request signatures** card of the platform:

| Setting | Property | Meaning |
| --- | --- | --- |
| Require signed requests | `AcpRequireSignedRequests` | Every ACP request must carry a valid signature. |
| Algorithm | `AcpSignatureAlgorithm` | `ES256` (ECDSA on P-256 with SHA-256), the only one. .NET 10 has no Ed25519 signature API of its own, so Ed25519 isn't offered. |
| Platform's public key | `AcpSignaturePublicKey` | An EC JSON Web Key (`{"kty":"EC","crv":"P-256","x":...,"y":...}`) or a PEM `PUBLIC KEY` block, on P-256. A private key is refused when the platform is saved. The key isn't a secret: it travels in deployments and recipes. |
| Allowed clock skew | `AcpSignatureClockSkewSeconds` | How far `Timestamp` may be from the store's clock, 1 to 3600 seconds; 300 when empty. |

How a request is verified (`AcpRequestSignature` in the Core project, applied by `AcpSignatureCheck`):

- **Signed bytes:** the RFC 8785 (JCS) canonical JSON of the request body, or nothing for a request without a body. Key order, whitespace and number form don't change it; array order does. The timestamp isn't part of the signed bytes: no ACP source defines a `timestamp.body` form, so the store only checks `Timestamp` against the skew window.
- **Signature:** the 64-byte `r || s` form JOSE uses, base64url (padding is allowed). A DER signature is accepted too.
- **When:** after the platform signs in and before any checkout work, including the idempotency replay.

A platform that requires signed requests gets `401` with `type: invalid_request` and `code: invalid_signature` for a missing, stale or wrong signature. A platform that doesn't require them may still sign: when it has a public key, a signed request is verified and refused the same way if it doesn't match. Without a key, the headers are ignored.

```csharp
// How an agent signs a request for this store.
var input = AcpRequestSignature.GetSigningInput(JsonNode.Parse(body));
request.Headers.Add("Signature", AcpRequestSignature.Sign(input, privateKey));
request.Headers.Add("Timestamp", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
```

## Idempotency

The checkout and cart endpoints that change something take an `Idempotency-Key` header, through `WithIdempotency(IdempotencyPolicy)` from `CrestApps.OrchardCore.Commerce.Api.Core`. Keys are kept for each platform.

| | UCP | ACP |
| --- | --- | --- |
| Key required | No; a request without a key isn't de-duplicated | On every POST (`400 idempotency_key_required`) |
| Kept for | 48 hours | 24 hours |
| Key length | At most 255 characters (`400`) | 1 to 255 characters (`400`) |
| Same key, same body | The first answer again | The first answer again, with `Idempotent-Replayed: true` and the key echoed |
| Same body means | The same bytes (SHA-256 of the raw body) | Equal JSON, whatever the key order or number form (RFC 8785 canonical form) |
| Same key, another body | `409 idempotency_conflict` | `422 idempotency_conflict` |
| Same key, first request still running | `409 idempotency_in_flight` | `409 idempotency_in_flight` with `Retry-After: 1` |

`IdempotencyPolicy` sets the header, the lifetime, the key length, the fingerprint and the answer for each case, so another protocol can keep its own rules on the same filter.

ACP over MCP takes the key in `meta.idempotency_key` instead of a header, and follows the same ACP rules with `AcpMcpIdempotency`: keys are kept per platform, tool and checkout, and a replayed answer has `_meta.idempotent_replayed: true`.

## Payment handlers

A payment handler (`IAgentPaymentHandler`) describes a way agents pay and turns the agent's credential into provider data for the checkout engine. The store lists the handlers in the UCP profile and in every checkout answer, and an agent names one, by its id or its name, when it completes a checkout.

| Handler | Name | Offered when | Charged by |
| --- | --- | --- | --- |
| Stripe shared payment tokens (`StripeSharedPaymentTokenHandler`) | UCP `com.stripe.payments`, ACP `dev.acp.tokenized.card` (id `stripe_card`, PSP `stripe`, `requires_delegate_payment`) | The Stripe feature is on and set up. UCP also needs **Stripe business profile** (`StripeBusinessProfileId`, `profile_...`), which agents issue tokens to; ACP sends it as `merchant_id` when it is set. | Stripe, with `payment_method_data[shared_payment_granted_token]` |
| Google Pay through Stripe (`GooglePayPaymentHandler`) | UCP `com.google.pay` | UCP only. The Stripe feature is on with a publishable key, and **Offer Google Pay to UCP agents** is on. The config names Stripe as the gateway, the merchant name (the store's name when empty) and ID, and `TEST` while **Use Google Pay's test environment** is on or Stripe uses test keys. | Stripe, with the card token from the Google Pay token |
| Test credentials (`TestAgentPaymentHandler`) | `dev.crestapps.test` in every protocol | The Test payments feature is on. | The test payment provider |

A platform in **Test mode** is only offered the test handler: the Stripe and Google Pay handlers describe nothing for it. A platform's **Payment handlers** list, when not empty, limits the handlers its agents see and may pay through, by name.

A handler only prepares the payment. The money moves through the checkout engine: `BeginPaymentAsync` with the handler's `ProviderKey` and provider data that carry `CheckoutPaymentDataKeys.DelegatedCredential` and `DelegatedCredentialType` (`DelegatedCredentialTypes.StripeSharedPaymentToken`, `GooglePayToken` or `Test`). The provider charges the credential on the server, with no browser, and the checkout's durable ledger and reconciliation apply as for any payment. See [Checkout](checkout.md#payments-an-ai-agent-brings) and [Stripe](payments.md#payments-ai-agents-bring).

### Test payments

While the Test payments feature is on and its provider is active (see [Checkout](checkout.md#when-the-provider-is-active)), the store offers the `dev.crestapps.test` handler (id `test`, version `2026-11-01`). Its config lists the test credentials an agent sends as the payment's token:

| Credential | Outcome |
| --- | --- |
| `tok_test_success` | The payment succeeds and the checkout completes. |
| `tok_test_decline` | The payment is declined (`payment_declined`). |
| `tok_test_3ds` | The bank asks the buyer to approve the payment: the checkout goes to `requires_escalation` with a continue URL. |

Any other credential is declined. The test handler is offered only to platforms in test mode, and it refuses a payment from any other platform, so a live platform's buyers always pay for real.

## When the buyer must act

Some things only the buyer can do in a browser: approve a payment their bank asks about (3-D Secure), or give details a store checkout step needs that the agent can't fill, such as the store's own checkout fields or a company's approval. Then the checkout's status is `requires_escalation`, a message with the severity `requires_buyer_input` says why, and `continue_url` is the page that opens the store's own checkout in the buyer's browser.

Every checkout that isn't completed, canceled or expired carries a `continue_url`. It is the hand-off link, `agentic-commerce/continue/{token}`, unless the payment provider gave a page of its own for the bank's check. The token is signed with `SignedTokenService` and lasts two days. When the buyer opens it:

- a completed checkout opens its confirmation page;
- a guest's checkout is given to that browser (a new guest token), and a linked buyer's checkout stays theirs;
- the browser is sent to the checkout's next step.

The agent's request runs as the platform, and a store checkout would otherwise take the request's user as its owner. So the store checkout belongs to the linked buyer, or to nobody until the hand-off link gives it to a browser.

## Order updates

When an agent's order changes, the store tells the platform. `AgentOrderEventHandler` (an `IOrderEventHandler`) and, with the Shipping module, `AgentShipmentEventHandler` call `AgentOrderUpdates`, which finds the agent checkout of an order with the `agent` channel and calls each `IAgentOrderPublisher` of the checkout's protocol.

| Event | Update |
| --- | --- |
| The order is paid | A new order |
| The order is canceled, refunded or returned | A changed order |
| A shipment is shipped, delivered or returned (Shipping) | A changed order |

The update is queued with the Webhooks module's [direct deliveries](webhooks.md#direct-deliveries) (`IWebhookOutbox`). It is sent after the work that raised it is saved, signed by the protocol's `IWebhookDeliveryFormatter` at every attempt, and retried after 1 minute, 5 minutes, 30 minutes, 2 hours, 6 hours, 12 hours and 24 hours. Failing to queue an update is logged and never fails the work that changed the order.

| | UCP | ACP |
| --- | --- | --- |
| Sent to | The `webhook_url` in the `dev.ucp.shopping.order` capability of the platform's profile, else the platform's **Order updates address** | The platform's **Order updates address**; nothing is sent without it and a signing secret |
| Body | The UCP order of the checkout's version | `{"type": "...", "data": {order}}` |
| Event | `order.created` or `order.updated` | **ACP new order event** (`order_create` by default) or **ACP changed order event** (`order_update` by default) |
| Signature | RFC 9421 over `@method`, `@authority`, `@path`, `content-digest` and `content-type`, with the store's current ES256 key | `Merchant-Signature: t={unix seconds},v1={hex}`, the HMAC-SHA256 of `{t}.{body}` with the platform's signing secret |
| Other headers | `UCP-Agent` (the store's profile), `Webhook-Id`, `Webhook-Timestamp`, `Content-Digest` | `Request-Id`, `Timestamp` |

The platform's signing secret is made with **New secret** (`whsec_...`), shown once, and kept protected with the tenant's data protection keys. When it can't be read, the delivery fails with a message that asks for a new secret.

### The store's signing keys

`IMerchantSigningKeyService` keeps the store's ES256 (P-256) keys in the `MerchantSigningKeysDocument`, each private key protected with the tenant's data protection keys. The first key is made when it is first needed. The **Agentic commerce signing key rotation** background task runs once a day and makes a new key when the current one is 90 days old. A retired key stays in the profile's `keys` for 7 days, so a platform can still verify what it signed, and is deleted after 28 days. The keys sign the UCP order webhooks, the AP2 merchant authorizations and the AP2 receipts.

## AP2 mandates (preview)

:::warning[Preview]
The AP2 feature follows a protocol that is still changing. Its contracts and behavior can change in a later release.
:::

The **Agent Payments Protocol (Preview)** feature adds AP2's mandates extension to UCP. The profile offers it with `vp_formats_supported` `dc+sd-jwt`. When a platform agrees it:

- Every checkout answer carries `ap2.merchant_authorization`: a JWS with detached content (`b64url(header)..b64url(signature)`, header `alg` and `kid`), ES256 with the store's current key, over the RFC 8785 canonical form of the checkout without its `ap2` member.
- Completing the checkout needs `ap2.checkout_mandate`, the buyer's checkout mandate. The checkout is locked to mandates: there is no completing without one (`mandate_required`).
- `Ap2MandateVerifier` verifies the mandate as AP2 v0.2 does: an SD-JWT delegation chain joined by `~~`, whose root is signed with a key from the platform's profile `keys`, each later hop signed with the previous hop's `cnf.jwk`, and the last hop a key binding JWT with the closed checkout mandate. A single SD-JWT with a key binding JWT is accepted too. Clock skew of 60 seconds is allowed. A platform whose profile has no keys gets `agent_missing_key`.
- The mandate must be for the checkout as it stands: the same checkout id, the same items and quantities, and the same total in minor units. Otherwise the request gets `mandate_scope_mismatch`.
- When the checkout completes, the mandate and a receipt the store signs are kept on the agent checkout, as the evidence for a dispute, and later answers carry `ap2.checkout_receipt`.

A refused mandate is answered with the checkout, an `error` status and an unrecoverable message with the AP2 error code. ACP checkouts don't use AP2.

## Account linking (UCP)

With **UCP Account Linking** on, the profile offers `dev.ucp.common.identity_linking` with two scopes: `dev.ucp.shopping.checkout:manage` (check out as the buyer, with their saved addresses) and `dev.ucp.shopping.order:read` (read the buyer's orders). The buyer links their account through the tenant's OpenID server with the authorization code flow, and the agent then sends the token the buyer's consent gave it.

- `/.well-known/oauth-authorization-server` redirects to the OpenID server's own discovery document, which UCP agents read.
- With `checkout:manage`, the agent's checkouts belong to the buyer (`AgentCheckoutContext.UserId`), so the store checkout and the order are theirs.
- With `order:read`, `GET ucp/v1/orders/{id}` also returns the buyer's own orders.

To set it up, run the `AgenticCommerceAccountLinking` recipe, which turns on the OpenID server, the OpenID token validation and UCP Account Linking, and adds the `dev.ucp.shopping.checkout:manage`, `dev.ucp.shopping.order:read` and `dev.ucp.shopping.order:manage` scopes. Then add an OpenID application for each platform, with the authorization code flow and PKCE, and put its client ID in the platform's **OAuth client ID**.

## Catalog and cart (UCP)

With **UCP Catalog and Cart** on, `UcpCatalog` writes the products for agents from the same items as the product feeds: the same item ids, prices, photos and availability. Only items agents may see (**Show to AI agents**) are returned.

- `catalog/search` takes `query`, `pagination.limit` (at most 50, 20 by default), `pagination.cursor` and `context.currency`, and returns `products` with `pagination.total_count` and `next_cursor`.
- `catalog/lookup` takes up to 50 `ids`, either product ids or feed item ids. `catalog/product` takes one `id`.
- A cart keeps items without a payment or a store checkout. `POST ucp/v1/checkout-sessions` with a `cart_id` and no `line_items` starts a checkout from it.

## UCP over MCP

With **UCP over MCP** on, `POST ucp/mcp` serves UCP's MCP binding: JSON-RPC 2.0 answered as plain JSON, with the MCP protocol version `2025-06-18`. It signs in and negotiates like the REST binding. It answers `initialize`, `ping`, `tools/list` and `tools/call`; a notification gets `202`.

| Tool | Arguments |
| --- | --- |
| `create_checkout` | `checkout`: the same checkout the REST binding takes |
| `get_checkout` | `id` |
| `update_checkout` | `id`, `checkout` |
| `complete_checkout` | `id`, `checkout` with the payment |
| `cancel_checkout` | `id` |

Each tool returns the checkout in `structuredContent`, as the REST binding writes it.

## ACP over MCP

With **ACP over MCP** on, `POST acp/mcp` serves ACP's MCP binding: JSON-RPC 2.0 over MCP Streamable HTTP, answered as plain JSON, with the MCP protocol version `2025-06-18`. `/.well-known/acp.json` then lists `mcp` in `transports`. It answers `initialize`, `ping`, `tools/list` and `tools/call`; a notification gets `202`.

The platform signs in on the HTTP request, with the same API key or OAuth bearer token as REST. Authorization is never a tool argument. A request that can't sign in gets HTTP `401` (or `403`) with a JSON-RPC error `-32000` whose `data` is the ACP error.

Every tool takes `{ meta, id, payload }`:

| Argument | Meaning |
| --- | --- |
| `meta` | The values REST sends as headers: `api_version` (required), `idempotency_key`, `request_id`, `signature`, `timestamp`. Other members are allowed and ignored. |
| `id` | The checkout session's id. Every tool but `create_checkout_session` needs it. |
| `payload` | The REST request body, in the same schema: required for create, update and complete, optional for cancel. |

| Tool | Runs the same code as |
| --- | --- |
| `create_checkout_session` | `POST acp/checkout_sessions` |
| `get_checkout_session` | `GET acp/checkout_sessions/{id}` |
| `update_checkout_session` | `POST acp/checkout_sessions/{id}` |
| `complete_checkout_session` | `POST acp/checkout_sessions/{id}/complete` |
| `cancel_checkout_session` | `POST acp/checkout_sessions/{id}/cancel` |

Both bindings call `AcpCheckoutOperations`, so the checks and answers are the same. Every tool but `get_checkout_session` needs `meta.idempotency_key` (see [Idempotency](#idempotency)). When the platform signs its requests, `meta.signature` and `meta.timestamp` are checked as [ACP request signatures](#acp-request-signatures) are, over the canonical JSON of `payload`, so one signature works for both bindings.

A successful call returns the REST body in `structuredContent` (and as text in `content`), with `meta.request_id` echoed in `_meta.request_id`. Any ACP error, including a missing idempotency key, a conflict, an unknown version or a bad signature, is a JSON-RPC error `-32000` with the ACP error (`type`, `code`, `message`, `param`) in `error.data`. Only malformed arguments (no tool name, an unknown tool, no `meta.api_version`, a missing `id` or `payload`) get `-32602`.

## AI tools

When the AI feature (`CrestApps.OrchardCore.AI`), Product Listing and Product Feeds are on with Agentic Commerce, the module registers three AI tools in the **Commerce** category. Administrators pick them on AI profiles, and can expose them over MCP and WebMCP.

| Tool | What it does |
| --- | --- |
| `searchStoreProducts` | Searches the products (`query`, `limit` 1 to 20, 10 by default) and returns each with its variants, the item id to buy each with, its price and whether it is in stock. |
| `getStoreProduct` | Returns one product (`id`, a product id or a variant's item id) with its description, photos and variants. |
| `startStoreCheckout` | Starts a checkout for `items` (each an item `id` and a `quantity`) and an optional buyer `email`, and returns its total and the `checkout_url` where the buyer enters their address and pays. It never pays. |

The tools use the same catalog and agent checkout as UCP. `startStoreCheckout` keeps its checkouts under the platform id `site-assistant` with the protocol `assistant`, and a signed-in user's checkout is theirs. See [AI Tools](../ai/tools.md#commerce-tools).

## Recipes and deployment

Agent platforms travel with the **AI agent platforms** deployment step and the `AgentPlatforms` recipe step. Credentials never travel: the deployment step leaves the API key hash, its hint and the signing secret out, and the recipe step never reads them. The ACP request signing settings, the platform's public key included, do travel. An imported platform has no API key or signing secret, so give it new ones in the admin.

```json
{
  "name": "AgentPlatforms",
  "Platforms": [
    {
      "Name": "Contoso Agents",
      "Description": "Contoso's shopping agent.",
      "Enabled": true,
      "TestMode": true,
      "Protocols": [ "ucp", "acp" ],
      "OAuthClientId": "contoso-agents",
      "ProfileUrl": "https://agents.contoso.com/.well-known/ucp",
      "WebhookUrl": "https://agents.contoso.com/webhooks/orders",
      "AllowedPaymentHandlers": [ "com.stripe.payments", "dev.acp.tokenized.card" ],
      "AllowedIpRanges": [ "203.0.113.0/24" ],
      "AcpOrderCreatedEvent": "order_create",
      "AcpOrderUpdatedEvent": "order_update",
      "AcpRequireSignedRequests": true,
      "AcpSignatureAlgorithm": "ES256",
      "AcpSignaturePublicKey": "{\"kty\":\"EC\",\"crv\":\"P-256\",\"x\":\"...\",\"y\":\"...\"}",
      "AcpSignatureClockSkewSeconds": 300
    }
  ]
}
```

`AgenticCommerceSettings` travels with Orchard Core's `Settings` recipe step:

```json
{
  "name": "Settings",
  "AgenticCommerceSettings": {
    "CheckoutLifetimeHours": 6,
    "MaxOpenCheckoutsPerPlatform": 1000,
    "AllowUnregisteredUcpPlatforms": false,
    "StripeBusinessProfileId": "profile_123",
    "EnableGooglePay": true,
    "GooglePayMerchantId": "BCR2DN4T000000",
    "GooglePayMerchantName": "Contoso",
    "GooglePayTestEnvironment": true
  }
}
```

The module adds two recipes, which you run from **Configuration > Recipes**:

| Recipe | What it does |
| --- | --- |
| `AgenticCommerceSample` (Agentic commerce: sample) | Turns on Test payments, Agentic Commerce, UCP, UCP Catalog and Cart, UCP over MCP, ACP, ACP over MCP and Agent Product Feeds, and adds a *Test agent platform* in test mode for UCP and ACP. Give it an API key under **Commerce > AI agents**. |
| `AgenticCommerceAccountLinking` (Agentic commerce: UCP account linking) | Turns on the OpenID server, OpenID token validation and UCP Account Linking, and adds the UCP scopes. See [Account linking](#account-linking-ucp). |

The module has no `appsettings.json` configuration of its own.

## Extension points

| Contract | Project | Use it to |
| --- | --- | --- |
| `IAgentPaymentHandler` | AgenticCommerce.Abstractions | Add a way agents pay. |
| `IAgentCheckoutService` | AgenticCommerce.Core | Create, change, complete and cancel agent checkouts from another binding. |
| `IUcpProfileContributor` | AgenticCommerce (module) | Add a capability, an extension or a transport to the UCP profile. |
| `IUcpCheckoutExtension` | AgenticCommerce (module) | Add members to every UCP checkout answer, check a complete request, and act once the checkout is completed. |
| `IAcpDiscoveryContributor` | AgenticCommerce (module) | Add to the ACP discovery document, such as a transport. |
| `IAgentOrderPublisher` | AgenticCommerce (module) | Send a protocol's order updates. |
| `IWebhookOutbox`, `IWebhookDeliveryFormatter` | Webhooks.Abstractions | Queue a signed delivery to one receiver, and sign it the way its protocol asks. See [Direct deliveries](webhooks.md#direct-deliveries). |
| `ICheckoutContactStep`, `ICheckoutFulfillmentStep` | Checkout.Abstractions | Fill the Contact and Shipping steps from code. See [Checkout](checkout.md#filling-steps-from-code). |
| `IdempotencyPolicy` | Commerce.Api.Core | Keep a protocol's own idempotency rules. |

### Add a payment handler

A handler describes itself for the request's protocol, version, platform and test mode, and returns `null` when it can't be offered. `PrepareAsync` returns the provider key and provider data the checkout engine charges. Put the agent's credential only in `DelegatedCredential`, never in a key a browser can send.

```csharp
public sealed class ContosoPayHandler : IAgentPaymentHandler
{
    public const string HandlerName = "com.contoso.pay";

    public string GetName(string protocol)
        => HandlerName;

    public Task<AgentPaymentHandlerDescriptor> DescribeAsync(AgentPaymentHandlerContext context, CancellationToken cancellationToken = default)
    {
        // A platform in test mode only pays with test credentials.
        if (context.TestMode || context.Protocol != AgenticCommerceConstants.Protocols.Ucp)
        {
            return Task.FromResult<AgentPaymentHandlerDescriptor>(null);
        }

        return Task.FromResult(new AgentPaymentHandlerDescriptor
        {
            Id = "contoso_pay",
            Name = HandlerName,
            Version = "2026-01-01",
            Spec = "https://pay.contoso.com/ucp/spec",
            Schema = "https://pay.contoso.com/ucp/schema.json",
            Psp = "contoso",
            DisplayName = "Contoso Pay",
            InstrumentTypes = ["card"],
            Config = new JsonObject { ["environment"] = "production" },
        });
    }

    public Task<AgentPaymentPreparation> PrepareAsync(AgentPaymentHandlerContext context, AgentPayment payment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payment.Credential))
        {
            return Task.FromResult(AgentPaymentPreparation.Fail("The payment has no Contoso Pay token."));
        }

        return Task.FromResult(new AgentPaymentPreparation
        {
            Succeeded = true,
            ProviderKey = "contoso",
            ProviderData = new Dictionary<string, string>
            {
                [CheckoutPaymentDataKeys.DelegatedCredential] = payment.Credential.Trim(),
                [CheckoutPaymentDataKeys.DelegatedCredentialType] = "contoso_token",
            },
        });
    }
}
```

Register it in a startup of the Agentic Commerce feature, gated on the feature of its payment provider:

```csharp
[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
[RequireFeatures("Contoso.Payments")]
public sealed class ContosoAgentPaymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IAgentPaymentHandler, ContosoPayHandler>();
}
```

The `ICheckoutPaymentProvider` with the key `contoso` sets `AcceptsDelegatedCredentials` and, in `BeginAsync`, charges the credential on the server when `CheckoutPaymentDataKeys.DelegatedCredential` is in the provider data.

### Add a UCP capability or extension

An `IUcpProfileContributor` adds the capability to the profile for each version. An extension names its parent in `Extends`, so negotiation drops it when the parent isn't agreed. An `IUcpCheckoutExtension` then writes its members on every answer the platform agreed it for, and may refuse a complete request with a message.

```csharp
public sealed class GiftNoteProfileContributor : IUcpProfileContributor
{
    public const string Name = "com.contoso.gift_note";

    public Task ContributeAsync(UcpProfileContext context)
    {
        context.Capabilities.Add(new UcpCapability
        {
            Name = Name,
            Version = context.Version,
            Spec = "https://contoso.com/ucp/gift-note",
            Schema = "https://contoso.com/ucp/gift_note.json",
            Extends = [UcpConstants.Capabilities.Checkout],
        });

        return Task.CompletedTask;
    }
}

public sealed class GiftNoteCheckoutExtension : IUcpCheckoutExtension
{
    public Task WritingAsync(UcpCheckoutContext context)
    {
        if (context.Request.Has(GiftNoteProfileContributor.Name))
        {
            context.Json["gift_note"] = new JsonObject { ["allowed"] = true };
        }

        return Task.CompletedTask;
    }

    public Task<AgentCheckoutMessage> CompletingAsync(UcpCheckoutContext context)
        => Task.FromResult<AgentCheckoutMessage>(null);

    public Task CompletedAsync(UcpCheckoutContext context)
        => Task.CompletedTask;
}
```

```csharp
services.AddScoped<IUcpProfileContributor, GiftNoteProfileContributor>();
services.AddScoped<IUcpCheckoutExtension, GiftNoteCheckoutExtension>();
```

### Send a protocol's order updates

An `IAgentOrderPublisher` names its protocol and queues a direct delivery; an `IWebhookDeliveryFormatter` with the same name signs it each time it is sent. Put only ids in `Data`: the formatter reads secrets itself, when it sends.

```csharp
public sealed class ContosoOrderPublisher : IAgentOrderPublisher
{
    private readonly IWebhookOutbox _outbox;

    public ContosoOrderPublisher(IWebhookOutbox outbox)
    {
        _outbox = outbox;
    }

    public string Protocol
        => "contoso";

    public Task PublishAsync(AgentCheckout checkout, Order order, bool created, CancellationToken cancellationToken = default)
        => _outbox.EnqueueAsync(new WebhookDirectDelivery
        {
            OwnerId = checkout.PlatformId,
            Url = "https://agents.contoso.com/orders",
            EventName = created ? "order.created" : "order.updated",
            Body = JsonSerializer.Serialize(new { id = order.ItemId, status = order.Status.ToString() }),
            FormatterName = "contoso-order",
            Data = new Dictionary<string, string> { ["platformId"] = checkout.PlatformId },
        }, cancellationToken);
}
```

## Limitations

- **ACP request signatures** are verified with ES256 only. Ed25519 isn't offered, because .NET 10 has no Ed25519 signature API of its own.
- **ACP's delegate payment endpoint** isn't hosted by the store. ACP agents pay with a token their payment service provider issued, such as a Stripe shared payment token.
- **UCP's A2A binding** and **embedded checkout** aren't built. UCP and ACP are served over REST and MCP.
- **ACP over MCP** serves tool schemas with `payload` as a plain object; it doesn't bundle ACP's JSON Schemas into `tools/list`.
- **Delegated 3-D Secure** isn't built. When a bank asks the buyer to approve a payment, the checkout goes to `requires_escalation` and the buyer approves it in the store's checkout.
- **Account linking** has no authorization server metadata of its own. `/.well-known/oauth-authorization-server` redirects to the OpenID server's discovery document.
- **Stripe shared payment tokens** are a preview of Stripe's API. The store calls Stripe with the preview API version `2026-09-30.preview`.
- **AP2** is a preview, for UCP only.

## Agent product feeds

The **Agent Product Feeds** feature (`CrestApps.OrchardCore.AgenticCommerce.Feeds`, which turns on Agentic Commerce and Product Feeds) publishes the store's products for AI agents, from the same items as the [product feed](products.md#product-feeds):

- `feeds/openai/products.jsonl.gz` and `feeds/openai/products.csv.gz` (or `.jsonl` and `.csv` uncompressed) in OpenAI's product feed format for ChatGPT, with `?currency=` and the product feed's `?key=`.
- A daily push of the full OpenAI feed over SFTP.
- An Agentic Commerce Protocol (ACP) feed API client that keeps agent platforms that host one up to date.

**Settings > Commerce > Agent feeds** (permission `ManageAgentFeeds`, settings group `agent-feeds`) shows the feed addresses, how many items are in the feed and why the others were left out, the SFTP push and the feed APIs, the result of the last pushes, and a **Push now** button. See [Product listings](../user-manual/commerce/product-listings.md#feeds-for-ai-agents) for the screen.

### The OpenAI feed

`OpenAIProductFeedService` turns the store's product feed into `OpenAIProductFeedRecord`s with `OpenAIProductFeedWriter.ToRecord` and keeps them as long as the product feed it comes from, so a stock change rebuilds both. The fields:

| Field | From the feed item |
| --- | --- |
| `item_id`, `group_id`, `listing_has_variations`, `variant_dict` | The item id (`ProductFeedItemIds`), the product's id for a variant, and its options. |
| `title`, `description`, `url`, `image_url`, `additional_image_urls` | As in the product feed. |
| `brand`, `gtin`, `mpn`, `condition`, `product_category`, `color`, `size` | As in the product feed; `product_category` is the taxonomy path, else a Google category given by name. |
| `availability` | `in_stock`, `out_of_stock`, `pre_order`, `backorder`, or `unknown`. |
| `price`, `sale_price`, `shipping_price` | Money as `79.99 USD`, at the currency's precision (`CurrencyScale`). |
| `weight`, `item_weight_unit`, `length`, `width`, `height`, `dimensions_unit` | The Shipping contributor's values. |
| `accepts_returns`, `return_deadline_in_days`, `return_policy`, `accepts_exchanges` | The Returns contributor's values and the store profile's return policy link. |
| `seller_name`, `seller_url`, `seller_privacy_policy`, `seller_tos` | The store profile. |
| `is_digital` | The product type is Digital. |
| `is_eligible_search`, `is_eligible_checkout` | **Show to AI agents** and **Let AI agents check out**. An item agents may not show stays in the file with `is_eligible_search` set to `false`, which removes it from ChatGPT sooner than leaving it out. |
| `store_country`, `target_countries` | The store profile's country, and `AgentFeedSettings.TargetCountries` (the store's country when empty). |

A field without a value is left out of a JSON line and empty in a CSV cell. In CSV, booleans are `true` or `false`, lists are joined by commas and `variant_dict` is a JSON object; cells are escaped with `CsvWriter`.

`OpenAIProductFeedValidator` leaves out an item that breaks one of OpenAI's rules, and the settings page lists why: a missing required field (`item_id`, `title`, `description`, `url`, `brand`, `seller_name`, `image_url`, `availability`, `price`), an availability OpenAI doesn't know, money not written as an amount and an uppercase code, a sale price not below the price in the same currency, a GTIN without its check digit, a variant without a `group_id` other than its `item_id` or without `variant_dict`, a weight or size without its unit, and an item agents may check out without `seller_privacy_policy` and `seller_tos`. Items the product feed already left out are listed too.

### The SFTP push

When **Push the full feed over SFTP every day** is on, `AgentFeedPushBackgroundTask` (daily at 02:30) has `AgentFeedSftpPusher` write the whole catalog to the SFTP folder in shards of at most 500,000 items, ordered by item id, with stable names (`products-00001.jsonl.gz`, `products-00002.jsonl.gz`, …) that each push overwrites. Shards a smaller catalog no longer needs, and files of the other format, are deleted. The push signs in with the password, the private key (OpenSSH or PEM, with its passphrase), or both, all stored protected with the tenant's data protection keys, and checks the server's SHA-256 host key fingerprint when one is entered. The result is kept in the `AgentFeedStatusDocument`.

### ACP feed APIs

Each `AcpFeedTarget` in `AgentFeedSettings.FeedApiTargets` has a name, the feed API's https base address, the platform's bearer key (stored protected), a feed id, a target country, a currency and an enabled flag. `AcpFeedPusher`:

- creates the feed with `POST /feeds` (`{"target_country": "US"}`) when the target has no feed id, and keeps the id it gets in the `AgentFeedStatusDocument`;
- upserts complete products with `PATCH /feeds/{id}/products` in batches of 100, every product in the daily push and on **Push now**, and the changed products after a product is published or, with Inventory on, after its available units change (in a deferred task, once per unit of work).

Every call sends `Authorization: Bearer`, `API-Version: 2026-04-17` and an `Idempotency-Key` made from the target, the feed and the body sent, so a retry is the same request. Calls go through the egress-guarded HTTP client, which refuses addresses on the site's own network. Only items agents may show are sent; a product unpublished since the last push stays on the platform until it is changed there.

`AcpFeedProductMapper` makes one ACP `Product` per store product (`id`, `title`, `description.plain`, `url`, `media`) with each item as a `Variant`: `id` (the item id checkouts accept), `title`, `barcodes` (`gtin`), `price` and, during a sale, `list_price` as `{amount, currency}` in minor units with an uppercase code, `availability` (`available`, `status`), `categories`, `condition`, `variant_options`, `media`, and `seller` with its `terms_of_use`, `privacy_policy` and `return_policy` links.

### Recipes

`AgentFeedSettings` travels with Orchard Core's `Settings` recipe step. The secrets are stored protected, so enter them again on another site.

```json
{
  "name": "Settings",
  "AgentFeedSettings": {
    "TargetCountries": [ "US", "CA" ],
    "Sftp": {
      "Enabled": true,
      "Host": "sftp.example.com",
      "Port": 22,
      "Username": "contoso",
      "RemoteFolder": "/",
      "Format": "jsonl.gz"
    },
    "FeedApiTargets": [
      {
        "Id": "01kfeedtarget0000000000000",
        "Name": "Example agent",
        "BaseUrl": "https://api.example.com/acp",
        "TargetCountry": "US",
        "Enabled": true
      }
    ]
  }
}
```

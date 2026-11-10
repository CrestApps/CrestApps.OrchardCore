# Agentic commerce plan: UCP, ACP and AP2

Status: built, 2026-11-09, finished 2026-11-10, in one branch and PR. [As built](#as-built) at the end says what changed from the plan and what is left. This plan follows the [commerce parity plan](commerce-parity-plan.md), whose phase 6 headless API, phase 10 product feeds and checkout engine it builds on.

The goal is to let AI agents discover a store, find its products, check out and pay on a buyer's behalf, and follow the order afterwards, through the open standards those agents speak:

- **UCP**, the Universal Commerce Protocol (Google and Shopify, with Stripe on its council). It covers discovery, catalog, cart, checkout, orders, account linking and payment handlers. Google AI Mode and Gemini use it.
- **ACP**, the Agentic Commerce Protocol (OpenAI and Stripe). It covers a checkout API, delegated payment, order webhooks and a product feed. ChatGPT uses it.
- **AP2**, the Agent Payments Protocol (Google, given to the FIDO Alliance in April 2026). It adds signed mandates that prove the buyer approved this exact purchase. It rides on a commerce protocol; UCP carries it through an extension.

[Agentic commerce protocols reference](agentic-commerce-protocols-reference.md) has the details this plan relies on: endpoints, schemas, headers, signatures, status values and the defects in each spec, with links to the sources. Read it before starting a slice. It was compiled from the specs' own repositories on 2026-11-02.

## Versions this plan targets

| Protocol | Version | Why |
| --- | --- | --- |
| UCP | `2026-08-25`, plus `2026-04-08` through `supported_versions` | 2026-08-25 is the latest release. Google's native checkout guide still targets 2026-04-08, and the two differ in the fulfillment shapes, consent and where signing keys live. |
| ACP | `2026-04-17`, plus `2026-01-30` | 2026-04-17 is the latest release. 2026-01-30 introduced the payment handler shapes we accept. We also accept the older `payment_data { token, provider }` shape because OpenAI's own docs still show it. |
| AP2 | v0.2 | v0.2 replaced the Intent, Cart and Payment mandates of v0.1 with a Checkout Mandate and a Payment Mandate (SD-JWT). FIDO may change the wire format before 1.0, so AP2 ships as a preview feature. |

Each protocol adapter keeps its version handling in one place, so adding the next dated version is a mapper, not a rewrite.

## What the market looks like (November 2026)

This decides the order of the work:

- **Google is the live checkout consumer.** Merchants who want purchases inside AI Mode and Gemini implement UCP checkout. Stripe's "use your own checkout" guide for agentic sellers now covers UCP only.
- **OpenAI is feed-first.** Trade press reported in March 2026 that ChatGPT Instant Checkout was scaled back. That report is unverified, but OpenAI's onboarding now centres on the product feed, open to approved partners. The ACP checkout API is still the open standard and is still documented, so we build it, after UCP.
- **Stripe's Shared Payment Token (`spt_…`) serves both.** A UCP `com.stripe.payments` credential and an ACP delegated-payment credential are charged the same way: a PaymentIntent confirmed with `payment_method_data[shared_payment_granted_token]`. One Stripe change serves both protocols.
- **AP2 adoption is early.** No production volume is documented. It's worth building once UCP is solid, because the mandate is what lets an agent buy while the buyer isn't watching.

## Where we already stand

Most of what these protocols need already exists. This table is the starting point; the gaps below it are what the plan builds.

| Need | What we have | Where |
| --- | --- | --- |
| A checkout an API can drive | Minimal API endpoints for the cart, checkout steps, contact, shipping rates, codes, payment and completion, with guest tokens in headers | `Commerce.Api.Core` (`CommerceApi`, `ApiProblems`), each module's `… API` feature |
| One engine that moves money | `ICheckoutEngine`: a durable `PaymentAttempt` before the provider is called, completion only on confirmed settlement, compensation and reconciliation | `Checkout.Core/Services/DefaultCheckoutEngine.cs` |
| Step logic shared by the pages and the API | `CartCheckoutService`, `CustomerContactStepService`, `ShippingCheckoutStepService`, `CheckoutCouponService` | Orders, Customers, Shipping, Checkout |
| Totals with tax, shipping and discounts | `CheckoutInvoiceBuilder` → `CheckoutInvoice` (lines, discounts, tax lines, tenders) | `Checkout.Core` |
| A hook before money moves | `ICheckoutPaymentGate` (coupon and tender holds and company approval use it) | `Checkout.Core/Services/ICheckoutPaymentGate.cs` |
| Stock held while paying | `IInventoryService.ReserveAsync` / `CommitAsync` / `ReleaseAsync` | `Inventory.Core` |
| Stripe with idempotent intents, refunds and verified webhooks | `StripeCheckoutPaymentProvider`, `IStripePaymentIntentService` (`ConfirmAsync`, `ChargeOffSessionAsync`) | `Stripe`, `Stripe.Core` |
| Idempotent API calls | `IdempotencyEndpointFilter` (24 hours, distributed lock, replay with headers) | `Commerce.Api.Core` |
| Signed outgoing webhooks with retries and a delivery log | Commerce Webhooks (HMAC-SHA256, backoff for about two days, internal hosts refused) | `Commerce.Webhooks` |
| Order and shipment events | `IOrderEventHandler`, `IShipmentEventHandler`, `OrderWebhookEventHandler` | `Orders.Core`, `Shipping.Core` |
| A product feed | Google Merchant Center RSS and CSV, per currency, validated, with GTIN, MPN, brand and condition on `ProductFeedPart` | `Products/Feeds` |
| Catalog search and product detail priced for the caller | `IProductSearchService`, `CatalogApiProductReader`, `IPriceResolver`, `IProductAvailabilityProvider` | `Products`, `Products.Core` |
| An OAuth 2.0 server | Orchard Core's OpenId module; the commerce API already takes its bearer tokens (`Api` scheme) | upstream `OrchardCore.OpenId` |
| AI agent surfaces | MCP server, A2A host (`/.well-known/agent-card.json`), WebMCP, and `AddCoreAITool` for tools | `AI.Mcp`, `AI.A2A`, `AI.WebMcp` |

## Gaps

| # | Gap | Why it matters | Slice |
| --- | --- | --- | --- |
| G1 | A checkout can only start from the caller's cart | UCP and ACP create a checkout from line items in one call | AC1 |
| G2 | An open checkout's lines can't change | Both protocols replace the line items on update | AC1 |
| G3 | Stripe can't be confirmed on the server with a token the agent brings; the client always confirms with Stripe.js | Agent payments arrive as an SPT or a Google Pay token, and there is no browser | AC2 |
| G4 | No store policy links (terms, privacy, returns, shipping) and no seller name outside receipts | UCP and ACP responses carry them, Google requires privacy and terms, and OpenAI's feed requires them for checkout | AC0 |
| G5 | No per-agent identity: the API knows users and guests, not platforms | Each agent platform has its own credentials, webhook address, secret, allowed payment handlers and test mode | AC3 |
| G6 | No ES256 signing keys, JWKS, RFC 9421 HTTP message signatures, RFC 8941 structured fields or RFC 8785 JSON canonicalization | UCP order webhooks must be signed with RFC 9421; AP2 needs ES256 JWS over JCS | AC4 |
| G7 | The idempotency filter keys on the route and key only, not the body | UCP answers a reused key with a different body with 409, and ACP with 422 | AC3 |
| G8 | Webhooks have one payload shape and one signature scheme, and admins add every address by hand | UCP needs RFC 9421-signed full-order snapshots; ACP needs `Merchant-Signature`; both go to an address the agent gives | AC5 |
| G9 | No `/.well-known` commerce discovery, and tenants with a URL prefix can't serve the host root | UCP needs `/.well-known/ucp`; ACP has an optional `/.well-known/acp.json` | U1, A1 |
| G10 | The feed lacks stock quantity, seller, policies, shipping price, return window, weight and dimensions, agent eligibility flags, and a JSON lines output | OpenAI's feed requires several of these; ACP's feed schema differs from Merchant Center's | F1 |
| G11 | One item id is needed everywhere | Google requires a UCP line item's `item.id` to equal the product feed's id; ACP's `Item.id` is the feed variant's id | AC1 |
| G12 | No mandate verification, evidence store or merchant-signed checkout | AP2 | P1 |
| G13 | No commerce AI tools, and WebMCP refuses anonymous visitors | The site's own agents and generic MCP clients can't shop | T1 |
| G14 | Orders have no agent channel or attribution | Reports, the orders list and webhooks need to know an agent made the order and which one | AC0 |

## Design

### One agent checkout, several protocols

The protocols are thin adapters over one protocol-neutral service:

```
Google / ChatGPT / other agents
        │ UCP REST · ACP REST · (later) UCP MCP · ACP MCP
        ▼
Protocol adapters (Ucp, Acp): versions, headers, auth, shapes, status mapping, errors
        ▼
IAgentCheckoutService  ── AgentCheckoutInfo on the CheckoutSession (protocol, platform, version, capabilities, webhook)
        │ the same step services the storefront and the API use
        ▼
CartCheckoutService · CustomerContactStepService · ShippingCheckoutStepService · CheckoutCouponService
        ▼
ICheckoutEngine ── ICheckoutPaymentGate (AP2 mandate check) ── ICheckoutPaymentProvider (Stripe with a delegated credential)
        ▼
Order (Channel = "agent") ── IOrderEventHandler / IShipmentEventHandler ── protocol order webhooks
```

Rules that keep this safe:

- **The engine stays the only thing that moves money.** An agent's credential becomes `ProviderData` on `BeginPaymentAsync`; the adapters never call Stripe. The storefront and headless API's `SanitizeProviderData` drops the new delegated-credential keys, so only the agent path can set them.
- **No new pricing, tax, stock or discount logic.** Everything an agent sees comes from the step services and the invoice, so a price on the site and a price in an agent always agree.
- **The agent checkout is the internal checkout.** Its protocol data is an `AgentCheckoutInfo` part in the `CheckoutSession`'s property bag, with an index for lookups by protocol id, platform and order. The id an agent sees is an opaque public id mapped to the session, never the internal one.
- **Deterministic answers.** The same session state always maps to the same status, messages and totals, as UCP requires.

### Status mapping

| Internal state | UCP | ACP |
| --- | --- | --- |
| A step other than Payment is unfinished (no email, no address, no shipping choice) | `incomplete`, an error message per missing field with its JSONPath | `not_ready_for_payment` (`incomplete` before 2026-01-30) |
| Only payment is left | `ready_for_complete` | `ready_for_payment` |
| Needs the buyer in a browser (3D Secure, approval, a step the protocol can't express) | `requires_escalation` with `continue_url` | `requires_escalation`, or `authentication_required` once delegated 3DS is built |
| `AwaitingProvider` / `PaymentPending` | `complete_in_progress` | `complete_in_progress` |
| Company approval pending | `requires_escalation` | `pending_approval` |
| `Completed` | `completed` with `order` | `completed` with `order` |
| `Canceled` / `Failed` | `canceled` | `canceled` |
| `Expired` | `canceled` | `expired` |

`continue_url` opens the storefront checkout at `/Checkout/{sessionId}/{step}` with a single-use hand-off token that binds the buyer's browser to the session. The buyer finishes there, and the agent's next read reports the result.

### Totals, messages and links

- Money is integer minor units in both protocols (`Money.ToMinorUnits`). UCP discounts are negative; ACP discounts are positive and shown as subtractive. Every response is checked against both protocols' rule that the parts add up to the total.
- Totals come from `CheckoutInvoice`: subtotal from the product lines, fulfillment from the shipping line, discount from `Discounts`, tax from `TaxAmount`, fees from any other billing line, then total. UCP gets `lines[]` breakdowns of the tax lines.
- Messages come from the same reasons the API's problems use (`out-of-stock`, `not-available`, `invoice-changed`, …), mapped to each protocol's codes (`out_of_stock`, `item_unavailable`, `payment_failed`, `address_undeliverable`, …) and severities.
- Links come from the store policies (AC0): UCP `privacy_policy`, `terms_of_service`, `refund_policy`, `shipping_policy`, `faq`; ACP `privacy_policy`, `terms_of_use`, `return_policy`, `shipping_policy`, `contact_us`, `faq`.

### Fulfillment

`ShippingCheckoutStepService.QuoteAsync` gives the options for an address; pickup locations give the pickup methods. UCP 2026-08-25 receives methods, destinations, groups and options; UCP 2026-04-08 its older shapes; ACP `fulfillment_options[]` and `selected_fulfillment_options[]`. Choosing an option calls `ShippingCheckoutStepService.SaveAsync`, which rebuilds the invoice with tax. A digital-only order has no fulfillment.

### Item ids

`IAgentItemIdResolver` turns an item id into a product, a variant and a price, and back. It uses the same rule as the product feed: the SKU, or `{contentItemId}-{variantId}` without one. The feed builder and both adapters call it, so the id an agent copies from a feed always resolves at checkout.

### Payment handlers

A payment handler is what a protocol calls the way an agent pays. `IAgentPaymentHandler` describes one: its protocol name, its configuration for the discovery document and each response (filtered by the cart's currency and contents), the instrument types it takes, and how a credential becomes `ProviderData` for a payment provider.

| Handler | Protocol name | Credential | Charged by |
| --- | --- | --- | --- |
| Stripe Shared Payment Token | UCP `com.stripe.payments`; ACP `dev.acp.tokenized.card` with `psp: stripe` | `spt_…` | Stripe: a PaymentIntent with `payment_method_data[shared_payment_granted_token]`, confirmed on the server |
| Google Pay through Stripe | UCP `com.google.pay` | A Google Pay token whose gateway is Stripe | Stripe: the token's Stripe card token as `payment_method_data[card][token]`, confirmed on the server (to verify against Stripe's Google Pay docs in AC2) |
| Test | `dev.crestapps.test` (UCP and ACP) | `tok_test_success`, `tok_test_decline`, `tok_test_3ds` | The test payment provider, so the conformance suites and demos run without a gateway |

New `PaymentProviderCapabilities.AcceptsDelegatedCredentials` marks a provider that can be confirmed on the server with a credential it is given. Stripe gains it in AC2. Adyen, PayPal and other gateways are later handlers on the same seam.

### Agent platforms

An **agent platform** is a business whose agents buy from the store, such as Google or OpenAI, or a test platform. It is an admin-managed record:

- Name, the protocols it uses, enabled, and test mode (test mode allows only test handlers).
- How it signs in, one or more of:
  - an OAuth client of the tenant's OpenId server (client credentials);
  - an API key (ACP's bearer key), stored hashed and shown once;
  - for UCP, a profile URL whose `keys[]` verify RFC 9421-signed requests, with no prior registration needed.
- The webhook address and secret for ACP. UCP takes the address from the platform's profile.
- The payment handlers it may use, an optional IP allowlist (OpenAI requires one), and the ACP webhook event names (the spec says `order_create`, OpenAI's docs `order_created`).

Every request is matched to a platform, and the platform must be allowed to act for the profile in `UCP-Agent`. A UCP platform the store doesn't know is accepted only when its RFC 9421 signature verifies against its profile and the store allows open onboarding (off by default).

### Signing

`AgenticCommerce.Core` owns the cryptography, all on the BCL (`ECDsa` P-256 in IEEE P1363 format, `SHA256`, `HMACSHA256`):

- **Merchant keys.** ES256 key pairs, the private half data-protected at rest, published as a JWK set in `/.well-known/ucp`'s `keys[]`. A key is added, signs once it's current, stays published for at least 7 days after it stops signing, then goes. A background task rotates every 90 days, and admins can rotate now.
- **RFC 9421 HTTP message signatures** with RFC 9530 `Content-Digest` and the RFC 8941 structured fields they need: sign order webhooks and complete responses, verify signed requests.
- **RFC 8785 JSON canonicalization** for AP2's merchant authorization. No maintained .NET library is assumed; it's small enough to own and test against the RFC's vectors.
- **HMAC** `t=…,v1=…` for ACP webhooks.
- **Fetching a platform's profile** uses the webhook module's network guard (no internal hosts, no redirects), caches by `Cache-Control`, and checks UCP's authority binding on schema URLs.

### Idempotency

The existing filter gains a body fingerprint: SHA-256 of the raw body for UCP, and of the RFC 8785 canonical body for ACP, which compares JSON by meaning. The same key with a different body gets 409 (UCP) or 422 `idempotency_conflict` (ACP); a request still running gets 409 with `Retry-After`; a replay adds `Idempotent-Replayed: true` for ACP. Keys last 48 hours for the agent endpoints (UCP recommends 48, ACP requires at least 24). The scope is the platform plus the route.

### Order webhooks

Commerce Webhooks gains two seams, so agent webhooks keep its delivery log, retries, resend and network guard:

- `IWebhookPayloadFormatter` builds the body. Formatters: the existing commerce shape, the UCP full order snapshot, and the ACP `{ type, data: Order }` event.
- `IWebhookSigner` signs it. Signers: the existing `X-Webhook-Signature`, RFC 9421 with the merchant key, and ACP's `Merchant-Signature`.

An agent order gets a system-managed subscription for its platform's address; admins see it in the deliveries log but don't edit it. `IOrderEventHandler` (created, paid, canceled, refunded, returned, lines changed) and `IShipmentEventHandler` (shipped, delivered) send the order's full current state, never a delta. UCP fulfillment events and ACP fulfillments come from shipments and their tracking; adjustments come from refunds and returns.

### Tenants and discovery

`/.well-known/ucp` and `/.well-known/acp.json` must be at the host's root. A tenant with its own host serves them. A tenant reached through a URL prefix (`example.com/shop`) can't, so its settings say discovery needs a host of its own, and the REST endpoints still work under the prefix. Unlike the A2A card, base URLs are built from `PathBase`, so prefixed tenants get correct links.

## Module layout

One new module, `CrestApps.OrchardCore.AgenticCommerce`, with its shared code in `src/Core/CrestApps.OrchardCore.AgenticCommerce.Core`. Category Commerce; every feature depends on Commerce and Checkout.

| Feature | Id | Depends on | Gives |
| --- | --- | --- | --- |
| Agentic Commerce | `CrestApps.OrchardCore.AgenticCommerce` | Orders, Customers, Products | Agent platforms, merchant keys, `IAgentCheckoutService`, payment handlers, settings, the agent order channel |
| Universal Commerce Protocol | `….AgenticCommerce.Ucp` | Agentic Commerce, Shipping | `/.well-known/ucp`, UCP checkout, fulfillment, discount, order and buyer consent, signed order webhooks |
| UCP account linking | `….AgenticCommerce.Ucp.IdentityLinking` | UCP, `OrchardCore.OpenId.Server` | OAuth account linking, the UCP scopes, buyer-scoped orders and addresses |
| UCP catalog and cart | `….AgenticCommerce.Ucp.Catalog` | UCP, Catalog API | `catalog.search`, `catalog.lookup`, the UCP cart |
| UCP over MCP | `….AgenticCommerce.Ucp.Mcp` | UCP | UCP's MCP binding at its own endpoint |
| Agentic Commerce Protocol | `….AgenticCommerce.Acp` | Agentic Commerce, Shipping | ACP checkout, `/.well-known/acp.json`, ACP order webhooks |
| Agent product feeds | `….AgenticCommerce.Feeds` | Agentic Commerce, Product Feeds | OpenAI's file feed (JSON lines and CSV, pushed over SFTP) and the ACP feed API client |
| Agent Payments Protocol (preview) | `….AgenticCommerce.Ap2` | UCP | The `dev.ucp.common.payment.ap2_mandate` extension |
| Commerce AI tools | `….AgenticCommerce.AITools` | Agentic Commerce, `CrestApps.OrchardCore.AI` | Product search, details, cart and checkout tools for the site's own AI agents, MCP, A2A and WebMCP |

The Stripe module adds the delegated-credential path to its provider (AC2); no new Stripe feature is needed.

Permissions: **Manage agent platforms**, **Manage agentic commerce settings** and **View agent checkouts**. The agent endpoints authorize the platform, not a user, except buyer-scoped calls under account linking. Admin lists (agent platforms, keys) render through `<admin-list>` per `AGENTS.md`. The admin menu goes under Commerce › Agentic commerce, and the store setup guide gets a "Sell through AI agents" step.

## Slices

### Phase 0: foundation in existing modules

- **AC0 store policies and the agent channel.**
  - A Store policies settings group (Settings › Commerce): seller name (defaulting to the receipt business name), and links to the terms, privacy, returns, shipping, FAQ, contact and about pages, each a content item or URL.
  - `OrderChannels.Agent = "agent"`, plus `Order` agent attribution (platform id, protocol, agent checkout id, the platform's attribution data), indexed. The orders list, its export, the admin API and reports filter by it.
- **AC1 a checkout from items.**
  - `IAgentCheckoutService.CreateAsync(items, buyer, fulfillment, context)` builds a server-owned cart (no user, its own guest token) through `ICartService`, then calls `CartCheckoutService.StartAsync`, so stock and coupon holds and pricing are the storefront's.
  - `UpdateAsync` replaces lines. `OrderService.ReplaceLinesAsync` is extended to orders awaiting payment that have no live `PaymentAttempt`: it releases and re-takes the stock holds, raises `LinesChangedAsync`, and rebuilds the invoice. After payment has begun, a line change is refused with the protocol's "not editable" message.
  - `IAgentItemIdResolver`, shared with the product feed (G11).
  - Expiry: the agent checkout expires after 6 hours by default (UCP's default; a setting), through the engine's `ExpireAsync` and the existing reconciliation sweep.
  - A single-use browser hand-off token for `continue_url`.
- **AC2 server-confirmed delegated payment.**
  - `PaymentProviderCapabilities.AcceptsDelegatedCredentials`.
  - Stripe: in `BeginAsync`, a delegated SPT creates the PaymentIntent with `payment_method_data[shared_payment_granted_token]` and `confirm=true`. SPTs need the `2026-09-30.preview` Stripe API version and Stripe.net is pinned to its own, so this call goes through `StripeClient.RawRequestAsync` with that version (to verify; otherwise a typed request with extra params). A Google Pay credential becomes a card payment method from its Stripe token.
  - The result maps to succeeded, declined (`payment_failed` / `payment_declined`) or requires action. Requires action becomes `requires_escalation`, with Stripe's `next_action` page as the `continue_url`.
  - SPTs reject `setup_future_usage`, so the saved-card options are refused on this path.
  - The test provider accepts the test credentials, for sandboxes and conformance runs.
  - Credentials are never logged or stored: the attempt keeps only the brand and last four digits that Stripe reports.

### Phase 1: agent platform infrastructure

- **AC3 agent platforms, authentication and idempotency.**
  - The agent platform record and admin list, credentials (OpenId client, hashed API key, UCP profile), the IP allowlist, and test mode.
  - An `AgentPlatform` authentication scheme for the agent route groups that resolves the platform from a bearer token, an API key or a verified signature.
  - The idempotency filter's body fingerprint and per-protocol answers (G7).
  - A rate-limit group `agent-checkout`.
- **AC4 keys and signatures.** Merchant ES256 keys with rotation, the JWK set, RFC 9421 sign and verify, `Content-Digest`, RFC 8941 parsing, RFC 8785 canonicalization, HMAC `t=,v1=`, and the guarded profile fetcher with its cache. Tested against the RFCs' published vectors.
- **AC5 webhook formatters and signers.** The two Commerce Webhooks seams, system-managed agent subscriptions, and the order and shipment event bridge.

### Phase 2: UCP checkout

- **U1 discovery and negotiation.**
  - `/.well-known/ucp`: `version`, `supported_versions` (with a leaf profile at `/.well-known/ucp/2026-04-08`), the REST service and its endpoint, capabilities, payment handlers and `keys[]`. It's public, cached at least 60 seconds with an `ETag`, and never redirected.
  - On every request: parse `UCP-Agent`, fetch and cache the platform's profile, check the version (`version_unsupported` is 422), intersect the capabilities as the spec defines, and echo `ucp.version`, `ucp.capabilities` and `ucp.payment_handlers`.
- **U2 the checkout.**
  - `POST /checkout-sessions` (201), `GET` and `PUT` (full replacement) on `/checkout-sessions/{id}`, and `POST /complete` and `POST /cancel`, under `ucp/v1`.
  - Buyer, context, signals and attribution in; totals, messages, links, `expires_at` and `continue_url` out.
  - Business failures return 200 with `ucp.status: error`, as the spec requires; transport failures return the spec's codes.
  - Complete charges through the handler. A synchronous success returns `completed` with `order { id, label, permalink_url }`; otherwise the response is `complete_in_progress` and the agent polls.
  - A confirmation email goes to the buyer, as UCP requires, through the existing order email.
- **U3 extensions.**
  - `dev.ucp.shopping.fulfillment` (Google requires it for physical goods), in both versions' shapes.
  - `dev.ucp.shopping.discount`: codes through `CheckoutCouponService`, with the applied discounts.
  - `dev.ucp.shopping.buyer_consent`: marketing consent saved on the customer.
- **U4 orders.**
  - `dev.ucp.shopping.order`: `GET /orders/{id}`, limited to the orders the calling platform made.
  - RFC 9421-signed order webhooks to the profile's `webhook_url`: the full order with line item quantities and statuses, fulfillment expectations and events, and adjustments.
- **U5 the conformance suite.** Run the official UCP conformance suite against a test host seeded by an "Agentic commerce sample" recipe (a small store, test handler, test platform), for both versions. Then a sandbox purchase with a Stripe test SPT and with a Google Pay TEST token.

Exit gate: the conformance suite passes for checkout, fulfillment, discount and order on 2026-08-25 and 2026-04-08. A script acting as a platform buys as a guest with a Stripe test SPT and with a Google Pay test token, gets `requires_escalation` for a 3D Secure card and finishes in the browser, and receives signed order webhooks for created, shipped and refunded that verify against `/.well-known/ucp`.

### Phase 3: ACP checkout

- **A1 discovery and versions.** `/.well-known/acp.json`. `API-Version` required and validated, with 400 and `supported_versions` otherwise. Mappers for 2026-04-17 and 2026-01-30, and the legacy `payment_data` shape accepted.
- **A2 the checkout.**
  - `POST /checkout_sessions` (201), `POST /checkout_sessions/{id}` (partial update: an absent field is unchanged, `null` clears it, and `line_items` replaces the list), `GET`, `POST /complete` and `POST /cancel` (405 when already completed or canceled), under `acp/`.
  - Capabilities and payment handlers.
  - `quantity` accepted on `Item` although the 2026-04-17 schema omits it, and unknown fields ignored.
  - Discount codes, and `intent_trace` on cancel, kept write-only with the order.
  - Affiliate attribution, marketing consent, order notes and risk signals stored with the order.
- **A3 order webhooks.** `order_create` / `order_update` (names per platform) with the full order, signed `Merchant-Signature: t=…,v1=…`, through AC5.
- **A4 the sandbox checklist.** OpenAI's production checklist scripted against a test host:
  - sessions with and without an address;
  - a shipping change recomputing the totals;
  - completion returning an order;
  - signed webhooks;
  - the `missing`, `out_of_stock` and `payment_declined` errors;
  - idempotent replay and conflict;
  - working policy links.

Exit gate: the checklist passes with a Stripe test SPT. Where OpenAI's docs and the spec disagree (409 or 422 for a conflict, 200 or 201 for completion, the event names), the spec wins and the difference is a platform setting.

### Phase 4: product feeds for agents

- **F1 richer feed items.**
  - `ProductFeedItem` gains:
    - the available quantity;
    - seller name and URL;
    - the policy links;
    - a shipping price from the cheapest domestic rate;
    - returns (accepted, window days, exchanges) from `ReturnSettings`;
    - weight and dimensions (dimensions are new on `ShippingPart`);
    - a variant's options as a dictionary;
    - a digital flag.
  - `ProductFeedPart` gains per-product **Show to AI agents** and **Let AI agents check out**, with store defaults.
  - Stock changes clear the feed cache through `IStockLevelHandler`.
- **F2 OpenAI's file feed.**
  - A writer for OpenAI's fields (`item_id`, `group_id`, `variant_dict`, `price` as `"79.99 USD"`, `seller_tos`, `seller_privacy_policy`, eligibility flags, …) as gzipped JSON lines and CSV.
  - Its own validator, and a page showing what's left out and why.
  - A daily full snapshot pushed over SFTP with sharding by item id. The SFTP client the MCP SFTP resources use is reused.
  - Parquet only if OpenAI asks for it (P-AC6).
- **F3 the ACP feed API.** A client that creates a feed on an agent platform and upserts changed products after edits and stock changes, with the platform's bearer key.
- Google's UCP uses Merchant Center for discovery, so the existing Merchant Center feed stays, with F1's identifiers matching the UCP item ids.

Exit gate: the OpenAI file passes OpenAI's published field rules for every in-feed product of the sample store, and every feed `item_id` resolves through `IAgentItemIdResolver` to the product and price the checkout charges.

### Phase 5: account linking, catalog and MCP

- **L1 UCP account linking.**
  - The tenant's OpenId server is the authorization server: authorization code with PKCE S256 only, exact redirect URIs, `iss` in the response, revocation, and token checks on every user call.
  - RFC 8414 metadata at `/.well-known/oauth-authorization-server`; OpenID discovery is the fallback the spec allows.
  - The `dev.ucp.shopping.checkout:manage`, `dev.ucp.shopping.order:read` and `dev.ucp.shopping.order:manage` scopes, created by recipe.
  - A linked buyer checks out as their customer record with saved addresses, and reads their orders.
  - `401` and `403` with `WWW-Authenticate` and the `identity_required` / `insufficient_scope` messages.
  - The accelerated identity provider grant (RFC 8693 and RFC 7523) waits for a platform that needs it.
  - Each OpenIddict requirement is checked against the Orchard Core version in use before building.
- **L2 UCP catalog and cart.** `POST /catalog/search` over `IProductSearchService`, `POST /catalog/lookup` and `/catalog/product` over `CatalogApiProductReader` (priced for the caller and the context's country and currency through markets), and the UCP cart over `ICartService`, convertible to a checkout.
- **L3 UCP over MCP.** UCP's MCP binding (`create_checkout`, `get_checkout`, … with `meta.ucp-agent` and the idempotency key) at `ucp/mcp` on the MCP C# SDK, calling the same adapter. It's separate from the AI MCP server, so the store doesn't need the AI features. ACP's MCP binding follows the same pattern when a platform needs it.

### Phase 6: AP2 (preview)

- **P1 the AP2 mandates extension.**
  - Declared as `dev.ucp.common.payment.ap2_mandate` with `vp_formats_supported: { "dc+sd-jwt": {} }`, and active only when negotiated. A negotiated session is locked to mandates.
  - Every checkout response carries `ap2.merchant_authorization`: a detached ES256 JWS over the RFC 8785 canonical checkout without `ap2`, with the merchant key's `kid`.
  - On complete, an `ICheckoutPaymentGate` verifies `ap2.checkout_mandate`:
    1. Verify the SD-JWT and key-binding chain, its expiry, audience and nonce, and that `vct` is `mandate.checkout.1` (or `.open.1` with its constraints).
    2. Re-verify the store's own `merchant_authorization` inside it.
    3. Confirm that the id, line items and totals match the session now.
    4. Allow a closed mandate from an open one only once.
  - Failures use the spec's codes: `mandate_required`, `mandate_invalid_signature`, `mandate_expired`, `mandate_scope_mismatch`, `merchant_authorization_invalid`, `agent_missing_key`.
  - The trust list: agent providers' keys from their profiles, and credential issuer roots an admin adds.
  - The payment mandate travels to the payment provider inside the credential token. A provider that can't take it is not offered while AP2 is negotiated.
  - Evidence: the mandate, its disclosures and a signed checkout receipt are kept with the order for disputes, and shown on the order's payment tab.
- No .NET SD-JWT library handles AP2's delegated chains, so the verifier is ours, tested with the vectors from the AP2 SDK's tests. Where the specs leave a mapping open (UCP's detached JWS against AP2's `checkout_jwt`, nonce issuance), the plan follows the reference's recommendation and records it in the module docs.

Exit gate: the AP2 Python SDK's sample shopping agent completes a human-present purchase against the test host, and tampered, expired, replayed and mismatched mandates are each refused with the right code.

### Phase 7: the store's own agents

- **T1 commerce AI tools.**
  - Selectable `AddCoreAITool` tools: search products, product details and availability, add to cart, view cart, start checkout. Start checkout returns the checkout link; the tools never pay.
  - The site's chat agents can use them, the MCP server can expose them, A2A agent profiles can carry them, and WebMCP can put them on the page.
  - WebMCP gains an opt-in for anonymous storefront tools, limited to these read-and-cart tools.

## Testing

- **Unit tests per slice:**
  - status, totals, messages and fulfillment mapping for every protocol version;
  - totals that add up;
  - idempotency fingerprints;
  - the RFC vectors for 9421, 9530, 8941 and 8785;
  - JWS and SD-JWT verification with the AP2 SDK's vectors;
  - feed writers and validators;
  - item id round trips.
- **Integration tests** in the commerce integration suite: a real host with the agentic sample, platforms driving each protocol over HTTP with the test handler, signed webhooks received by a local listener and verified, and a platform refused for another platform's checkout.
- **External suites:** the UCP conformance suite (phase 2) and the scripted ACP checklist (phase 3), run before each phase's PR is marked ready. Stripe test mode with test SPTs from Stripe's test helpers.
- **Browser check:** the hand-off from `continue_url` and the admin pages at every Bootstrap breakpoint.

## Delivery

One branch and one PR per phase, a commit per slice, pushed when a slice is verified. Phases 0 and 1 can share a PR because nothing is visible until UCP lands. Docs ship with each phase:

- a module page, `src/CrestApps.Docs/docs/modules/agentic-commerce.md`;
- a User Manual page, "Sell through AI agents", under commerce;
- the changelog;
- a store setup guide step.

## Risks

| Risk | What we do |
| --- | --- |
| The specs move quickly: UCP releases a dated version every few months, ACP has known schema defects, and AP2 may change under FIDO | Versioned mappers; a schema check in CI against the pinned schema files; lenient reading of unknown fields; AP2 marked preview |
| Google lags the latest UCP version | Serve 2026-04-08 and 2026-08-25 side by side |
| The SPT needs a preview Stripe API version | Isolate the call behind `RawRequestAsync`, and test with Stripe's test helpers |
| A platform's profile fetch could be used for SSRF | The webhook network guard, no redirects, size and time limits, and a cache |
| Agents bring PII (signals, attribution, addresses) | Keep only what an order needs, keep signals with the attempt for the risk check and drop them after settlement, and never echo credentials |
| An agent could drain stock or holds by creating checkouts | Rate limits per platform; holds expire with the checkout (6 hours, configurable); a cap on open checkouts per platform |

## Decisions needed

Until they are answered, the work goes ahead on the default in the last column.

| # | Question | Options | Default taken |
| --- | --- | --- | --- |
| P-AC1 | One Agentic Commerce module with a feature per protocol, or a module per protocol? | One module / separate modules | One module: the protocols share the agent checkout, platforms, keys and webhooks |
| P-AC2 | Build order | UCP first / ACP first / both together | UCP, then ACP, then feeds, then account linking, catalog and MCP, then AP2. Google is the live checkout consumer; OpenAI is feed-first right now |
| P-AC3 | Which payment handlers first? | Stripe SPT and Google Pay through Stripe / add Adyen or PayPal | Stripe SPT and Google Pay through Stripe, plus the test handler |
| P-AC4 | Accept UCP platforms the store hasn't registered, on a verified RFC 9421 signature alone? | Registered platforms only / open onboarding as a setting | Registered only by default, with open onboarding as a setting |
| P-AC5 | Move OpenAI's feed fields forward to phase 2, so a store can apply to OpenAI's merchant program early? | Phase 4 / with phase 2 | Phase 4 |
| P-AC6 | OpenAI prefers Parquet feeds. Add a Parquet dependency? | JSON lines and CSV only / add Parquet.Net | JSON lines and CSV, which OpenAI accepts; Parquet if OpenAI asks |
| P-AC7 | Is AP2 in scope now, while it's v0.2 and moving to FIDO? | Preview in phase 6 / wait for 1.0 | Preview in phase 6 |
| P-AC8 | Should the store's own agents (phase 7) be part of this plan or separate? | In / separate | In, last |

## As built

Built on 2026-11-09 and finished on 2026-11-10 in one branch and one PR, with a commit per group of slices, not a PR per phase: the phases share the agent checkout, so they are reviewed together.

### Where the code is

| Piece | Project |
| --- | --- |
| Protocol-neutral models, `IAgentPaymentHandler` | `CrestApps.OrchardCore.AgenticCommerce.Abstractions` |
| Agent checkout service, platforms, checkout store, signing keys, RFC 9421/9530/8941/8785 helpers, AP2 SD-JWT and JWS | `CrestApps.OrchardCore.AgenticCommerce.Core` |
| Features, endpoints, admin, payment handlers, order publishers, feeds, AI tools, recipes | `CrestApps.OrchardCore.AgenticCommerce` (features `Ucp`, `Ucp.IdentityLinking`, `Ucp.Catalog`, `Ucp.Mcp`, `Acp`, `Feeds`, `Ap2`) |
| Headless checkout steps | `ICheckoutContactStep` and `ICheckoutFulfillmentStep` in Checkout.Abstractions; Customers and Shipping implement them |
| Delegated credentials | `CheckoutPaymentDataKeys.DelegatedCredential`, `PaymentProviderCapabilities.AcceptsDelegatedCredentials`; Stripe charges shared payment tokens and Google Pay tokens, the test provider takes `tok_test_*` |
| Direct webhook deliveries | `IWebhookOutbox` and `IWebhookDeliveryFormatter` in Webhooks.Core: one signed delivery to a URL that isn't a subscription, with the outbox's retries |
| Protocol idempotency rules | `IdempotencyPolicy` in Commerce.Api.Core, set per endpoint group |
| Store website and policy links | `StoreProfileSettings.Website` and `Policies` |

### Changes from the plan

- **Checkout lines.** When an agent changes the lines, the service reprices a transient cart and starts a new order and checkout session. The engine cancels the earlier unpaid order for the same cart, so only one order is open per agent checkout.
- **Hand-off.** `continue_url` is a signed, short-lived link to `agentic-commerce/continue/{token}`. It gives the store checkout session to the browser that opens it: to the signed-in user, or to a guest with the guest token, never to the agent platform. A buyer back from their bank's approval is sent to the confirmation, because the hand-off completes a payment that is still being confirmed.
- **Agent catalog without the feed rules.** The agent catalog and UCP catalog read products without Merchant Center's validation (`IProductFeedService.BuildAsync(validate: false)`), so a store without a base URL or GTINs can still sell through agents. The feeds keep the validation.
- **Fulfillment.** When the agent gives an address and no option, the cheapest option is selected, so totals are complete on the first answer.
- **Limits.** The checkout lifetime (default 6 hours, 1 to 72) and the cap on open checkouts per platform (default 1,000) are site settings. There is no per-platform request rate limit beyond the cap.
- **RFC 8414.** OpenIddict answers `/.well-known/oauth-authorization-server` itself, so UCP Account Linking adds the UCP scopes to that document and makes the OpenID server accept S256 PKCE only. The module's own route answers 404 when there is no OpenID server.
- **AI tools.** Five tools: search products, get a product, add to cart, view cart, and start a checkout (returns the hand-off link, never pays). WebMCP serves the tools a module marks with `MarkAIToolAnonymousSafe` to anonymous visitors when **Offer storefront tools to visitors** is on.
- **ACP request signatures.** Optional per platform: ES256 (no Ed25519 in .NET 10) over the RFC 8785 canonical body, with the `Timestamp` checked against a clock skew. The specs leave the signed string open.
- **Delegated 3-D Secure.** ACP only. UCP's authentication extension carries no result in 2026-08-25; it needs a challenge page that speaks the embedded protocol, which isn't built.
- **UCP over A2A.** UCP defines A2A for checkout only, so catalog and cart aren't offered there. Order get and the error mapping are this module's choices.

### Double charges

Found and fixed while testing against Stripe test mode, each with a unit or integration test:

- The agent checkout was saved after its lock was released, and after the checkout engine's own commit, so a request inserted a second row with the same id. A paid checkout could then read as payable again. The changes now go onto the stored row, committed inside the lock.
- A Stripe failure whose outcome is unknown (a server error, a rate limit, an idempotency conflict) was recorded as a decline, so the next try used a new idempotency key and could charge again. Only Stripe's final answers (a card error, a refused request, a PaymentIntent that ended unpaid) end an attempt now. The same fix applies to saved-card charges.
- The engine now cancels a payment begun with another provider, at that provider, before beginning one for the same obligation, and begins nothing when the first was paid or can't be canceled.

### Verified

- **Stripe test mode**, with the account's test keys, shared payment tokens from Stripe's test helper and webhooks forwarded by the Stripe CLI. Each run was checked against Stripe's own list of PaymentIntents:
  - ACP and UCP purchases with a shared payment token; a second complete is refused.
  - 8 parallel completes with different keys and tokens: one charge. 8 parallel completes with the same key: one charge, the rest told it's in flight or replayed.
  - A decline then a good card: one charge. A token whose limit is below the total: refused, nothing charged.
  - Google Pay (a Stripe card token in Google Pay's gateway format): a charge, a decline then a charge, and 3-D Secure escalation approved in a browser, landing on the store's confirmation with one charge.
  - Delegated 3-D Secure over ACP: `authentication_required` with the acquirer details, then the result imported onto the same PaymentIntent (one charge); a `denied` result cancels the PaymentIntent.
  - ACP over MCP paid with a shared payment token, replay with the same key, refusal with a new key.
  - Stripe webhooks arriving after the agent's completion don't record the payment twice.
- **The UCP conformance suite** (`Universal-Commerce-Protocol/conformance`), run locally against the test host with two local changes to the suite: an `Authorization` header with a test platform's key, and a default buyer (email and name) on its checkouts, because the store needs a contact email and a recipient name to place an order. Results are in the PR.
- **AP2 with the official AP2 Python SDK** (`google-agentic-commerce/AP2`): an open checkout mandate signed with a key from the platform's profile, then a closed one signed by the agent's key over the store's merchant authorization, completes the checkout. A tampered mandate is refused (`mandate_invalid`), and so is a mandate for a checkout changed after the buyer approved it (`mandate_scope_mismatch`).
- **The commerce integration suite** has agentic tests: a purchase, parallel completes with different keys and with the same key, a decline then a success, and one platform refused another's checkout. Each checks there is exactly one succeeded payment attempt and that it equals the order total.
- UCP over A2A, the embedded checkout (in a frame from another origin), WebMCP storefront tools and RFC 8414 metadata were each run on a live host.
- Unit tests for every piece, the RFC vectors, and the UCP and ACP mappers.

### Not done

- **The AP2 sample shopping agent** runs on Google's ADK with Gemini and needs a Google API key, so it wasn't run; its SDK was (see Verified).
- **Live Stripe** was run in test mode only. The shared payment token call uses the preview API version `2026-09-30.preview`.
- **UCP conformance differences** the store keeps on purpose: it refuses the suite's `mock_payment_handler` and raw card numbers (a business must refuse handlers it didn't advertise), retries failed webhooks on a backoff of seconds to minutes rather than within the suite's 5 seconds, and has no `/testing/simulate-shipping` endpoint.
- **Embedded checkout** accepts no delegations. **ACP over MCP** describes `payload` as a plain object in `tools/list`.

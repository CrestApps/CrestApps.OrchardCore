# Agentic Commerce Protocols — Merchant-Side Implementation Reference (UCP, ACP, AP2)

**Compiled:** 2026-11-02, for a .NET / Orchard Core merchant (seller) implementation.
**Method:** the three official GitHub repositories were cloned and read directly (spec Markdown, JSON Schemas, OpenAPI and OpenRPC). This was cross-checked against the live spec sites and vendor docs (Google Merchant UCP docs, Stripe docs, OpenAI developer docs). Anything not confirmed from a primary source is marked **unverified**.

| Protocol | Version read | Date | Primary source |
|---|---|---|---|
| UCP | **2026-08-25** (latest stable; `release/2026-08-25` @ `cfba1e7`). Older: 2026-04-08, 2026-01-23, 2026-01-11. `main` = draft | released 2026-08-25, branch read 2026-11-02 | https://ucp.dev/2026-08-25/ · https://github.com/Universal-Commerce-Protocol/ucp |
| ACP | **2026-04-17** (latest stable; `main` @ `7fdd78d`). Older: 2026-01-30, 2026-01-16, 2025-12-12, 2025-09-29. Plus `unreleased/` | last repo commit 2026-07-17 | https://agenticcommerce.dev · https://github.com/agentic-commerce-protocol/agentic-commerce-protocol |
| AP2 | **v0.2.0** (`main` @ `e1ea56d`). Older: v0.1.0 (2025-09-16) | released 2026-04-28 | https://ap2-protocol.org · https://github.com/google-agentic-commerce/AP2 |

## 0. Executive summary (merchant view)

| | UCP | ACP | AP2 |
|---|---|---|---|
| Owner / governance | Google + Shopify (permanent), Stripe (Governing Council since 2026-04). Tech Council includes Amazon, Meta, Microsoft, Salesforce. Apache-2.0 | OpenAI + Stripe (maintainers). Meta joining the TSC (unreleased). Apache-2.0, "beta" | Google, donated to the **FIDO Alliance** with v0.2 (2026-04-28) |
| What it is | Full commerce protocol: discovery, negotiation, catalog, cart, checkout, order, identity linking, payment-handler framework | Checkout API + delegated payment + order webhooks + product feed (+ cart, MCP, discovery since 2026-04-17) | **Payment-authorization layer only**: verifiable mandates (SD-JWT). Rides on a commerce protocol, recommended UCP |
| Merchant hosts | `/.well-known/ucp` + REST (and/or MCP/A2A/embedded) endpoints `/checkout-sessions…`. Sends signed order webhooks | `/checkout_sessions…` REST (+ optional `/.well-known/acp.json`, MCP, `/carts`). Sends HMAC-signed webhooks. **Pushes** product feed | Signs the checkout (ES256 JWS), verifies the user's Checkout Mandate, returns a signed receipt |
| Discovery / versioning | Profile at `/.well-known/ucp`. Date versions. Per-request `UCP-Agent: profile="…"`. Capability **intersection** | `API-Version: YYYY-MM-DD` header (required). Optional `/.well-known/acp.json` | n/a (version suffixes on mandate `vct`, e.g. `mandate.checkout.1`) |
| Auth | Business choice: OAuth bearer (Google uses this), API key, mTLS, or **RFC 9421 HTTP Message Signatures** (ES256 baseline, keys in profile `keys[]`) | `Authorization: Bearer` (required). Request `Signature`/`Timestamp` headers exist but the algorithm is **unspecified** | SD-JWT + KB-JWT chains. Merchant key ES256 (ECDSA) |
| Payment credential arrives as | `payment.instruments[].credential` per **payment handler** (`com.google.pay` token, `com.stripe.payments` SPT `spt_…`, `dev.shopify.shop_pay`, network token…) | `payment_data` per **payment handler** (since 2026-01-30). Typically a Stripe **Shared Payment Token** (`spt_…`) or a PSP vault token from Delegate Payment | Payment Mandate inside the PSP/CP token. Merchant passes it to its processor |
| Charging with Stripe | `POST /v1/payment_intents` with `payment_method_data[shared_payment_granted_token]=spt_…&confirm=true` (same for ACP and UCP) | same | n/a |
| Order updates | `POST` full Order to platform `webhook_url`, RFC 9421 signed (MUST) | `order_create` / `order_update` webhooks, `Merchant-Signature: t=…,v1=HMAC-SHA256` | n/a |
| Who consumes it today | **Google AI Mode / Gemini** (native checkout targets UCP **2026-04-08**). Stripe "use your own checkout" now documents UCP. Shopify (UCP co-author) | OpenAI ChatGPT. Instant Checkout was **reportedly scaled back in 2026-03** ("moving to Apps", unverified press). OpenAI onboarding now centres on the **product feed** (approved partners) | Via UCP's `dev.ucp.common.payment.ap2_mandate` extension. Production adoption unverified |
| Official .NET SDK | **None.** JSON Schema 2020-12 + OpenAPI/OpenRPC for codegen. Rust `ucp-schema` resolver. Conformance test suite | **None.** JSON Schema + OpenAPI + OpenRPC. No conformance suite | **None.** JSON Schemas in `code/sdk/schemas`. Python SDK only |

**Recommended build order for an Orchard Core merchant:**
1. A product feed: ACP/OpenAI file feed and Google Merchant Center feed.
2. The UCP core: profile, checkout REST (version 2026-04-08 for Google, plus 2026-08-25), the fulfillment extension, the `com.google.pay` and/or `com.stripe.payments` handler, and signed order webhooks.
3. An ACP checkout adapter over the same internal checkout-session engine, since the two protocols map closely (see section 4).
4. Identity linking, using Orchard Core's OpenId module as the OAuth AS.
5. The AP2 mandates extension, last.

---

---

# 1. UCP — Universal Commerce Protocol

**Version read:** latest stable release **`2026-08-25`**. Read from the `release/2026-08-25` branch of `github.com/Universal-Commerce-Protocol/ucp` (head `cfba1e7`, 2026-11-02; the branch receives backward-compatible backports in place). The `main` branch (`58c7ab9`, 2026-10-31) is the unreleased `draft`. Earlier releases: `2026-01-11`, `2026-01-23`, `2026-04-08` (tags `v…`, long-lived `release/…` branches).

**Governance (from `docs/documentation/announcements.md`):** Google and Shopify are permanent Governing Council members, and Stripe joined on 2026-04-28. A Tech Council of 16 seats includes Amazon, Meta, Microsoft, Stripe and Salesforce. A Payments TC was formed with Adyen, Ant International, Coinbase, Global Payments, Google, PayPal, Shopify and Stripe. Food and Lodging TCs also exist. Licence: Apache-2.0.

**Primary sources:**
- Spec: https://ucp.dev/2026-08-25/specification/overview/ (source `docs/specification/**`)
- Schemas: `source/schemas/**` (published at `https://ucp.dev/2026-08-25/schemas/...`)
- REST OpenAPI: `source/services/shopping/rest.openapi.json`
- MCP and Embedded OpenRPC: `source/services/shopping/mcp.openrpc.json` and `embedded.openrpc.json`
- Release notes: https://github.com/Universal-Commerce-Protocol/ucp/releases/tag/v2026-08-25

> **Important version caveat (Google):** Google's *Native checkout* guide (https://developers.google.com/merchant/ucp/guides/checkout/native, fetched 2026-11-02) still targets **UCP `2026-04-08`**. Its Google Pay handler config references `2026-01-23`. A merchant that wants Google AI Mode/Gemini traffic today should therefore serve `2026-04-08`, either as its current version or through `supported_versions`. This document describes `2026-08-25`; the release notes list the breaking deltas from `2026-04-08` (see 1.14).

---

## 1.1 Core model

- **Roles:** *Platform* (the agent or app acting for the buyer), *Business* (merchant of record) and *Payment Credential Provider* (PSP or wallet: Google Pay, Shop Pay, Stripe…).
- **Service** = an API surface for a vertical, keyed by reverse-DNS (`dev.ucp.shopping`, `dev.ucp.common`). Each service entry pairs it with **one transport binding**: `rest` (OpenAPI 3.x JSON), `mcp` (OpenRPC), `a2a` (Agent Card) or `embedded` (OpenRPC).
- **Capability** = a feature in a service (`dev.ucp.shopping.checkout`). **Extension** = an optional capability that augments a parent through `extends` (a string, or an array for multi-parent). Extension schemas compose onto the parent through JSON Schema `allOf`.
- **Naming:** `{reverse-domain}.{service}.{capability}`. `dev.ucp.*` is reserved for the UCP body. Vendors use `com.{vendor}.*` and `org.{org}.*`.
- **Authority binding (MUST):** a capability, service or handler `schema` URL host, with its labels reversed, must equal the entity name or be a label-aligned prefix of it. For example, `dev.ucp.shopping.checkout` must be served from `ucp.dev` or `shopping.ucp.dev`. Platforms MUST reject entities that fail this check and MUST NOT follow redirects when fetching schemas.
- **Amounts** are integer **minor units**. Dates are RFC 3339. Totals are *signed* integers.
- **Quantities** are integer step counts. They can carry an optional `quantity_unit` (UN/CEFACT Rec20 code + `scale` + `display_text` + `increment`) for goods sold by measure, new in 2026-08-25. The default is `C62` ("each") at scale 0.

## 1.2 All capabilities and extensions in 2026-08-25

| Name | Kind / extends | Purpose |
|---|---|---|
| `dev.ucp.shopping.checkout` | root capability | Checkout sessions (create, get, update, complete, cancel). |
| `dev.ucp.shopping.order` | root | Order snapshot: `GET /orders/{id}` plus **order webhooks** to the platform. |
| `dev.ucp.shopping.cart` | root | Pre-purchase basket (`/carts`) with estimated pricing; converts to checkout via `cart_id`. |
| `dev.ucp.shopping.catalog.search` | root | `POST /catalog/search`. |
| `dev.ucp.shopping.catalog.lookup` | root | `POST /catalog/lookup` (batch) and `POST /catalog/product` (single detail). |
| `dev.ucp.shopping.permalink` | root | Browser-addressable shopping intent (cart/checkout permalinks, redirect resolution). |
| `dev.ucp.shopping.fulfillment` | ext → checkout (and catalog) | Hierarchical fulfillment: methods → destinations → groups → options. |
| `dev.ucp.shopping.discount` | ext → checkout, cart | `discounts.codes[]` in, `discounts.applied[]` out. |
| `dev.ucp.shopping.buyer_consent` | ext → checkout, cart | Per-purpose consent map on `buyer` (reverse-DNS purposes, `granted`, `source`). |
| `dev.ucp.common.identity_linking` | root (common) | OAuth 2.0 account linking, scopes and accelerated IdP chaining. |
| `dev.ucp.common.loyalty` | ext (catalog, cart, checkout) | Loyalty memberships and benefits. |
| `dev.ucp.common.ask` | root (common) | Natural-language Q&A about the business; read-only. Backported to the 2026-08-25 branch on 2026-11-02. |
| `dev.ucp.common.location.search` / `.lookup` | root (common) | Store/pickup location search and detail (grocery). |
| `dev.ucp.common.payment.ap2_mandate` | ext → checkout | AP2 mandates: merchant signature on checkout plus user-signed checkout mandate (see section 3.8.3). |
| `dev.ucp.common.payment.authentication` | ext | 3DS2 device-data-collection and challenge **Actions**. |
| `dev.ucp.common.payment.split_payments` | ext | More than one payment instrument per checkout. |
| `dev.ucp.common.payment.terms` | ext | Payment terms and schedules (deposits, deferred, installments). |

Draft only, on `main` and not in 2026-08-25: `dev.ucp.lodging.booking`, `dev.ucp.lodging.policy.cancellation` and `dev.ucp.shopping.policy.return`. Note that `policies[]` with type `dev.ucp.shopping.policy.return` already exists as a *value* in 2026-08-25.

The 2026-08-25 overview's "Standard Capabilities" table lists only Ask, Cart, Checkout, Identity Linking and Order. The others are defined in their own spec pages and schemas.

## 1.3 Discovery: `/.well-known/ucp` (business profile)

The business publishes `GET https://{merchant-domain}/.well-known/ucp`. The platform sends its own profile URL in the `UCP-Agent` header on **every** request.

**Hosting rules (MUST):**
- HTTPS only, and no 3xx redirects.
- `Cache-Control: public, max-age>=60`. Never `private`, `no-store` or `no-cache`.
- SHOULD send `ETag` or `Last-Modified`.

**Profile shape** (`source/schemas/profile.json`):

```json
{
  "ucp": {
    "version": "2026-08-25",                       // REQUIRED, dated YYYY-MM-DD; never "draft"
    "supported_versions": {                        // OPTIONAL (business only): older version -> leaf profile URL
      "2026-04-08": "https://shop.example.com/.well-known/ucp/2026-04-08"
    },
    "services": {                                  // REQUIRED (may be {})
      "dev.ucp.shopping": [
        { "version": "2026-08-25", "transport": "rest",
          "spec": "https://ucp.dev/2026-08-25/specification/overview/",
          "schema": "https://ucp.dev/2026-08-25/services/shopping/rest.openapi.json",
          "endpoint": "https://shop.example.com/ucp/v1" },
        { "version": "2026-08-25", "transport": "mcp",
          "schema": "https://ucp.dev/2026-08-25/services/shopping/mcp.openrpc.json",
          "endpoint": "https://shop.example.com/ucp/mcp" },
        { "version": "2026-08-25", "transport": "a2a",
          "endpoint": "https://shop.example.com/.well-known/agent-card.json" },
        { "version": "2026-08-25", "transport": "embedded",
          "schema": "https://ucp.dev/2026-08-25/services/shopping/embedded.openrpc.json" }
      ]
    },
    "capabilities": {                              // OPTIONAL map: name -> array of versioned entries
      "dev.ucp.shopping.checkout": [
        { "version": "2026-08-25",
          "spec": "https://ucp.dev/2026-08-25/specification/shopping/checkout",
          "schema": "https://ucp.dev/2026-08-25/schemas/shopping/checkout.json" } ],
      "dev.ucp.shopping.fulfillment": [
        { "version": "2026-08-25", "extends": "dev.ucp.shopping.checkout",
          "spec": "https://ucp.dev/2026-08-25/specification/shopping/extensions/fulfillment",
          "schema": "https://ucp.dev/2026-08-25/schemas/shopping/fulfillment.json" } ],
      "dev.ucp.shopping.discount": [ { "version": "2026-08-25", "extends": ["dev.ucp.shopping.checkout","dev.ucp.shopping.cart"], "...": "..." } ],
      "dev.ucp.shopping.order": [ { "version": "2026-08-25", "...": "..." } ],
      "dev.ucp.common.identity_linking": [
        { "version": "2026-08-25", "...": "...",
          "config": {
            "providers": { "com.example.idp": [ { "type": "oauth2", "auth_url": "https://accounts.example.com/" } ] },
            "scopes": { "dev.ucp.shopping.order:read": {}, "dev.ucp.shopping.order:manage": {} }
          } } ]
    },
    "payment_handlers": {                          // REQUIRED (may be {}): handler name -> array of instances
      "com.google.pay": [ { "id": "gpay_1", "version": "2026-01-23", "spec": "...", "schema": "...", "config": { } } ],
      "com.stripe.payments": [ { "id": "stripe_payments", "version": "2026-06-25", "available_instruments": [ ], "config": { } } ]
    },
    "map_order": { "payment_handlers": ["com.stripe.payments", "com.google.pay"] }   // OPTIONAL presentation order
  },
  "keys": [                                        // OPTIONAL top-level JWK Set (RFC 7517): signing keys
    { "kid": "business_2026", "kty": "EC", "crv": "P-256", "x": "...", "y": "...", "use": "sig", "alg": "ES256" }
  ]
}
```

- **Capability entry fields** (`capability.json`): `version` (required), `spec`, `schema` (required for capabilities), `extends`, `config` and `id`. The platform-profile, business-profile and response variants differ: in responses, entries carry just `{version}`.
- **Payment handler entry** (`payment_handler.json`, with variants `business_schema`, `platform_schema` and `response_schema`): `id` (instance id, referenced by `handler_id` on instruments), `version`, `spec`, `schema`, `config` (handler-specific) and `available_instruments[]` (`{type, constraints, ucp.request_constraints}`).
- **`keys[]`:** a JWK Set. EC (P-256/P-384) or OKP (Ed25519). The **universal baseline is ES256**. A single ES256 key is enough unless you also need Web Bot Auth (Ed25519 is common there) or AP2 (ES256/384/512).
- **The `ucp` member** is reserved at every structured scope. Consumers MUST ignore unknown members inside `ucp`, and extensions MUST NOT put data there.

## 1.4 Versioning and capability negotiation

1. The platform fetches `/.well-known/ucp`, picks a mutually supported **protocol version** (preferring the newest) from `version` and the `supported_versions` keys, and fetches the leaf profile if needed. A leaf profile's `ucp.version` MUST equal its key, and leaf profiles MUST NOT themselves contain `supported_versions`.
2. On **every** request the business:
   - MUST fetch and validate the platform profile named in `UCP-Agent` (caching allowed).
   - MUST validate the platform's protocol version: it must match `version` or be a key of `supported_versions`. Otherwise it returns `version_unsupported` (REST 422).
   - MUST compute the **capability intersection**:
     - (a) keep business capabilities whose name the platform also lists;
     - (b) keep only versions present in **both** arrays and select the latest; a capability with no common version is dropped;
     - (c) drop extensions with no parent in the intersection;
     - (d) repeat (c) until stable.
3. Every `dev.ucp.*` entry in a profile for release `D` MUST declare version `D`. Platforms reject mismatches. Third-party (`com.*`/`org.*`) extensions version independently and declare compatibility through `requires`.
4. Every response MUST include `ucp.version` (the negotiated version) and `ucp.capabilities`, filtered to those relevant to the operation: checkout responses carry checkout plus its extensions, cart responses carry cart plus its extensions, and order webhooks carry order. Checkout responses also carry `ucp.payment_handlers` (the response variant, filtered to the cart context; this is a MUST).

**Negotiation and protocol error codes (REST / MCP):**

| Code | REST | MCP |
|---|---|---|
| `invalid_profile_url` | 400 | -32001 |
| `profile_unreachable` | 424 | -32001 |
| `profile_malformed` | 422 | -32001 |
| `version_unsupported` | 422 | -32001 |
| `capabilities_incompatible` | **200** with `ucp.status:"error"` + `messages[]` | result |
| `signature_missing` / `signature_invalid` / `key_not_found` | 401 | -32000 |
| `digest_mismatch` / `algorithm_unsupported` | 400 | -32600 |
| Auth required / forbidden / idempotency conflict / rate limit / server error / unavailable | 401 / 403 / **409** / 429 (`Retry-After`) / 500 / 503 | -32000 / -32603 |

Transport-level errors carry a body of `{code, content, continue_url?}`. **Business outcomes, including failure to create a resource, return HTTP 200** with `{"ucp":{"version":"…","status":"error"},"messages":[…],"continue_url":"…"}` (`common/types/error_response.json`).

## 1.5 REST binding — checkout endpoints

The base URL is `services["dev.ucp.shopping"][transport=rest].endpoint`, with no trailing slash. Requests and responses use `application/json`. TLS **1.3 minimum** is required for REST endpoints (stated in `checkout/rest.md`).

| Operation | Method | Path | Success |
|---|---|---|---|
| Create Checkout | `POST` | `/checkout-sessions` | **201** |
| Get Checkout | `GET` | `/checkout-sessions/{id}` | 200 |
| Update Checkout (**full replacement**) | `PUT` | `/checkout-sessions/{id}` | 200 |
| Complete Checkout | `POST` | `/checkout-sessions/{id}/complete` | 200 |
| Cancel Checkout | `POST` | `/checkout-sessions/{id}/cancel` | 200 |
| (Cart) create / get / update / cancel | `POST /carts`, `GET /carts/{id}`, `PUT /carts/{id}`, `POST /carts/{id}/cancel` | | 201/200 |
| (Catalog) search / lookup / product | `POST /catalog/search`, `POST /catalog/lookup`, `POST /catalog/product` | | 200 |
| (Order) get | `GET /orders/{id}` | | 200 |
| Order event webhook (business → platform) | `POST` | platform `config.webhook_url` | platform returns 2xx |

Update is a **full PUT**: the platform resends the whole writable resource. Cancel SHOULD succeed for any status other than `completed` and `canceled`; otherwise the business returns an error message.

### 1.5.1 Request headers (from `rest.openapi.json` `components/parameters`)

| Header | OpenAPI `required` | Notes |
|---|---|---|
| `UCP-Agent` | **true** | RFC 8941 Dictionary: `profile="https://platform.example/.well-known/ucp"`. MUST be on all requests and webhooks. |
| `Request-Id` | **true** (uuid) | Tracing. |
| `Idempotency-Key` | **true** on POST/PUT (uuid) | Prose says state-changing ops "SHOULD support idempotency". The OpenAPI marks it required. Store results for **>= 24h (48h recommended)**. Same key + same body: return the cached response. Same key + different body: **409**. Storage failure: fail closed with 503. Body match uses SHA-256 of the raw bytes. |
| `Authorization` | false | `Bearer` OAuth token: platform `client_credentials`, or user token via identity linking. |
| `X-API-Key` | false | Pre-shared platform key. |
| `Signature`, `Signature-Input` | false | RFC 9421 (see 1.9). |
| `Content-Digest` | false (required when signing a body) | RFC 9530 `sha-256=:<b64>:`. |
| `User-Agent`, `Content-Type`, `Accept`, `Accept-Language`, `Accept-Encoding` | false | Standard. |
| `Webhook-Id` (uuid), `Webhook-Timestamp` (unix s) | true on **webhooks** | Standard Webhooks style. |

Authentication is the business's choice (open, API key, OAuth 2.0, mTLS or RFC 9421 signatures). If API key, OAuth or mTLS is used, the business MUST check that the authenticated principal is allowed to act for the profile in `UCP-Agent`.

### 1.5.2 Checkout object (`schemas/shopping/checkout.json`)

Response-required fields: `ucp`, `id`, `line_items`, `status`, `currency`, `totals`, `links`. The `ucp_request` column shows the per-operation request annotation: required / optional / omit.

| Field | Type | Request (create / update / complete) | Notes |
|---|---|---|---|
| `ucp` | `{version, status?, capabilities, payment_handlers}` | omit | `status` is `success` or `error`. |
| `id` | string | omit (taken from the path) | |
| `line_items[]` | LineItem | required / required / omit | |
| `buyer` | `{first_name,last_name,email,phone_number(E.164)}` | optional / optional / omit | |
| `context` | `{address_country, address_region, postal_code, location, intent, language, currency, eligibility[], payment[{handler,types[]}]}` | optional / optional / omit | Provisional hints, not authoritative. |
| `signals` | map of reverse-DNS key to value, e.g. `dev.ucp.buyer_ip`, `dev.ucp.user_agent` | optional (all ops) | Must be observed, not buyer-asserted. The business can request one through a message with `code:"signal"`. |
| `attribution` | map (gclid, campaign_* …) | optional | Informational. |
| `status` | enum (see 1.6) | omit | |
| `currency` | ISO 4217 | omit | Set by the business. |
| `totals[]` | Total | omit | Exactly one `subtotal` and one `total`. |
| `actions` | map of Action type to instances | omit | Extension-defined outstanding work (e.g. 3DS). |
| `messages[]` | Message | omit | |
| `links[]` | `{type, url, title?, id?}` | omit | Response-required. Well-known types: `privacy_policy`, `terms_of_service`, `refund_policy`, `shipping_policy`, `faq`. Google requires privacy and ToS. |
| `policies[]` | `{type (reverse-DNS), description{plain,html,markdown}, applies_to[] (JSONPath), url?}` | omit | |
| `expires_at` | RFC 3339 | omit | **Default TTL 6h** from creation. |
| `continue_url` | https URL | omit | MUST be present when `requires_escalation`, SHOULD be present on other non-terminal states, and omitted on terminal states. |
| `payment` | `{instruments[]}` | optional / optional / **required** | |
| `order` | `{id, label?, permalink_url}` | omit | Present iff `completed`. |
| `fulfillment` (fulfillment ext) | see 1.5.4 | optional / optional / omit | |
| `discounts` (discount ext) | `{codes[], applied[]}` | `codes` optional | |
| `ap2` (AP2 ext) | `{merchant_authorization}` in responses, `{checkout_mandate}` on complete | | See section 3.8.3. |

**LineItem:** `id` (omit on create, optional on update), `item {id*, title (resp), price (resp, minor units per unit), quantity_unit?, unit_price?, image_url?}`, `quantity*` (integer steps), `totals[]` (resp) and `parent_id?`. The platform sends only `item.id` and `quantity`; the business fills in title, price and totals. Google requires `item.id` to equal the product feed id.

**Total:** `{type, display_text?, amount (signed int), lines?[{display_text, amount}]}`.

| Total type | Sign | Default label |
|---|---|---|
| `subtotal` | + | Subtotal |
| `discount` | − | Discount |
| `items_discount` | − | Item Discounts |
| `fulfillment` | + | Shipping |
| `tax` | + | Tax |
| `fee` | + | Fee |
| `total` | = | Total |

Other values are allowed but MUST carry `display_text`. Each `lines[]` sum must equal its parent. The platform renders totals in the given order and MUST NOT complete autonomously if `sum(non-total) != total`.

**Message** (one of three shapes):
- `error`: `{type:"error", code, content, content_type?(plain|markdown), path?(RFC 9535 JSONPath), severity*}`. `severity` is one of `recoverable`, `requires_buyer_input`, `requires_buyer_review` or `unrecoverable`.
- `warning`: `{type:"warning", code, content, path?, presentation?(notice|disclosure), image_url?, url?}`.
- `info`: `{type:"info", code, content, path?}`.

Codes are open strings:
- Standard error codes (schema examples): `not_found`, `out_of_stock`, `item_unavailable`, `address_undeliverable`, `payment_failed`, `eligibility_invalid`, `identity_required`, `insufficient_scope`. Codes that appear only in spec examples include `missing`, `requires_3ds`, `invalid_phone`, `signal` and `unauthorized`.
- Warning codes: `final_sale`, `prop65`, `fulfillment_changed`, `payment_term_changed`, `age_restricted`. `quantity_adjusted` appears only in examples.
- Info codes: `identity_optional`, `signal`, `free_shipping`, `not_found`.

### 1.5.3 Payment instrument and credential

```json
"payment": { "instruments": [ {
  "id": "pi_1",                     // required: platform-assigned (or a business id for saved instruments)
  "handler_id": "gpay_1234",        // required: = payment_handlers[...][].id advertised by the business
  "type": "card",                   // required: card | shop_pay | apple_pay | ap2_mandate | …
  "selected": true,
  "display": { "brand": "visa", "last_digits": "4242", "description": "…" },
  "billing_address": { "street_address": "…", "extended_address": "…", "address_locality": "…",
                       "address_region": "…", "postal_code": "…", "address_country": "US",
                       "first_name": "…", "last_name": "…", "phone_number": "…" },
  "credential": { "type": "PAYMENT_GATEWAY", "token": "{…Google Pay ECv2 token…}" }
} ] }
```

Credential shapes (`common/types/*_credential.json`):
- `token_credential` `{type, token}` (handler-specific; examples include `PAYMENT_GATEWAY`, `stripe_payment_token`, `pk_payment_token`).
- `network_token` `{number, cryptogram*, expiry_month, expiry_year, eci_value, token_requestor_id, name}`.
- `pan` `{number, expiry_month, expiry_year, cvc, name}`. Only used with PSP tokenization handlers.
- The legacy `card` credential is **deprecated**.

Rules:
- Exactly **one** instrument unless `split_payments` is active. Otherwise the business rejects the submission with `payment_failed`.
- The business MUST NOT echo credentials, and Google also says not to echo `signals`.
- The business MUST check that `handler_id` is one it advertised.
- The business MUST filter the handlers it returns by cart context.

### 1.5.4 Fulfillment extension (`dev.ucp.shopping.fulfillment`)

`fulfillment.methods[]`:
- `id`
- `type`: `shipping` or `pickup` (open)
- `line_item_ids[]`
- `destinations[]`: in responses each has `type` (`shipping_address` or pickup location) and `id`, plus postal address fields
- `selected_destination_id`
- `groups[]`: each has `id`, `line_item_ids[]`, `selected_option_id` and `options[]`
  - each option is `{id, title, description{plain|html|markdown}, carrier?, earliest_fulfillment_time?, latest_fulfillment_time?, totals[]}`

`fulfillment.available_methods[]` gives availability hints in responses. The platform selects by setting `groups[].selected_option_id` (and `selected_destination_id`) on update. Google limits shipping option descriptions to 200 characters.

### 1.5.5 Create example (REST)

```http
POST /ucp/v1/checkout-sessions HTTP/1.1
UCP-Agent: profile="https://platform.example/.well-known/ucp"
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Request-Id: 7b0c…
Content-Type: application/json

{"line_items":[{"item":{"id":"item_123"},"quantity":2}]}
```

```json
HTTP/1.1 201 Created
{
  "ucp": { "version": "2026-08-25",
    "capabilities": { "dev.ucp.shopping.checkout": [ {"version": "2026-08-25"} ] },
    "payment_handlers": { "com.shopify.shop_pay": [ { "id": "shop_pay_1234", "version": "2026-08-25",
        "available_instruments": [ {"type": "shop_pay"} ], "config": { "merchant_id": "shop_merchant_123" } } ] } },
  "id": "chk_1234567890",
  "status": "incomplete",
  "messages": [ { "type": "error", "code": "missing", "path": "$.buyer.email",
                  "content": "Buyer email is required", "severity": "recoverable" } ],
  "currency": "USD",
  "line_items": [ { "id": "li_1", "item": { "id": "item_123", "title": "Red T-Shirt", "price": 2500 },
                    "quantity": 2, "totals": [ {"type":"subtotal","amount":5000}, {"type":"total","amount":5000} ] } ],
  "totals": [ {"type":"subtotal","amount":5000}, {"type":"tax","amount":400}, {"type":"total","amount":5400} ],
  "links": [ { "type": "terms_of_service", "url": "https://business.example.com/terms" } ],
  "continue_url": "https://business.example.com/checkout-sessions/chk_1234567890"
}
```

Complete request: `{"payment":{"instruments":[…]},"signals":{…}}`, plus `"ap2":{"checkout_mandate":"…"}` when AP2 is negotiated. A synchronous response has `status:"completed"` and `"order":{"id":"ord_…","label":"…","permalink_url":"https://…"}`.

## 1.6 Checkout status state machine

`incomplete` ⇄ `requires_escalation` → (buyer via `continue_url`) → `completed`
`incomplete` → `ready_for_complete` → (Complete) → `complete_in_progress` → `completed`
`canceled` can be reached from any non-terminal state (invalid or expired). `completed` is immutable.

| Status | Meaning / rule |
|---|---|
| `incomplete` | Missing info. Read `messages`, then fix through Update. |
| `requires_escalation` | Needs buyer UI. MUST include `continue_url` and at least one message with severity `requires_buyer_input` or `requires_buyer_review`. |
| `ready_for_complete` | All info present and no outstanding Action. The platform may call Complete. |
| `complete_in_progress` | Complete was accepted for async processing. The response MUST NOT contain `order`. Update is forbidden: the business returns the unchanged checkout plus a recoverable error. The platform polls with Get until `expires_at`. |
| `completed` | `order` is present. |
| `canceled` | Terminal. |

Complete retries: re-sending the *identical* request with the *same* idempotency key is allowed only for lost-response recovery. A new Complete after the checkout returns to `ready_for_complete` MUST use a new key.

3DS/SCA:
- Legacy route: return `requires_escalation` with message `requires_3ds` and a PSP challenge `continue_url`.
- New route (2026-08-25): `actions` from `dev.ucp.common.payment.authentication`, either device-data-collection or a `three_ds_challenge` Action.

## 1.7 Order capability, webhooks and signing

**Order object** (`schemas/shopping/order.json`). Required fields: `ucp, id, checkout_id, permalink_url, line_items, fulfillment, currency, totals`. Optional fields: `label, adjustments[], policies[], messages[], attribution`.
- `line_items[]`: `{id, item, quantity{original,total,fulfilled}, totals[], status: processing|partial|fulfilled|removed, parent_id?}`. Status is derived: `total==0` → removed, `fulfilled==total` → fulfilled, `>0` → partial, else processing.
- `fulfillment.expectations[]`: `{id, line_items[{id,quantity}], method_type, destination, description, fulfillable_on ("now" | RFC 3339)}`.
- `fulfillment.events[]`: an **append-only** log of `{id, occurred_at, type (open: processing, shipped, in_transit, delivered, failed_attempt, canceled, undeliverable, returned_to_sender…), line_items[], tracking_number?, tracking_url?, carrier?, description?}`.
- `adjustments[]`: `{id, type (open: refund, return, credit, price_adjustment, dispute, cancellation), occurred_at, status: pending|completed|failed, line_items?[{id, quantity (signed)}], totals[] (signed), description?}`.

**Get Order:** `GET /orders/{id}`. The business MUST authenticate. With platform credentials it may return only orders that platform originated. With a buyer token it returns the buyer's orders, subject to scope `dev.ucp.shopping.order:read`.

**Webhooks:**
- The platform declares `config.webhook_url` (required) in its `dev.ucp.shopping.order` profile entry.
- The business POSTs the **full current-state order** (never deltas) and MUST send an "order created" event, plus one on every update.
- The business MUST retry failures.
- The platform must return 2xx quickly.

Headers: `UCP-Agent` (business profile URL, which MUST point to `/.well-known/ucp`), `Webhook-Id`, `Webhook-Timestamp`, `Content-Digest`, `Signature-Input`, `Signature`, `Content-Type`.

```http
POST /webhooks/ucp/orders HTTP/1.1
Host: platform.example.com
Content-Type: application/json
UCP-Agent: profile="https://merchant.example/.well-known/ucp"
Webhook-Id: 3f2a…            Webhook-Timestamp: 1767225600
Content-Digest: sha-256=:X48E9q...:
Signature-Input: sig1=("@method" "@authority" "@path" "content-digest" "content-type");keyid="merchant-2026"
Signature: sig1=:MEUCIQ...:

{ "ucp": {"version":"2026-08-25","capabilities":{"dev.ucp.shopping.order":[{"version":"2026-08-25"}]}},
  "id":"order_abc123","checkout_id":"chk_…","permalink_url":"…","currency":"USD","line_items":[…],"fulfillment":{…},"totals":[…] }
```

**Webhook signatures are mandatory**: RFC 9421, signed with a key from the business profile's `keys[]`. The platform verifies the signature and also checks that the signer's profile is the business the order was placed with.

## 1.8 Identity linking (`dev.ucp.common.identity_linking`)

- **Access levels:**
  - Public.
  - Agent-authenticated: platform `client_credentials`, e.g. guest checkout.
  - User-authenticated: platform credentials plus a user token, e.g. saved addresses, order history, saved instruments in `payment.instruments`.
- **The business is the OAuth 2.0 Authorization Server.** It MUST:
  - publish RFC 8414 metadata at `/.well-known/oauth-authorization-server`, with OIDC discovery as a fallback;
  - populate `scopes_supported`;
  - return `iss` in the authorization response (RFC 9207);
  - enforce PKCE `S256`;
  - require exact `redirect_uri` matching (port ignored for loopback);
  - declare and enforce `token_endpoint_auth_methods_supported`. SHOULD support `private_key_jwt` or `tls_client_auth`. MAY support `client_secret_basic`, and `none` for public clients but then PKCE is required;
  - validate the bearer token (`iss`, `aud`, `exp`, scopes, `client_id`/`azp`) on every user-authenticated request;
  - implement revocation (RFC 7009). Revoking a refresh token kills its access tokens.
  - SHOULD publish `/.well-known/oauth-protected-resource` (RFC 9728), and MUST publish it if the AS is on another origin. SHOULD support OpenID RISC.
- **Flow:** Authorization Code + PKCE. The platform sends `Authorization: Bearer <token>`.
- **Scopes:** the format is `{capability}:{scope}` with scope names matching `^[a-z][a-z0-9_]*$`. Well-known scopes:
  - `dev.ucp.shopping.checkout:manage`
  - `dev.ucp.shopping.order:read`
  - `dev.ucp.shopping.order:manage`

  The business lists **gating** scopes in `config.scopes`. Each value is a policy object: `{}`, or something like `{description:{plain,markdown}, min_acr, max_token_age, require_mfa}`. Platforms request only the derived set.
- **Errors:**
  - `401` + `WWW-Authenticate: Bearer realm="<issuer>"[, error="invalid_token"], resource_metadata="…"` + a message with `code:"identity_required"`.
  - `403` + `error="insufficient_scope", scope="<full required set>"` + a message with `code:"insufficient_scope"`.
  - Info `identity_optional` hints that signing in adds value.
- **Accelerated IdP flow** (optional): `config.providers` lists trusted external IdPs (`{type:"oauth2", auth_url}`). The platform obtains a JWT authorization grant from the IdP via RFC 8693 token exchange and presents it to the business token endpoint with `grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer` (RFC 7523).
  - Grant requirements: `aud` single-valued, `jti` required, exp ≤ 60s after iat.
  - The business MUST verify against the IdP's `jwks_uri` (fail closed) and SHOULD NOT issue refresh tokens.
  - If the business lists providers, it MUST support the jwt-bearer grant.

## 1.9 Message signatures (RFC 9421) — `docs/specification/signatures.md`

- **Algorithms:**
  - Everyone MUST be able to *verify* **ES256** (ECDSA P-256 / SHA-256).
  - ES384 and EdDSA (Ed25519) are OPTIONAL.
  - The algorithm is derived from the JWK, and `alg` is not put in `Signature-Input`.
  - **ECDSA signatures use raw `r||s` (64 bytes for P-256), not DER.** In .NET, `ECDsa.SignData(..., DSASignatureFormat.IeeeP1363FixedFieldConcatenation)` is the default for `SignData`. Be careful with libraries that emit DER.
- **Request covered components:**
  - always `@method`, `@authority`, `@path`;
  - `@query` if present;
  - `ucp-agent` if present;
  - `idempotency-key` on POST/PUT/DELETE/PATCH;
  - `content-digest` and `content-type` when there is a body.
  - Example: `Signature-Input: sig1=("@method" "@authority" "@path" "ucp-agent" "idempotency-key" "content-digest" "content-type");keyid="platform-2026"` and `Signature: sig1=:<b64>:`.
- **Response covered components:** `@status`, `content-digest`, `content-type`. The `created` parameter is optional.
- **Content-Digest:** `sha-256=:base64(sha256(raw body bytes)):`. No JSON canonicalization is applied, so intermediaries MUST NOT re-serialize the body.
- **Key lookup:** `keyid` → `kid` in the signer profile's `keys[]` (via `UCP-Agent`, or `Signature-Agent` for Web Bot Auth).
- **Replay protection** is provided by the signed `Idempotency-Key`, not timestamps. Default UCP signatures don't need `created`; WBA-shape signatures carry `created`/`expires`/`nonce`.
- **When signing applies:**
  - Requests: platforms SHOULD sign if they use signatures; other auth is allowed.
  - Webhooks: **MUST** be signed.
  - Responses: signing is RECOMMENDED for complete-checkout responses and OPTIONAL for others.
- **Key rotation:** add the new key, start signing with it, accept the old key for at least 7 days, then remove it. Rotate every 90 days (SHOULD).
- **MCP:** the same RFC 9421 headers on the Streamable HTTP POST.

## 1.10 Transports other than REST

- **MCP:**
  - Tools: `create_checkout`, `get_checkout`, `update_checkout`, `complete_checkout`, `cancel_checkout` (plus cart, catalog and order tools).
  - Arguments are `{meta:{"ucp-agent":{profile}, "idempotency-key"}, id?, checkout:{…}}`. `idempotency-key` is required for complete and cancel.
  - The result MUST be in `structuredContent`, SHOULD also be serialized in `content[]`, and the tool SHOULD declare `outputSchema`.
  - Business outcomes are returned as JSON-RPC `result`. Streamable HTTP is required.
- **A2A:**
  - The business exposes an Agent Card, referenced as `endpoint` in the services entry.
  - It advertises an extension with URI `https://ucp.dev/2026-08-25/specification/reference` and `params.capabilities`.
  - UCP payloads travel in `DataPart`s. Headers are `UCP-Agent` and `X-A2A-Extensions`. Idempotency uses the A2A `messageId`.
- **Embedded Checkout Protocol (ECP):**
  - The business's checkout UI is loaded in an iframe or webview from `continue_url`, with query parameters `ec_version` (required), `ec_auth`, `ec_delegate` (e.g. `payment.instruments_change`, `payment.credential`, `fulfillment.address_change`, `window.open`) and `ec_color_scheme`.
  - JSON-RPC 2.0 over postMessage. Core messages: `ec.ready`, `ec.auth`, `ec.start`, `ec.complete`, `ec.line_items.change`, `ec.buyer.change`, `ec.payment.change`, `ec.messages.change`, `ec.totals.change` and `ec.error`. Delegation messages follow the pattern `ec.{domain}.{action}_request`.
  - Google says embedded checkout is only for "specific, approved merchants".

## 1.11 Payment handlers model

- **Who does what:**
  - The provider (Google, Shopify, Stripe…) authors a **handler spec and schema**, e.g. `com.google.pay`, `dev.shopify.shop_pay`, `com.stripe.payments`.
  - The business **configures** it: it lists handler instances in its profile and returns resolved instances in each checkout response under `ucp.payment_handlers`.
  - The platform **executes** it: it obtains a token from the provider and submits it in `payment.instruments[].credential` on Complete.
- **Credential flow is one-way, platform → business.** The business charges the token through its own PSP integration.
- **`available_instruments` resolution:** intersect the platform's support, the business's setup and the cart context. The response value is authoritative.
- **`ucp.request_constraints`** on an available instrument tells the platform which fields are required, e.g. `billing_address.postal_code` or `credential` as `pan`+`cvc` or `network_token`. It uses a JSONPath `path` matching `handler_id` and `type`.
- **Concrete handlers seen in primary sources:**
  - **`com.google.pay`:**
    - `config` = Google Pay API `PaymentDataRequest`-style object: `api_version: 2`, `api_version_minor: 0`, `environment` (TEST/PRODUCTION), `merchant_info{merchant_name, merchant_id, merchant_origin}`, and `allowed_payment_methods[{type:"CARD", parameters{allowed_auth_methods, allowed_card_networks, billing_address_required…}, tokenization_specification{type:"PAYMENT_GATEWAY", parameters{gateway, gatewayMerchantId | stripe:publishableKey …}}}]`.
    - The credential is `{type:"PAYMENT_GATEWAY", token:"<Google Pay token JSON>"}`. The merchant passes it to its gateway (Stripe, Adyen…).
    - Handler schema: `https://pay.google.com/gp/p/ucp/2026-01-11/schemas/config.json`. Guide: https://developers.google.com/pay/api/universal-commerce-protocol/google-pay-payment-handler.
  - **`com.stripe.payments`** (Stripe docs, handler version `2026-06-25`):
    - Business `config{environment: testmode|livemode, business_profile: "profile_…"}`.
    - Instruments: `card` with credential `{type:"stripe_payment_token", token:"spt_…"}`, and `apple_pay` with `{type:"pk_payment_token"}`. `link` is advertised but not supported for completion.
    - The merchant charges it with `POST /v1/payment_intents` (header `Stripe-Version: 2026-09-30.preview`), `amount`, `currency`, `payment_method_data[shared_payment_granted_token]=spt_…` and `confirm=true`. **This is the same SPT mechanism as ACP.**
    - The platform tokenizes at `POST https://commerce.stripe.com/api/ucp/tokenize`. The SPT is bound to the checkout id and limited by `max_amount`.
    - Source: https://docs.stripe.com/agentic-commerce/protocols/ucp/stripe-payments-handler.
  - **`dev.shopify.shop_pay`** (`available_instruments:[{type:"shop_pay"}]`, `config{shop_id, environment}`). Spec URL in the UCP examples: `https://shopify.dev/ucp/shop-pay-handler` (**unverified** beyond the UCP spec's example).
  - **AP2:** in the UCP example, a handler whose instruments are `{type:"ap2_mandate"}`. The payment mandate travels inside the credential token. See section 3.8.3.
- UCP itself ships **no concrete handler**. It provides a handler-author guide and template, plus example handlers (`payment/examples/*`: processor tokenizer, platform tokenizer, encrypted credential) and a tokenization OpenAPI (`source/handlers/tokenization/openapi.json`).

## 1.12 Security requirements (merchant-relevant MUSTs)

- HTTPS everywhere, TLS 1.3 for REST.
- PCI: platforms forward opaque tokens and the business never echoes credentials. PAN credentials are only used with PSP tokenization handlers.
- Signals and attribution may be PII. Don't keep them longer than needed.
- Validate `handler_id`. Use separate TEST/PROD PSP credentials. Make payment processing idempotent.
- Verify authority binding of any schema URL you dereference, and guard against SSRF (resolved-address checks) when fetching platform profiles.
- Logic for checkout sessions MUST be **deterministic**. The business **MUST send a confirmation email** after completion (checkout "Business" guidelines).

## 1.13 Minimal conformant merchant (synthesized from the MUSTs; not an official profile)

1. Serve `/.well-known/ucp` with `ucp.version`, `services["dev.ucp.shopping"]` (at least a `rest` entry with `endpoint`), `capabilities["dev.ucp.shopping.checkout"]`, and `payment_handlers` (at least one handler the target platform supports; for Google, `com.google.pay`). Serve it with the caching headers listed in 1.3.
2. Implement the 5 checkout operations with the exact status codes, the full-PUT update semantics, the status machine, messages/severity, `continue_url` on escalation, `links[]` (privacy + ToS), totals invariants and `expires_at`.
3. On every request: parse `UCP-Agent`, fetch and cache the platform profile, check the version, intersect capabilities, and echo `ucp.version`, `ucp.capabilities` and `ucp.payment_handlers`. Support `Idempotency-Key` semantics (24h, 409 on mismatch).
4. Authenticate the platform. In practice Google sends `Authorization: Bearer` (OAuth), and RFC 9421 lets you onboard platforms without prior registration. Be able to verify ES256 signatures if you accept them.
5. Charge the token from the advertised handler in Complete and return `completed` + `order{id,permalink_url}`.
6. Recommended next steps:
   - `dev.ucp.shopping.fulfillment` (Google **requires** it for physical goods);
   - `dev.ucp.shopping.order` with signed webhooks (requires an ES256 key in `keys[]`);
   - `dev.ucp.shopping.discount`;
   - identity linking (requires an OAuth AS; Orchard Core OpenId can serve this role);
   - MCP binding.
7. Test with the official **conformance suite**: https://github.com/Universal-Commerce-Protocol/conformance. It is language-agnostic and runs against a running merchant server; you seed test data (flower_shop) and supply `conformance_input.json` with `ucp_version` and `required_capabilities`.

## 1.14 Breaking changes 2026-04-08 → 2026-08-25 (from the release notes)

- Fulfillment schema:
  - `allows_` prefix dropped (`multi_destination`, `method_combinations`);
  - `fulfillment_option.description` became an object `{plain,html,markdown}`;
  - `multi_destination` changed from a map to an array;
  - `fulfillment_available_method.type` opened to a string;
  - `merchant_fulfillment_config.json` became `business_fulfillment_config.json`.
- Buyer consent changed from booleans to a reverse-DNS map of `consent_purpose`.
- Profile `signing_keys[]` was removed in favour of top-level `keys[]`.
- Schemas moved under `common/types/` (`$id` URLs changed). Payment extensions were renamed from `dev.ucp.shopping.*` to `dev.ucp.common.payment.*` (`ap2_mandate`, `split_payments`, `terms`).
- `$requestConstraints` / `ucp.request_constraints` replaced static instrument requirements, and the PAN and network-token credentials were split.
- `retail_location.json` became `common/types/location.json`. Explicit destination types were introduced.
- New in this release: Actions (3DS2), `map_order`, authority binding, Web Bot Auth interop, attribution, `policies[]`, permalink, fractional quantities, location capabilities, loyalty, payment terms and split payments.

## 1.15 UCP tooling and SDKs (GitHub org `Universal-Commerce-Protocol`)

| Repo | What | .NET relevance |
|---|---|---|
| `ucp` | Spec, JSON Schemas (draft 2020-12), OpenAPI 3 (JSON) and OpenRPC | **Generate C# types** from `source/schemas` + `source/services/shopping/rest.openapi.json`. Note the custom `ucp_request` annotations: one schema yields different request shapes per operation. |
| `ucp-schema` | Rust CLI/library (`cargo install ucp-schema`): `compose`, `resolve --op create|update|complete --request`, `validate`, `lint` | Use `resolve` to emit **per-operation resolved JSON Schemas**, then run NJsonSchema/quicktype to generate C#. |
| `python-sdk` | Pydantic models (`ucp-sdk` on PyPI) | Reference only. |
| `js-sdk` | TypeScript types + Zod. `0.6.x` → `2026-08-25`, `0.4.x` → `2026-04-08` | Reference only. |
| `samples` | Python/FastAPI and Node/Hono merchant servers + happy-path client | Behavioural reference. |
| `conformance` | Language-agnostic integration tests against your server | **Use for certification-style testing.** |

There is **no official .NET SDK** (verified: no such repo in the org as of 2026-11-02).

---

# 2. ACP — Agentic Commerce Protocol

> **Read on:** 2026-11-02. **Primary source:** `github.com/agentic-commerce-protocol/agentic-commerce-protocol`, `main` @ `7fdd78d` (2026-07-17, "SEP: Optional product URL on checkout Item (#281)"). Paths below are relative to the repo root.
> **Latest stable version:** **`2026-04-17`**. All field tables below are from `spec/2026-04-17/json-schema/*.json` (canonical) cross-checked against `spec/2026-04-17/openapi/*.yaml`. Differences in `spec/unreleased/` are called out in §2.15.
> **Maintainers:** OpenAI and Stripe (Founding Maintainers); Meta was added to the Technical Steering Committee in `unreleased` (`changelog/unreleased/add-meta-tsc-member.md`). License Apache-2.0. Status "beta" (README badge).
> **Legend:** `*` = required. "unverified" = not confirmed from a primary source.

---

## 2.0 Executive summary for the merchant (seller) side

| Concern | What ACP 2026-04-17 says | Who hosts it |
|---|---|---|
| Checkout API (5 endpoints) | **MUST implement**. REST/JSON over HTTPS. Bearer auth. `API-Version` required. `Idempotency-Key` required on every POST | **Merchant** |
| Discovery doc `/.well-known/acp.json` | Optional, added in 2026-04-17 | **Merchant** |
| MCP binding (5 tools) | Optional second transport, added in 2026-04-17 | **Merchant** (optional) |
| Cart API (`/carts`) | Optional, added in 2026-04-17 | **Merchant** (optional) |
| Order webhooks (`order_create`, `order_update`) | **MUST emit**. Signed with `Merchant-Signature: t=…,v1=…` (HMAC-SHA256) | **Agent** receives. Merchant sends |
| Delegate Payment (`POST /agentic_commerce/delegate_payment`) | Card vaulting with an allowance. Returns a `vt_…` token | **PSP or vault** (e.g. Stripe). Not the merchant, unless the merchant is a PCI DSS Level 1 vault |
| Charging the token | Out of scope for ACP. With Stripe: `PaymentIntent` with `payment_method_data[shared_payment_granted_token]=spt_…` | Merchant → its PSP |
| Delegate Authentication (3DS2) | Optional. The agent calls the merchant-designated authentication provider | **Authentication provider** |
| Product Feed API (`/feeds`) | Push model: the **merchant pushes** products to an **agent-hosted** API | **Agent** hosts it. Merchant calls it |

**Market status (important).** Trade press reported on 2026-03-06 that OpenAI is scaling back ChatGPT **Instant Checkout**. The quoted OpenAI statement was "Instant Checkout is moving to Apps", and OpenAI said it is "prioritizing making ChatGPT search and product discovery great". Secondary sources date a confirmation to 2026-03-24. **This is unverified: no openai.com announcement was found.** Source: digitalcommerce360.com/2026/03/06/openai-shifts-checkout-plans-agentic-commerce-strategy/.

OpenAI's current developer docs at developers.openai.com/commerce still document checkout, delegated payment and feeds. The "Get started" guide now covers only **product feed onboarding** ("currently available to approved partners", apply at `https://chatgpt.com/merchants`).

Stripe's "use your own checkout" seller page now points to **UCP**, not ACP: docs.stripe.com/agentic-commerce/sellers/use-cases/retail.md.

**Implication:** the product feed is the highest-value surface right now. The ACP checkout endpoints remain the open standard, but OpenAI-side certification is gated by partner approval.

---

## 2.1 Versioning

*Source: `README.md` §Versioning, `changelog/*.md`, `rfcs/rfc.agentic_checkout.md` §2.1, GitHub API (tags, releases and commits checked 2026-11-02).*

- **Model:** date-based `YYYY-MM-DD`. Each version is a complete snapshot under `spec/<version>/`. New work goes in `spec/unreleased/` and is snapshotted on release. Older versions are marked deprecated in the changelog.
- **Released versions:**

  | Version | Theme |
  |---|---|
  | `2025-09-29` | Initial |
  | `2025-12-12` | Fulfillment enhancements. Breaking |
  | `2026-01-16` | 3DS and authentication provider |
  | `2026-01-30` | Capability negotiation, payment handlers (breaking), extensions, discounts |
  | **`2026-04-17`** | Cart, feed, orders, delegate authentication, MCP, discovery, idempotency rewrite, marketing consent |

  The changelog also mentions a deprecated `2026-01-22`. There is no folder for it.
- **Nothing newer than `2026-04-17` has been released** as of 2026-11-02. The repo has no GitHub releases and no tags. The `spec/` and `changelog/` directories on GitHub match the clone, and the last commit is 2026-07-18T04:51Z (UTC).
- **Header:** the client **MUST** send `API-Version: <YYYY-MM-DD>`. The server **MUST** validate it.

  On a missing or unsupported version, the server **SHOULD** return **HTTP 400** with an `Error` that has `supported_versions[]`. The `code` **MAY** be `unsupported_api_version` or `missing_api_version`.

  `Error.supported_versions` is ordered newest first. `DiscoveryProtocol.supported_versions` is chronological, oldest first. Note the opposite orderings.

  ```json
  { "type": "invalid_request", "code": "unsupported_api_version",
    "message": "API version '2025-01-01' is not supported",
    "supported_versions": ["2026-04-17", "2026-01-30"] }
  ```
- `CheckoutSession.protocol.version` (pattern `^\d{4}-\d{2}-\d{2}$`) echoes the version that was negotiated.
- **Version strings in OpenAI's docs lag the repo.** OpenAI's checkout page examples use `API-Version: 2025-09-12` with the old `items`, `fulfillment_address` and `payment_data{token,provider}` shapes. OpenAI's delegated-payment page uses `2025-09-29`.

  Which version ChatGPT sends in production is **unverified**. A merchant should support at least `2026-01-30` and `2026-04-17`, and be prepared for the legacy shapes from 2025.

---

## 2.2 Transport, headers, auth and signatures

*Source: `spec/2026-04-17/openapi/openapi.agentic_checkout.yaml` `components.parameters` and `rfcs/rfc.agentic_checkout.md` §3.1, §7.*

All endpoints **MUST** use HTTPS and return JSON. All money is **integer minor units**.

- The RFC says TLS 1.3 **MUST** be used.
- OpenAI's production guide says "TLS 1.2 or later on port 443" with a valid public certificate.

### 2.2.1 Request headers (agent → merchant)

| Header | Required? | Format | Notes |
|---|---|---|---|
| `Authorization` | **Required** | `Bearer <token>` | `securitySchemes.bearerAuth: http/bearer, bearerFormat: "API Key"`. The merchant issues the key to the agent out of band |
| `API-Version` | **Required** | `YYYY-MM-DD` | See §2.1 |
| `Idempotency-Key` | **Required on every POST**. Not on GET | string, 1–255 chars. UUID v4 recommended | Scoped to the authenticated identity plus the endpoint path. See §2.6 |
| `Content-Type` | Required on requests with a body | `application/json` | |
| `Accept-Language` | Recommended | e.g. `en-US` | Localize `display_text` and messages |
| `User-Agent` | Recommended | e.g. `ChatGPT/2.0 (Mac OS X 15.0.1; arm64; build 0)` | |
| `Request-Id` | Recommended | string | Correlation ID |
| `Signature` | Recommended (optional) | base64url | See §2.2.3 |
| `Timestamp` | Recommended (optional) | RFC 3339 `date-time` | Freshness check with a bounded clock-skew window |

### 2.2.2 Response headers (merchant → agent)

| Header | When |
|---|---|
| `Idempotency-Key` | Echo on all POST 2xx responses |
| `Idempotent-Replayed: true` | On a replayed cached response (`enum ["true"]`) |
| `Request-Id` | Echo if it was provided |
| `Retry-After` (seconds) | **Required** on `409 idempotency_in_flight` |

### 2.2.3 Request signatures — **largely UNSPECIFIED**

- The RFC (§2.1, §7) says only that the server **SHOULD** publish acceptable signature algorithms **out of band**. The client **SHOULD** sign over **canonical JSON**, put the signature in `Signature` (base64url), and send `Timestamp` (RFC 3339). The server **SHOULD** verify within a bounded clock-skew window.
- The delegate-payment RFC §2.1–2.2 gives example algorithms, "Ed25519, ES256", advertised out of band. There:
  - the client **MUST** serialize with "a canonical JSON scheme";
  - the client **MUST** compute a **detached** signature, base64url-encode it, and put it in `Signature`;
  - in §2.3 the `Signature` header is still marked RECOMMENDED.
- **The spec does not define** the canonicalization algorithm (no JCS / RFC 8785 mandate for signing), the signed string (body only, or body plus timestamp), the key format, or the key distribution mechanism (no JWKS URL).

  The OpenAPI description of the `Signature` parameter even calls it an "HMAC signature for webhook verification", which is a copy-paste error.

  OpenAI's checkout page says only "Base64-encoded signature of the request body". The algorithm and keys are **unverified / not published**.

  OpenAI's production checklist asks for "canonical JSON serialization and correct detached signature generation" on delegate-payment test calls.
- **Practical guidance:**
  - Authenticate with the Bearer key.
  - Allowlist OpenAI's published egress IP ranges. OpenAI's production guide requires this.
  - Treat `Signature`/`Timestamp` verification as a pluggable, optional verifier. Make the algorithm and public key configurable, and the canonicalization JCS (RFC 8785) by default (an assumption).

### 2.2.4 Webhook signature (merchant → agent) — **specified**

See §2.9. Format: `Merchant-Signature: t=<unix_seconds>,v1=<64 hex>`, where `v1 = HMAC-SHA256(secret, t + "." + raw_body)`.

---

## 2.3 Checkout API — endpoints

*Source: `spec/2026-04-17/openapi/openapi.agentic_checkout.yaml` `paths`. Server base example: `https://merchant.example.com`. The discovery doc's `api_base_url` is prepended.*

| # | Method & path | operationId | Request body | Success | Error responses |
|---|---|---|---|---|---|
| 1 | `POST /checkout_sessions` | `createCheckoutSession` | `CheckoutSessionCreateRequest` (required) | **201** `CheckoutSession` | 400 `idempotency_key_required`, 409 `idempotency_in_flight`, 422 `idempotency_conflict`, 4XX/5XX `Error` |
| 2 | `POST /checkout_sessions/{checkout_session_id}` | `updateCheckoutSession` | `CheckoutSessionUpdateRequest` (required) | **200** `CheckoutSession` | 400 / 409 / 422 / 4XX / 5XX |
| 3 | `GET /checkout_sessions/{checkout_session_id}` | `getCheckoutSession` | — (no `Idempotency-Key`) | **200** `CheckoutSession` | **404** `Error` |
| 4 | `POST /checkout_sessions/{checkout_session_id}/complete` | `completeCheckoutSession` | `CheckoutSessionCompleteRequest` (required) | **200** `CheckoutSessionWithOrder` (`status: completed` + `order`) | 400 / 409 / 422 / 4XX / 5XX. A 4XX `requires_3ds` is required when `authentication_result` is missing in `authentication_required` |
| 5 | `POST /checkout_sessions/{checkout_session_id}/cancel` | `cancelCheckoutSession` | `CancelSessionRequest` (**optional** body) | **200** `CheckoutSession` (`status: canceled`) | **405** if it is already `completed`/`canceled`, plus 400 / 409 / 422 |

**Semantics:**

- Every response returns the **full authoritative session state**. Agents render it as is.
- `complete` **MUST** create an order.
- OpenAI's certification list says "HTTP 201 Created" for completion, which conflicts with the spec's **200**. Return 200 per the spec. The OpenAI wording is **unverified/inconsistent**.
- The cancel endpoint **MUST** accept an optional JSON body (`intent_trace`). Servers that don't implement intent traces must still accept the body and ignore it (`rfcs/rfc.intent_traces.md` §7.1).
- **Update semantics:** absent key = "do not modify". Explicit `null` = "clear this field" (RFC §6.2). `line_items` on update replaces the list (full-replacement pattern, as in carts).
- `discounts.codes` replaces the previously submitted codes. `[]` clears them.

### 2.3.1 Session status enum (`CheckoutSessionBase.status`)

```
incomplete | not_ready_for_payment | requires_escalation | authentication_required |
ready_for_payment | pending_approval | complete_in_progress | completed | canceled |
in_progress | expired
```

The RFC prose still lists only the original five, plus `authentication_required`.

- `authentication_required` means the session **MUST** include `authentication_metadata`.
- Cancel is allowed from any state other than `completed` and `canceled`.

---

## 2.4 Checkout request schemas (2026-04-17)

*Source: `spec/2026-04-17/json-schema/schema.agentic_checkout.json` `$defs`. All objects are `additionalProperties: false` unless noted. That matters for strict deserializers: accept unknown fields leniently.*

### 2.4.1 `CheckoutSessionCreateRequest`

| Field | Type | Notes |
|---|---|---|
| `line_items`* | `Item[]`, minItems 1 | Before 2026-01-30 this was `items: [{id*, quantity*}]` |
| `currency`* | string | ISO 4217 (lowercase in examples, e.g. `usd`) |
| `capabilities`* | `Capabilities` | Agent capabilities (§2.7) |
| `buyer` | `Buyer` | |
| `fulfillment_details` | `FulfillmentDetails` | |
| `fulfillment_groups` | `FulfillmentGroup[]` | |
| `affiliate_attribution` | `AffiliateAttribution` | First touch (`touchpoint: "first"`) |
| `discounts` | `DiscountsRequest` `{codes: string[]}` | Discount extension |
| `coupons` | string[] | **DEPRECATED** alias of `discounts.codes` |
| `locale` | string | e.g. `en-US` |
| `timezone` | string | IANA |
| `quote_id` | string | |
| `metadata` | object | Free-form |
| `order_notes` | string, maxLength 5000 | |

**SPEC DEFECT — quantity.** In 2026-01-30 and later, `Item` is `{id*, name?, unit_amount?}` with `additionalProperties:false`. **`quantity` is not on `Item`**, so the create, update and cart requests cannot express quantity by schema. Yet the RFC and cart examples send `{"id":"item_123","quantity":2}`, and `LineItem.quantity` (response) is required.

The "decimal quantity (B2B)" changelog change (integer → number, `exclusiveMinimum: 0`) was applied only to the `2025-09-29` folder.

**Recommendation:** accept an optional `quantity` (decimal > 0, default 1) on inbound `Item` and do not reject unknown properties.

### 2.4.2 `Item` (request line)

| Field | Type |
|---|---|
| `id`* | string. The merchant's item/variant ID, matching the feed `Variant.id` / `item_id` |
| `name` | string |
| `unit_amount` | integer (minor units) |
| *(unreleased)* `suggested_price` | `SuggestedPrice {amount* int, source* PriceSource{feed_id*, channel?, feed_update_id?, updated_at?}, observed_at* date-time}` |
| *(unreleased)* `url` | uri. Product page URL for disambiguation |

### 2.4.3 `CheckoutSessionUpdateRequest`

All fields are optional:

- `buyer`
- `line_items: Item[]`
- `fulfillment_details`
- `fulfillment_groups`
- `selected_fulfillment_options: SelectedFulfillmentOption[]`
- `discounts`
- `coupons` (deprecated)
- `order_notes` (≤5000)

### 2.4.4 `CheckoutSessionCompleteRequest`

| Field | Type | Notes |
|---|---|---|
| `payment_data`* | `PaymentData` | |
| `buyer` | `Buyer` | Final buyer info |
| `authentication_result` | `AuthenticationResult` | **MUST** be sent when the session is `authentication_required` |
| `affiliate_attribution` | `AffiliateAttribution` | Last touch. Stored with the order, not returned |
| `risk_signals` | `RiskSignals {ip_address, user_agent, accept_language, session_id, device_fingerprint}` (all string, optional) | |
| `marketing_consents` | `MarketingConsent[] {channel* string, opted_in* bool}` | Absent preserves existing subscriptions |
| `order_notes` | string ≤5000 | |
| *(unreleased)* `fulfillment_details` | `FulfillmentDetails` | Seller **MUST** apply it before payment. The changelog says an address change triggers **409 Conflict** (schema: unverified) |

### 2.4.5 `PaymentData` (since 2026-01-30)

`anyOf: [{required:[handler_id, instrument]}, {required:[purchase_order_number]}]`

| Field | Type |
|---|---|
| `handler_id` | string. Matches a `capabilities.payment.handlers[].id` |
| `instrument` | `{ type*: string (e.g. "card", "seller_backed_saved_card"), credential*: { type*: string (e.g. "spt"), token*: string } }` |
| `billing_address` | `Address` |
| `purchase_order_number` | string (B2B) |
| `payment_terms` | enum `immediate, net_15, net_30, net_60, net_90` |
| `due_date` | date-time |
| `approval_required` | boolean |

Legacy shape (2025-09-29 to 2026-01-16): `{ "token": "spt_123", "provider": "stripe", "billing_address": {…} }`.

The 2026-04-17 OpenAPI inline example **still shows the legacy shape**, which is another doc defect. Accept both.

### 2.4.6 `CancelSessionRequest`

`{ intent_trace?: IntentTrace }`

`IntentTrace`:

- `reason_code`*: an **extensible** enum: `price_sensitivity, shipping_cost, shipping_speed, product_fit, trust_security, returns_policy, payment_options, comparison, timing_deferred, other`. Treat unknown codes as `other`.
- `trace_summary`: string ≤500.
- `metadata`: a flat map of string, number or boolean values. No nesting.

The trace is write-only and **MUST NOT** appear in GET.

### 2.4.7 Shared request/response types

| Type | Fields |
|---|---|
| `Address` | `name`*, `line_one`*, `line_two`, `city`*, `state`*, `country`* (ISO 3166-1 alpha-2), `postal_code`*. **JSON Schema only:** `company` |
| `FulfillmentDetails` | `name`, `phone_number` (E.164 recommended), `email` (format email), `address: Address` |
| `Buyer` | `email`* (format email), `first_name`, `last_name`, `full_name`, `phone_number`, `customer_id`, `account_type` enum `guest\|registered\|business`, `authentication_status` enum `authenticated\|guest\|requires_signin`, `company: CompanyInfo{name*, tax_id, department, cost_center}`, `loyalty: LoyaltyInfo{tier, points_balance int, member_since}`, `tax_exemption: TaxExemption{certificate_id*, certificate_type* enum resale\|exempt_organization\|government, exempt_regions[], expires_at}` |
| `FulfillmentGroup` | `id`*, `item_ids`*[], `destination_type`* enum `shipping\|pickup\|local_delivery\|digital`, `fulfillment_details`, `location_id`, `instructions` |
| `SelectedFulfillmentOption` | `type`* enum `shipping\|digital\|pickup\|local_delivery`, `option_id`*, `item_ids`*[] |
| `AffiliateAttribution` | `provider`* (e.g. `impact.com`), `token` *or* `publisher_id` (anyOf one is required), `campaign_id`, `creative_id`, `sub_id`, `source{type* url\|platform\|unknown, url}`, `issued_at`, `expires_at`, `metadata` (map of string, number or bool), `touchpoint` enum `first\|last` |

---

## 2.5 Checkout response schema — `CheckoutSession`

*Source: `$defs.CheckoutSessionBase`. `CheckoutSession = allOf[Base]`. `CheckoutSessionWithOrder = allOf[Base, {order*: Order}]`.*

### 2.5.1 `CheckoutSessionBase`

| Field | Type | Notes |
|---|---|---|
| `id`* | string | |
| `capabilities`* | `Capabilities` | Seller view, the intersection (§2.7) |
| `status`* | enum (§2.3.1) | |
| `currency`* | string | ISO 4217 settlement currency |
| `line_items`* | `LineItem[]` | |
| `fulfillment_options`* | `oneOf[Shipping\|Digital\|Pickup\|LocalDelivery][]` | Discriminator `type` |
| `totals`* | `Total[]` | At least one `type:"total"` **SHOULD** be present when calculable |
| `messages`* | `oneOf[MessageInfo\|MessageWarning\|MessageError][]` | |
| `links`* | `Link[]` | OpenAI certification requires working ToS and Privacy links |
| `protocol` | `{version*: "YYYY-MM-DD"}` | |
| `buyer` | `Buyer` | |
| `presentment_currency`, `exchange_rate` (number), `exchange_rate_timestamp` | | Multi-currency |
| `locale`, `timezone` | string | |
| `fulfillment_details` | `FulfillmentDetails` | |
| `selected_fulfillment_options` | `SelectedFulfillmentOption[]` | Each `option_id` **MUST** match an element of `fulfillment_options` |
| `fulfillment_groups` | `FulfillmentGroup[]` | |
| `authentication_metadata` | `AuthenticationMetadata` | **MUST** be present when the status is `authentication_required` |
| `created_at`, `updated_at`, `expires_at` | date-time | |
| `continue_url` | uri | Hand-off to the merchant site |
| `metadata` | object | |
| `quote_id`, `quote_expires_at` | | |
| `marketing_consent_options` | `MarketingConsentOption[] {channel* (open: email\|sms\|whatsapp…), display_text*, privacy_policy_url* uri, is_subscribed bool}` | |
| `discounts` | `DiscountsResponse {codes[], applied: AppliedDiscount[], rejected: RejectedDiscount[]}` | When the `discount` extension is active |
| `order` | `Order` | JSON Schema only. Required in `CheckoutSessionWithOrder` |

### 2.5.2 `LineItem` (response)

| Field | Type |
|---|---|
| `id`* | string (line-item ID) |
| `item`* | `Item` |
| `quantity`* | integer ≥1. This conflicts with the decimal-quantity intent |
| `totals`* | `Total[]`. Per-line breakdown: `items_base_amount`, `discount`, `subtotal`, `tax`, `total` |
| `name`, `description` | string |
| `images` | uri[] |
| `unit_amount` | integer |
| `disclosures` | `Disclosure[] {type* "disclaimer", content_type* plain\|markdown, content*}` |
| `custom_attributes` | `{display_name*, value*}[]` |
| `marketplace_seller_details` | `{name*}` |
| `product_id`, `sku`, `variant_id`, `category` | string |
| `tags` | string[] |
| `weight` | `{value* number, unit* g\|kg\|oz\|lb}` |
| `dimensions` | `{length*, width*, height* number, unit* cm\|in}` |
| `availability_status` | enum `in_stock\|low_stock\|out_of_stock\|backorder\|pre_order` |
| `available_quantity` (≥0), `max_quantity_per_order` (≥1) | integer |
| `fulfillable_on` | date-time |
| `variant_options` | `{name*, value*}[]` |
| `discount_details` | `{code, type* percentage\|fixed\|bogo\|volume, amount* int, description, source coupon\|automatic\|loyalty}[]` |
| `tax_exempt` | bool |
| `tax_exemption_reason` | string |
| `parent_id` | string (bundles) |

Before 2026-01-30, `LineItem` had flat required integers: `base_amount`, `discount`, `subtotal`, `tax`, `total`. Those were replaced by `totals[]`.

### 2.5.3 `Total`

| Field | Type |
|---|---|
| `type`* | enum `items_base_amount, items_discount, subtotal, discount, fulfillment, tax, fee, gift_wrap, tip, store_credit, total, amount_refunded` |
| `display_text`* | string (localized) |
| `amount`* | integer (minor units) |
| `presentment_amount` | integer |
| `description` | string (e.g. a fee explanation) |
| `breakdown` | `TaxBreakdownItem[] {jurisdiction*, rate* number, amount* int}` |

Discount invariants (`rfc.discount_extension.md` §8):

- `totals[items_discount].amount == Σ line discounts`.
- Discount amounts are positive integers, displayed as subtractive.

### 2.5.4 Fulfillment options

All four share `type`* (const), `id`*, `title`*, `description`, and `totals`* (`Total[]`).

| Type | Extra fields |
|---|---|
| `shipping` | `carrier`, `earliest_delivery_time`, `latest_delivery_time` (date-time) |
| `digital` | — |
| `pickup` | `location`* `{name*, address*: Address, phone, instructions}`, `pickup_type` enum `in_store\|curbside\|locker`, `ready_by`, `pickup_by` |
| `local_delivery` | `delivery_window {start*, end*}`, `service_area {radius_miles number, center_postal_code}` |

OpenAI FAQ: one shipping address and one selected shipping option per session in practice. If you split shipments, present one consolidated option (OpenAI production guide).

### 2.5.5 Messages

| Type | Fields |
|---|---|
| `MessageInfo` | `type`* `"info"`, `content_type`* `plain\|markdown`, `content`*, `severity`, `resolution`, `param` (RFC 9535 JSONPath) |
| `MessageWarning` | `type`* `"warning"`, `code`*, `content_type`*, `content`*, `severity`, `resolution`, `param` |
| `MessageError` | `type`* `"error"`, `code`*, `content_type`*, `content`*, `severity`, `resolution`, `param` |

Enum values:

- `severity`: `info, low, medium, high, critical`.
- `resolution`: `recoverable` (the agent can fix it via the API), `requires_buyer_input`, `requires_buyer_review`.
- **MessageWarning `code`:** `low_stock, high_demand, shipping_delay, price_change, expiring_promotion, limited_availability`, plus the 8 `discount_code_*` codes (`expired, invalid, already_applied, combination_disallowed, minimum_not_met, user_not_logged_in, user_ineligible, usage_limit_reached`).
- **MessageError `code`:** `missing, invalid, out_of_stock, payment_declined, requires_sign_in, requires_3ds, low_stock, quantity_exceeded, coupon_invalid, coupon_expired, minimum_not_met, maximum_exceeded, region_restricted, age_verification_required, approval_required, unsupported, not_found, conflict, rate_limited, expired, intervention_required`.

Markdown content **MUST** be CommonMark 0.31.2 with **no raw HTML**. Servers **MUST** reject raw HTML (RFC §5.1).

Business errors such as out of stock or payment declined are returned as a **200/201 session with a `MessageError`**, not as an HTTP error. Examples: `checkout_session_with_out_of_stock` and `checkout_session_with_payment_declined` in `examples/2026-04-17/examples.agentic_checkout.json`.

### 2.5.6 `Link`

| Field | Type |
|---|---|
| `type`* | enum `terms_of_use, privacy_policy, return_policy, shipping_policy, contact_us, about_us, faq, support` |
| `url`* | uri |
| `title` | string |

### 2.5.7 `Order` (in the complete response and in webhooks)

| Field | Type |
|---|---|
| `id`* | Merchant order ID |
| `checkout_session_id`* | string |
| `permalink_url`* | uri |
| `type` | const `"order"`. Required in webhooks |
| `order_number` | Human-readable |
| `client_reference_id` | Agent's reference (e.g. `chatgpt_txn_abc789`). JSON Schema only |
| `status` | **Open** enum. Defined: `created, confirmed, manual_review, processing, shipped, delivered, canceled`. In `unreleased`, `delivered` → `completed` |
| `estimated_delivery` | `{earliest*, latest*}` |
| `confirmation` | `{confirmation_number, confirmation_email_sent bool, receipt_url, invoice_number}`. The JSON Schema adds `order_notes` |
| `support` | `{email, phone, hours, help_center_url}` |
| `line_items` | `OrderLineItem[]` (see below) |
| `fulfillments` | `Fulfillment[]` (see below) |
| `adjustments` | `Adjustment[]` (see below) |
| `totals` | `Total[]`. The `total` entry is the original charged amount. `amount_refunded` tracks refunds |

`OrderLineItem`:

- `id`*, `title`*, `product_id`, `description`, `image_url`, `url`, `unit_price` int, `subtotal` int, `totals[]`.
- `quantity`*: `{ordered* ≥1, current* ≥0, fulfilled ≥0 (default 0)}`.
- `status` (open): `processing, partial, fulfilled, removed`.

`Fulfillment`:

- `id`*, `type`* enum `shipping\|pickup\|digital`.
- `status` (open): `pending, processing, shipped, in_transit, delivered, ready_for_pickup, …`.
- `line_items[] {id*, quantity*}`, `carrier`, `tracking_number`, `tracking_url`, `destination: Address`, `estimated_delivery`.
- `digital_delivery{access_url, license_key, expires_at}`, `description`.
- `events[]: {id*, type*, occurred_at*, description, location}`.

`Adjustment`:

- `id`*, `type`* (open): `refund, credit, return, exchange, price_adjustment, dispute, …`.
- `occurred_at`*, `status`* (open): `pending, completed, failed`.
- `line_items[]`, `amount` int (tax-inclusive credit to the buyer), `currency` `^[a-z]{3}$`, `description`, `reason`.

### 2.5.8 `Error` (HTTP-level, flat — no envelope)

| Field | Type |
|---|---|
| `type`* | enum `invalid_request \| processing_error \| service_unavailable` |
| `code`* | string. Implementation-defined. Well-known: `idempotency_key_required`, `idempotency_conflict`, `idempotency_in_flight`, `requires_3ds`, `unsupported_api_version`, `missing_api_version`, `invalid_signature` (webhooks) |
| `message`* | string |
| `param` | RFC 9535 JSONPath (e.g. `$.line_items[0].quantity`) |
| `supported_versions` | string[]. Only on version errors |

`request_not_idempotent` was removed from `Error.type` in 2026-04-17. The MCP binding doc still lists it.

### 2.5.9 Example — create request and response (abridged from `examples/2026-04-17/examples.agentic_checkout.json`)

```json
POST /checkout_sessions
API-Version: 2026-04-17
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
{
  "currency": "usd",
  "line_items": [{ "id": "item_123" }],
  "capabilities": { "interventions": { "supported": ["3ds","address_verification"],
     "display_context": "webview", "redirect_context": "in_app", "max_redirects": 1, "max_interaction_depth": 1 } },
  "fulfillment_details": { "name": "John Doe", "phone_number": "15551234567", "email": "johndoe@example.com",
     "address": { "name": "John Doe", "line_one": "1234 Chat Road", "city": "San Francisco",
                  "state": "CA", "country": "US", "postal_code": "94131" } },
  "order_notes": "Leave at front door if I'm not home."
}
```

```json
201 Created
{
  "id": "checkout_session_123",
  "protocol": { "version": "2026-04-17" },
  "capabilities": {
    "payment": { "handlers": [{
      "id": "card_tokenized", "name": "dev.acp.tokenized.card", "display_name": "Credit Card",
      "version": "2026-01-22", "spec": "https://acp.dev/handlers/tokenized.card",
      "requires_delegate_payment": true, "requires_pci_compliance": false, "psp": "stripe",
      "config_schema": "https://acp.dev/schemas/handlers/tokenized.card/config.json",
      "instrument_schemas": ["https://acp.dev/schemas/handlers/tokenized.card/instrument.json"],
      "config": { "merchant_id": "acct_1234567890", "psp": "stripe",
                  "accepted_brands": ["visa","mastercard","amex","discover"],
                  "accepted_funding_types": ["credit","debit"], "supports_3ds": true,
                  "3ds_versions": ["2.1.0","2.2.0"], "environment": "production" },
      "display_order": 1 }] },
    "interventions": { "supported": ["3ds","address_verification"], "required": [], "enforcement": "conditional" }
  },
  "status": "ready_for_payment",
  "currency": "usd",
  "line_items": [{ "id": "line_item_123", "item": { "id": "item_123" }, "quantity": 1,
     "name": "Vintage Denim Jacket", "unit_amount": 300,
     "totals": [ {"type":"items_base_amount","display_text":"Base Amount","amount":300},
                 {"type":"discount","display_text":"Discount","amount":0},
                 {"type":"subtotal","display_text":"Subtotal","amount":300},
                 {"type":"tax","display_text":"Tax","amount":30},
                 {"type":"total","display_text":"Total","amount":330} ] }],
  "fulfillment_details": { "...": "echo" },
  "fulfillment_options": [
    { "type": "shipping", "id": "fulfillment_option_123", "title": "Standard", "carrier": "USPS",
      "earliest_delivery_time": "2025-10-12T07:20:50.52Z", "latest_delivery_time": "2025-10-13T07:20:50.52Z",
      "totals": [{ "type": "total", "display_text": "Shipping", "amount": 100 }] } ],
  "selected_fulfillment_options": [{ "type": "shipping", "option_id": "fulfillment_option_123", "item_ids": ["item_123"] }],
  "totals": [ {"type":"items_base_amount","display_text":"Item(s) total","amount":300},
              {"type":"subtotal","display_text":"Subtotal","amount":300},
              {"type":"tax","display_text":"Tax","amount":30},
              {"type":"fulfillment","display_text":"Fulfillment","amount":100},
              {"type":"fee","display_text":"Service Fee","amount":50,"description":"Processing and handling fee"},
              {"type":"total","display_text":"Total","amount":480} ],
  "marketing_consent_options": [{ "channel": "email", "display_text": "Promotional emails…",
      "privacy_policy_url": "https://www.testshop.com/privacy", "is_subscribed": false }],
  "messages": [],
  "links": [{ "type": "terms_of_use", "url": "https://www.testshop.com/legal/terms-of-use" },
            { "type": "return_policy", "url": "https://www.testshop.com/legal/return-policy" }]
}
```

The example keys `selected_fulfillment_options[].item_ids` with **item** IDs (`item_123`). The schema text says "line item IDs". Accept either.

### 2.5.10 Example — complete request and response (abridged)

```json
POST /checkout_sessions/checkout_session_123/complete
{
  "buyer": { "first_name": "John", "last_name": "Smith", "email": "johnsmith@mail.com", "phone_number": "15552003434" },
  "payment_data": {
    "handler_id": "card_tokenized",
    "instrument": { "type": "card", "credential": { "type": "spt", "token": "spt_123" } },
    "billing_address": { "name": "John Smith", "line_one": "1234 Chat Road", "city": "San Francisco",
                         "state": "CA", "country": "US", "postal_code": "94131" }
  },
  "marketing_consents": [{ "channel": "email", "opted_in": true }, { "channel": "sms", "opted_in": false }]
}
```

```json
200 OK
{ "id": "checkout_session_123", "status": "completed", "...": "full session",
  "order": { "id": "ord_abc123", "checkout_session_id": "checkout_session_123", "order_number": "#1001",
             "client_reference_id": "chatgpt_txn_abc789",
             "permalink_url": "https://www.testshop.com/orders/ord_abc123",
             "confirmation": { "confirmation_number": "CNF-1001", "confirmation_email_sent": true } } }
```

### 2.5.11 3-D Secure flow (since 2025-12-12 / 2026-01-16)

1. The merchant sets `status: "authentication_required"` and returns `authentication_metadata`:
   - `acquirer_details`*:
     - `acquirer_bin`* (≤11 chars)
     - `acquirer_country`* (ISO 3166-1 alpha-2, 2 chars)
     - `acquirer_merchant_id`* (≤35 chars)
     - `merchant_name`* (≤40 chars)
     - `requestor_id` (≤35 chars)
   - `directory_server`* enum `american_express\|mastercard\|visa`.
   - `flow_preference{type* challenge\|frictionless, challenge{type mandated\|preferred}, frictionless{type low_risk}}`.
2. The agent performs 3DS. It may use the merchant's authentication provider via the Delegate Authentication API (§2.12).
3. The agent calls `complete` with `authentication_result`:
   - `outcome`* enum `abandoned, attempt_acknowledged, authenticated, canceled, denied, informational, internal_error, not_supported, processing_error, rejected`.
   - `outcome_details`, which is required for `authenticated`, `informational` and `attempt_acknowledged`:
     - `three_ds_cryptogram`* (28-char base64)
     - `electronic_commerce_indicator`* enum `01, 02, 05, 06, 07`
     - `transaction_id`* (dsTransID)
     - `version`* (e.g. `2.2.0`)
4. If `authentication_result` is missing, return 4XX `{type: invalid_request, code: requires_3ds, param: "$.authentication_result"}`.

---

## 2.6 Idempotency (normative, since 2026-04-17)

*Source: `rfcs/rfc.agentic_checkout.md` §6 (SEP #120). The same rules apply to delegate payment (`rfc.delegate_payment.md` §5).*

| Rule | Detail |
|---|---|
| Key required | On every POST. Missing key → **400** `idempotency_key_required` |
| Scope | Authenticated identity + endpoint path |
| Equivalence | Semantic JSON equality of the **body only**:<br>• key order is ignored<br>• `1.0 == 1`<br>• **array order matters**<br>• `null` ≠ absent<br>JCS (RFC 8785) + SHA-256 fingerprint is suggested (informative). Extension fields participate |
| Replay | Same key + same body → the **original status and body**, plus `Idempotent-Replayed: true`. **MUST NOT** re-run side effects (capture, inventory) |
| Conflict | Same key + different body → **422** `idempotency_conflict`. Not retryable |
| In flight | **409** `idempotency_in_flight` + `Retry-After` |
| 5xx | **MUST NOT** be cached. A retry is processed fresh |
| Retention | ≥ **24 h** |
| Implementation | Middleware. Store the key and the business result in the same ACID transaction. Use recovery points around PSP calls |

The OpenAI production checklist says to "return `idempotency_conflict` with HTTP 409". The 2026-04-17 spec says **422**. This is **inconsistent**. Follow the spec, but it is worth confirming with OpenAI.

---

## 2.7 Capabilities, payment handlers and extensions

*Source: `rfcs/rfc.capability_negotiation.md`, `rfcs/rfc.payment_handlers.md`, `rfcs/rfc.extensions.md`, `rfcs/rfc.seller_backed_payment_handler.md`, `$defs.Capabilities`.*

### 2.7.1 `Capabilities` (request = agent, response = seller)

| Field | Type |
|---|---|
| `payment` | `Payment {handlers*: PaymentHandler[]}`. Seller response only |
| `interventions` | `InterventionCapabilities` (below) |
| `extensions` | oneOf `string[]` (request, e.g. `["discount","affiliate_attribution"]`) **or** `ExtensionDeclaration[]` (response). Both are `uniqueItems` |

`InterventionCapabilities` fields:

- `supported`: enum array `3ds\|biometric\|address_verification`. The seller returns the **intersection**.
- `required`: enum array `3ds\|biometric`. Seller only.
- `enforcement`: `always\|conditional\|optional`. Seller only. Default conditional.
- `display_context`: `native\|webview\|modal\|redirect`. Agent only.
- `redirect_context`: `in_app\|external_browser\|none`. Agent only.
- `max_redirects`: int ≥0. Agent only.
- `max_interaction_depth`: int ≥1. Agent only.

Mismatch: if a required intervention is not in the intersection and `enforcement: always`, return `status: not_ready_for_payment` plus a `MessageError` with `code: "intervention_required"`.

`ExtensionDeclaration` fields:

- `name`*: pattern `^[a-z][a-z0-9_-]*(@YYYY-MM-DD)?$` or a reverse-DNS form, e.g. `discount` or `dev.acp.discount@2026-01-27`.
- `extends`: JSONPath strings such as `$.CheckoutSession.discounts`.
- `schema`: uri.
- `spec`: uri.

Rules: extensions **MUST NOT** change required fields or semantics. They **MAY** add optional fields and message codes.

### 2.7.2 `PaymentHandler` (seller-declared, `capabilities.payment.handlers[]`)

| Field | Type |
|---|---|
| `id`* | Seller-defined instance ID, referenced by `payment_data.handler_id` |
| `name`* | Reverse-DNS spec name: `dev.acp.tokenized.card`, `dev.acp.seller_backed.{saved_card\|gift_card\|points\|store_credit}` |
| `version`* | `YYYY-MM-DD` |
| `spec`* | uri |
| `requires_delegate_payment`* | bool |
| `requires_pci_compliance`* | bool |
| `psp`* | string. `stripe`, `adyen`, … or `seller_managed` |
| `config_schema`* | uri |
| `instrument_schemas`* | uri[] |
| `config`* | object. **`merchant_id` is REQUIRED** for handlers that use delegate_payment. The agent copies it into `allowance.merchant_id` |
| `display_name` | string, e.g. "Credit Card" |
| `display_order` | int. Lower = preferred. Suggestive only |

Credential schemas (`rfc.payment_handlers.md` §10.3):

- **SPT:** `{type: "spt", token: "^vt_[a-zA-Z0-9]+$", allowance?}`.
- **Agentic token:** `{type: "agentic_token", token (13–19), network visa\|mastercard, last4, exp_month, exp_year}`.

The `acp.dev/...` handler and schema URLs in examples are illustrative. Whether they resolve is **unverified**.

Before 2026-01-30 this was `payment_provider {provider: stripe|adyen|braintree, supported_payment_methods: ["card"], merchant_id}`.

---

## 2.8 Delegated Payment API

*Source: `spec/2026-04-17/openapi/openapi.delegate_payment.yaml`, `spec/2026-04-17/json-schema/schema.delegate_payment.json`, `rfcs/rfc.delegate_payment.md`, developers.openai.com/commerce/specs/payment.md.*

**Who hosts it:** the **PSP or vault**. The call flows agent (OpenAI) → PSP.

- OpenAI: direct integration is for PSPs or **PCI DSS Level 1 merchants with their own vault**. Everyone else uses a compatible PSP. Stripe's **Shared Payment Token** is "the first compatible implementation".
- A normal merchant **does not implement** this endpoint. It only receives the resulting token in `complete`.

`POST /agentic_commerce/delegate_payment` takes the same headers as checkout. `Idempotency-Key` is required. The RFC wants `Signature` + `Timestamp` with a detached signature over canonical JSON (algorithm out of band, e.g. Ed25519/ES256).

### 2.8.1 Request `DelegatePaymentRequest`

| Field | Type |
|---|---|
| `payment_method`* | `PaymentMethodCard` (below) |
| `allowance`* | `Allowance` (below) |
| `risk_signals`* | `RiskSignal[] {type* "card_testing", score* int, action* blocked\|manual_review\|authorized}`. May be empty since 2026-04-17 |
| `metadata`* | map<string,string> |
| `billing_address` | `Address`. Delegate-payment flavor: `name`*≤256, `line_one`*≤60, `line_two`≤60, `city`*≤60, `state`*, `country`* 2 chars, `postal_code`*≤20 |

`PaymentMethodCard`:

- `type`* `"card"`.
- `card_number_type`* `fpan\|network_token`.
- `number`*.
- `display_card_funding_type`* `credit\|debit\|prepaid`.
- `metadata`* map.
- Optional: `exp_month`≤2, `exp_year`≤4, `name`, `cvc`≤4, `cryptogram`, `eci_value`≤2, `checks_performed`[`avs\|cvv\|ani\|auth0`], `iin`≤8, `display_wallet_type`, `display_brand`, `display_last4` (4 digits).
- `virtual` bool: the RFC says required, the schema says optional.

`Allowance`:

- `reason`* `"one_time"`.
- `max_amount`* int (minor units).
- `currency`* `^[a-z]{3}$`.
- `checkout_session_id`*.
- `merchant_id`* ≤256.
- `expires_at`* date-time.

### 2.8.2 Response — 201

```json
{ "id": "vt_01J8Z3WXYZ9ABC", "created": "2025-09-29T11:00:00Z",
  "metadata": { "source": "agent_checkout", "merchant_id": "acme", "idempotency_key": "550e8400-…" } }
```

### 2.8.3 Errors

The error body is flat: `type`* enum `invalid_request, rate_limit_exceeded, processing_error, service_unavailable`, plus `code`*.

`code` values: `invalid_card, duplicate_request, idempotency_conflict, too_many_requests, idempotency_key_required, idempotency_in_flight`.

HTTP statuses: 400/422 validation, 409, 429, 500/503.

The token **MUST** be usable only within the allowance and invalid at or after `expires_at`.

---

## 2.9 Stripe Shared Payment Tokens — how the merchant charges

*Source: docs.stripe.com/agentic-commerce/concepts/shared-payment-tokens.md?agent-seller=seller (read 2026-11-02). The pages carry `Stripe-Version: 2026-09-30.preview` and Seller Services **preview** terms.*

- **Flow:**
  1. The agent issues the SPT to the seller's **Stripe profile** (network business profile) with `usage_limits` (`currency`, `max_amount`, `expires_at`, optional `recurring_interval`).
  2. The agent sends `spt_…` in `payment_data.instrument.credential.token`.
  3. The seller creates a PaymentIntent.
- **Availability:** US, CA, and 31 European countries.
- **Charge with Stripe:**

  ```bash
  curl https://api.stripe.com/v1/payment_intents \
    -u "sk_…:" -H "Stripe-Version: 2026-09-30.preview" \
    -d amount=1000 -d currency=usd \
    -d "payment_method_data[shared_payment_granted_token]=spt_123" \
    -d confirm=true          # add off_session=true for recurring SPTs (requires usage_limits.recurring_interval)
  ```

  - Stripe clones the customer's PaymentMethod onto your account. Refunds and reporting then work as normal.
  - `setup_future_usage` is **rejected** with SPTs.
- **Inspect:** `GET /v1/shared_payment/granted_tokens/{spt}` returns `object: shared_payment.granted_token`, `usage_limits`, `payment_method_details.card.brand`/`last4`, `deactivated_at`, `deactivated_reason`.
- **Third-party processor:** `POST /v1/shared_payment/granted_tokens/{spt}/resolve` returns network credentials of type `fpan\|dpan\|agentic_token`. They may be RSA-encrypted.
  - **One-time only.**
  - Requires a credential configuration and a PCI AOC.
  - Afterwards, record the payment with `POST /v1/payment_records/report_payment`.
- **Sandbox token:** `POST /v1/test_helpers/shared_payment/granted_tokens` with `payment_method=pm_card_visa`, `usage_limits[currency]=usd`, `usage_limits[max_amount]=1000`, `usage_limits[expires_at]=<unix>`.
- **Webhook:** `shared_payment.granted_token.deactivated` (consumed, resolved, expired or revoked). Search snippets also mention `…granted_token.used`, which is unverified.
- **Token prefix mismatch:**
  - ACP repo examples: `spt_123` in `payment_data` and `vt_…` from delegate_payment. The SPT credential schema pattern is `^vt_`.
  - Stripe: `spt_…`.
  - The merchant should treat the token as opaque and route on `handler.psp`.

---

## 2.10 Order webhooks (merchant → agent)

*Source: `spec/2026-04-17/openapi/openapi.agentic_checkout_webhook.yaml`, `rfcs/rfc.orders.md` §6.*

- **Endpoint (OpenAI-hosted):** `POST /agentic_checkout/webhooks/order_events`. The server example is `https://openai.example.com`. The actual URL and secret are provided at onboarding (**unverified**).
- **Request headers:**

  | Header | Required? | Format |
  |---|---|---|
  | `Merchant-Signature` | Required | pattern `^t=\d+,v1=[a-fA-F0-9]{64}$` |
  | `Content-Type: application/json` | Required | |
  | `Request-Id` | Optional | |
  | `Timestamp` | Optional | date-time |

- **Signature algorithm:** Stripe-style.

  ```
  signed_payload = "<t>" + "." + <raw request body bytes>
  v1 = lowercase_hex( HMAC_SHA256(key = shared_secret, msg = signed_payload) )
  header = "t=<unix_seconds>,v1=<v1>"
  ```

  - The receiver rejects with **401** (`type: invalid_request`, `code: invalid_signature`) if the header is missing or malformed, if `t` is outside the window (recommended ±**300 s**), or if the HMAC does not match.
  - The secret is a shared secret "provided by OpenAI" (OpenAI checkout page). How it is distributed and rotated is **unspecified**.
  - OpenAI's page names the header `Merchant_Name-Signature`, which appears to be a placeholder. Use `Merchant-Signature` per the 2026-04-17 spec.
- **Body `WebhookEvent`** (`additionalProperties: false`):
  - `type`*: an open string. Defined values are **`order_create`** and **`order_update`**.
  - `data`*: `EventDataOrder`, which is `allOf[Order]` with required `type`, `checkout_session_id`, `permalink_url` and `status`.

  The `data` **MUST be the full Order**, not a delta. Include `line_items`, `fulfillments`, `adjustments` and `totals` when available.

  `refunds[]` was **removed** in 2026-04-17 in favor of `adjustments[]`. OpenAI's docs still show `refunds[] {type: store_credit|original_payment, amount}`.
- **Event naming is inconsistent:**
  - Repo: `order_create` and `order_update`.
  - OpenAI docs and certification list: `order_created` and `order_updated`, and elsewhere `order.created` and `ORDER_UPDATE`.
  - Make the event name configurable per agent (**unverified** which one ChatGPT expects).
- **Response:** `200 {received*: bool, request_id?}`. Also 400, 401, 429, 500.

```json
{ "type": "order_update",
  "data": { "type": "order", "id": "ord_123", "checkout_session_id": "checkout_session_123",
    "permalink_url": "https://example.com/orders/123", "status": "shipped",
    "line_items": [{ "id": "li_shoes", "title": "Running Shoes",
        "quantity": { "ordered": 1, "current": 1, "fulfilled": 1 }, "unit_price": 9900, "subtotal": 9900, "status": "fulfilled" }],
    "fulfillments": [{ "id": "ful_1", "type": "shipping", "status": "shipped",
        "line_items": [{ "id": "li_shoes", "quantity": 1 }], "carrier": "USPS",
        "tracking_number": "9400111899223456789012",
        "events": [{ "id": "evt_1", "type": "shipped", "occurred_at": "2026-02-05T10:00:00Z" }] }],
    "adjustments": [],
    "totals": [ { "type": "subtotal", "display_text": "Subtotal", "amount": 9900 },
                { "type": "total", "display_text": "Total", "amount": 11260 } ] } }
```

Status derivation hints (`rfc.orders.md` §7.1):

- **Line item:** `current==0` → `removed`. `fulfilled==current` → `fulfilled`. `0<fulfilled<current` → `partial`. Otherwise `processing`.
- **Order:** created → manual_review → … → `shipped` (all fulfilled) → `completed`/`delivered` (all delivered) → `canceled` (all removed).

---

## 2.11 Discovery and MCP binding (both new in 2026-04-17)

### 2.11.1 `GET /.well-known/acp.json`

*Source: `rfcs/rfc.discovery.md`, `$defs.DiscoveryResponse`.*

- **Hosting:** public, no auth, served at the origin root (RFC 8615). `Cache-Control: public, max-age=3600` **SHOULD** be sent.
- **Responses:** 200, 404 (= no ACP. Do not retry), 429/503 + `Retry-After`.

| Field | Type |
|---|---|
| `protocol`* | `{name*: "acp", version*, supported_versions*[] (oldest first), documentation_url}` |
| `api_base_url`* | uri |
| `transports`* | `["rest"]` or `["rest","mcp"]` |
| `capabilities`* | `{services*[] enum checkout\|orders\|delegate_payment\|carts, extensions[] {name*, spec, schema}, intervention_types[], supported_currencies[] (^[a-z]{3}$), supported_locales[] (BCP 47)}` |

The product-feeds RFC proposes adding `"feeds"` to `services`. That value is not in the 2026-04-17 enum.

```json
{ "protocol": { "name": "acp", "version": "2026-04-17",
    "supported_versions": ["2026-01-30","2026-04-17"], "documentation_url": "https://merchant.example.com/acp" },
  "api_base_url": "https://merchant.example.com/acp",
  "transports": ["rest"],
  "capabilities": { "services": ["checkout","orders"],
    "extensions": [{ "name": "discount" }], "intervention_types": ["3ds"],
    "supported_currencies": ["usd"], "supported_locales": ["en-US"] } }
```

### 2.11.2 MCP transport

*Source: `docs/mcp-binding.md`, `spec/2026-04-17/openrpc/openrpc.agentic_checkout.json`.*

- **Transport:** JSON-RPC 2.0 over MCP **Streamable HTTP** on a separate endpoint (e.g. `/mcp`). Advertised by `"mcp"` in `transports`.
- **Tools:** `create_checkout_session`, `get_checkout_session`, `update_checkout_session`, `complete_checkout_session`, `cancel_checkout_session`. They are tools, not resources.
- **Arguments:** `{ meta, id?, payload? }`.
  - `meta` carries the headers: `api_version` (required), `idempotency_key`, `request_id`, `user_agent`, `accept_language`, `signature`, `timestamp`. It is `additionalProperties: true`.
  - `payload` uses exactly the REST request schemas.
  - **Authorization is not in `meta`.** It is handled at the MCP connection level (server config or OAuth).
- **Results and errors:**
  - Success: `result` = the REST body.
  - Error: `error.code = -32000` with the ACP `Error` in `error.data`. `-32602` is only for malformed JSON-RPC params.
  - 401 and 403 map to `-32000` with `data.type: invalid_request`.
- **Codegen note:** the OpenRPC file uses relative `$ref`s into the JSON Schema bundle. Servers **MUST** bundle and resolve them before serving tool schemas.

---

## 2.12 Other 2026 additions (with the version that introduced each)

*Source: `changelog/*.md` and the rfcs.*

| Feature | Introduced | Merchant impact | Source |
|---|---|---|---|
| `fulfillment_details` (nested), `selected_fulfillment_options[]`, optional option subtotal/tax, `return_policy` link, line-item display fields | **2025-12-12** (breaking) | Core | `changelog/2025-12-12.md` |
| Intent traces (`intent_trace` on cancel) | **2025-12-12** | Accept and store. Write-only | `rfcs/rfc.intent_traces.md` |
| Affiliate attribution (first/last touch) | **2025-12-12** | Store with the order | `rfcs/rfc.affiliate_attribution.md` |
| 3DS: `authentication_required`, `authentication_metadata`/`_result` | **2025-12-12** (schema) / **2026-01-16** (`authentication_provider`, `merchant_id` on `PaymentProvider`, `adyen`) | Optional | `changelog/2026-01-16.md` |
| Capability negotiation (`capabilities` required on create and response) | **2026-01-30** | **Required** | `rfcs/rfc.capability_negotiation.md` |
| Payment handlers (replaces `payment_provider`; new `payment_data` shape) | **2026-01-30** (breaking) | **Required** | `rfcs/rfc.payment_handlers.md` |
| Extensions framework (`capabilities.extensions`) | **2026-01-30** | Optional | `rfcs/rfc.extensions.md`, `spec/*/json-schema/schema.extension.json` |
| Discount extension (`discounts.codes/applied/rejected`, `Coupon`, allocations) | **2026-01-30** | Optional | `rfcs/rfc.discount_extension.md`, `schema.discount.json`, `examples/2026-04-17/discount-extension/` |
| Rich session fields (B2B buyer, pickup/local delivery, fulfillment groups, quotes, multi-currency, `line_items` + `currency` on create, `LineItem.totals[]`) | **2026-01-30** | Core | schema diff |
| Seller-backed handlers (`dev.acp.seller_backed.*`), `display_order`, `display_name` | **2026-04-17** | Optional (saved cards, gift cards, points, store credit) | `rfcs/rfc.seller_backed_payment_handler.md` |
| Mandatory idempotency (400/409/422, `Idempotent-Replayed`) | **2026-04-17** | **Required** | RFC §6 |
| `supported_versions` in version errors | **2026-04-17** | Recommended | |
| Native orders (`line_items`, `fulfillments`, `adjustments`, `totals`; `amount_refunded`) + webhook = full Order | **2026-04-17** | **Required** for webhooks | `rfcs/rfc.orders.md` |
| Webhook signing format `t=,v1=` HMAC-SHA256 + 300 s window | **2026-04-17** | **Required** | webhook OAS |
| Discovery `/.well-known/acp.json` | **2026-04-17** | Recommended | `rfcs/rfc.discovery.md` |
| MCP transport binding (OpenRPC) | **2026-04-17** | Optional | `docs/mcp-binding.md` |
| Cart API: `POST /carts` 201, `GET /carts/{id}` 200/404, `PUT /carts/{id}` (full replace) 200, `POST /carts/{id}/cancel` 200 | **2026-04-17** | Optional. The cart has no status, no payment and no negotiation. `Cart{id*, line_items*, currency*, totals*, buyer, messages, continue_url, expires_at}`. Cart → checkout via `cart_id` is deferred | `rfcs/rfc.cart.md`, `openapi.cart.yaml` |
| Delegate Authentication (3DS2 browser): `POST /delegate_authentication` 201, `POST /{id}/authenticate` 200, `GET /{id}` 200 | **2026-04-17** | Hosted by the authentication provider. The merchant only designates it | `rfcs/rfc.delegate_authentication.md` |
| Feed API (agent-hosted; push) | **2026-04-17** | Merchant is the **client** | `rfcs/rfc.product_feeds.md`, `openapi.feed.yaml` |
| Marketing consent | **2026-04-17** | Optional | `rfcs/rfc.marketing_consent.md` |
| Markdown = CommonMark 0.31.2, no raw HTML | **2026-04-17** | Validate | |
| `Message.resolution` | **2026-04-17** | Optional | |
| `order_notes`, `Address.company`, `Order.client_reference_id` | **2026-04-17** | Optional | |
| Decimal quantity (B2B) | Changelog 2026-04-17, but applied only to the `2025-09-29` folder | See defect §2.4.1 | |

---

## 2.13 Product feeds

There are three distinct specs. Do not conflate them.

### 2.13.1 ACP Feed API (open spec, 2026-04-17)

*Source: `rfcs/rfc.product_feeds.md`, `spec/2026-04-17/openapi/openapi.feed.yaml`, `schema.feed.json`.*

- **Hosting:** **agent-hosted**. The merchant pushes. Agents **MUST NOT** call feed endpoints on merchants.
- **Conventions:** HTTPS, Bearer auth, `API-Version`, idempotency SHOULD, flat errors (e.g. `code: feed_not_found`).

| Op | Path | Success |
|---|---|---|
| Create feed | `POST /feeds` `{target_country?}` | 201 `FeedMetadata {id*, target_country (^[A-Z]{2}$), updated_at}` |
| Get feed | `GET /feeds/{id}` | 200 |
| Get products | `GET /feeds/{id}/products` | 200 `{products*: Product[]}` |
| Upsert | `PATCH /feeds/{id}/products` `{products*: Product[]}` | 200 `UpsertProductsResponse {id*, accepted*}`. *(unreleased)* adds `feed_update_id` |

Upsert is by `Product.id`. Omitted products stay unchanged. Send complete product objects. Deletion via PATCH is deferred: mark the variant unavailable or discontinued, or send a full file.

**File mode:** `metadata.json` (FeedMetadata) + `products.jsonl` (one Product per line) = **full replacement**.

Schema:

- **`Product`:** `id`*, `variants`*[], `title`, `description`, `url`, `media[]`. *(unreleased)* adds `last_update_id`.
- **`Description`:** `{plain, html, markdown}`.
- **`Variant`:**
  - `id`* (SHOULD be stable and accepted as the checkout `Item.id`), `title`*, `description`, `url`.
  - `barcodes[] {type*, value*}`.
  - `price`, `list_price`: `{amount* int ≥0, currency* ^[A-Z]{3}$}`. Uppercase here, unlike checkout's lowercase.
  - `unit_price {amount*, currency*, measure*{value* number, unit*}, reference*{value* int, unit*}}`.
  - `availability {available bool, status: in_stock\|limited_stock\|backorder\|preorder\|out_of_stock\|discontinued (open)}`.
  - `categories[] {value*, taxonomy}`, `condition` string[], `variant_options[] {name*, value*}`.
  - `media[] {type*, url*, alt_text, width, height}`. The first entry is primary.
  - `seller {name, links[] {type*, title, url*}}`, `marketplace` (Seller).

Feed prices are informational. **Checkout is authoritative.** Return a `price_change` warning when it differs.

### 2.13.2 OpenAI ChatGPT product feed — file upload (current OpenAI docs)

*Source: developers.openai.com/commerce/specs/file-upload/overview.md and …/products.md, read 2026-11-02.*

**Delivery:**

- **SFTP push.** The SFTP root is the entire catalog. No completion marker.
- **Formats:** **parquet** preferred (zstd), or `jsonl.gz`, `csv.gz`, `tsv.gz`. UTF-8.
- **Sharding:** ≤500k items and ~500 MB per shard (recommended). Keep stable file names and overwrite them. Shard deterministically by `item_id`.
- **Cadence:** a **full snapshot at least daily**. No incremental files.
- **Removal:** a missing item is kept for up to **14 days**. Set `is_eligible_search=false` to remove it sooner.
- **Access:** approved partners only. Apply at `chatgpt.com/merchants`.

**Required fields:**

| Field | Type / format |
|---|---|
| `item_id` (aliases `id`, `sku`) | Stable and unique. Never reuse |
| `title` | ≤150 chars recommended |
| `description` | Plain text, ≤5000 chars recommended |
| `url` | Public PDP |
| `brand` | |
| `seller_name` | |
| `image_url` | |
| `availability` | enum `in_stock\|out_of_stock\|pre_order\|backorder\|unknown`. An invalid value **rejects the row** |
| `price` | String `"79.99 USD"`: decimal, ISO 4217 uppercase, no thousands separators |

**Eligibility flags:**

| Field | Behavior |
|---|---|
| `is_eligible_search` (alias `enable_search`) | Default `true`. `false` also disables checkout |
| `is_eligible_checkout` (alias `enable_checkout`) | Opt-in. Effective only if search is true and checkout is enabled for the integration |
| `is_ads_eligible` (alias `is_eligible_ads`) | |

**Conditionally required:**

- For variants: `group_id` (alias `item_group_id`) ≠ `item_id`, `listing_has_variations=true`, `variant_dict` (e.g. `{"color":"Black","size":"10"}`).
- For checkout: `seller_privacy_policy`, `seller_tos` (URLs).
- For third-party marketplace offers: `marketplace_seller`.
- With separate dimension fields: `dimensions_unit`. With `weight`: `item_weight_unit` (`g, kg, oz, lb`).

**Optional fields:**

| Group | Fields |
|---|---|
| Identifiers | `offer_id`, `gtin` (8/12/13/14 digits, valid check digit), `mpn` |
| Attributes | `condition` (`new\|refurbished\|used`), `product_category` (`A > B > C`), `material`, `color`, `size`, `size_system` (`US, UK, EU, DE, FR, JP, CN, IT, BR, MEX, AU`), `gender` (`male\|female\|unisex`), `age_group` (`newborn\|infant\|toddler\|kids\|adult`) |
| Dimensions and weight | `dimensions {length,width,height,unit in\|cm\|ft\|m\|mm}` or `length`/`width`/`height` + `dimensions_unit`, `weight` |
| Media | `additional_image_urls` |
| Pricing | `sale_price` (< price, same currency) |
| Seller | `seller_url` |
| Shipping | `shipping_price` (money, ≥0) or `shipping` (`country:region:service_class:price`, setup required) |
| Returns | `accepts_returns`, `return_deadline_in_days` (alias `return_window`), `return_policy` (URL), `accepts_exchanges` |
| Reviews | `review_count`, `star_rating` (0–5, 2 decimals), `store_review_count`, `store_star_rating` |
| Digital and markets | `is_digital`, `target_countries` (currently `US, CA, MX`), `store_country` |
| Ads | `ads_metadata` |
| Legacy (still accepted) | `Custom_variant{1..3}_category` / `_option` |

**Data rules:**

- Empty or `null` means no value. Don't use placeholders.
- Booleans are JSON or lowercase strings.
- CSV: quote cells and serialize objects as JSON strings.

**Fields from the original 2025 OpenAI feed spec** are **not in the current page**, and their status is **unverified**. Treat them as legacy aliases and drop them:

- `inventory_quantity`, `link`, `image_link`, `additional_image_link`
- `popularity_score`, `return_rate`
- `q_and_a`, `raw_review_data`, `related_product_id`
- `seller_url` duplicates

### 2.13.3 OpenAI feed/promotions REST API (agent-hosted)

*Source: developers.openai.com/commerce/specs/api/{feeds,products,promotions}.md.*

- **Paths differ from the ACP spec** (`/product_feeds`, not `/feeds`):

  | Op | Path | Notes |
  |---|---|---|
  | Create feed | `POST /product_feeds` | **200**, not 201 |
  | Get feed | `GET /product_feeds/{id}` | |
  | Get products | `GET /product_feeds/{id}/products` | |
  | Get promotions | `GET /product_feeds/{id}/promotions` | |
  | Upsert promotions | `PATCH /product_feeds/{id}/promotions` | Body `Promotion[]`. Returns `{id, accepted}` |

  OpenAI's docs say product data is updated **via SFTP, not this API**. There is no products PATCH in OpenAI's API.
- **`Promotion`:**
  - `id`*, `title`*, `description`, `url`.
  - `status`: `draft\|scheduled\|active\|expired\|disabled`.
  - `active_period`* `{start_time*, end_time*}`.
  - `benefits`*[] of `amount_off{amount_off: Price}`, `percent_off{percent_off: number}` or `free_shipping`.
  - `applies_to`[] `{product_id*, variant_ids[]}`.
- **Base host and API-Version:** not documented. Example `API-Version: 2025-09-12` (**unverified**).

---

## 2.14 Merchant onboarding and certification with OpenAI

*Source: developers.openai.com/commerce/guides/{get-started,production,key-concepts}.md, read 2026-11-02.*

- **Apply:** `https://chatgpt.com/merchants`. Feed onboarding is "currently available to approved partners".
- **Steps:** review the file spec → validate required fields → upload over SFTP → keep a daily snapshot. Promotions go through the Promotions API.
- **Prohibited:** adult content, alcohol, nicotine, gambling, weapons, Rx drugs, unlicensed financial products, and more.
- **Roles:** the merchant is the **merchant of record**. It validates, accepts or declines orders, calculates and collects tax, runs risk checks, and charges through its own PSP. OpenAI is not the merchant of record.

**Production launch certification checklist** (run in sandbox and share request/response logs):

1. Create a session with and without an address. Shipping options and tax appear once the address is valid. `API-Version` is present and supported.
2. Changing the shipping option recomputes totals.
3. Delegated payment call with all fields, canonical JSON and a detached signature. Applies to PSP or vault integrators.
4. Complete returns `completed` + `order`. The checklist says "201", but the spec says 200.
5. `order_created` and `order_updated` webhooks with a valid HMAC.
6. Errors: `missing` (400 `invalid_request`), `out_of_stock`, `payment_declined`.
7. Idempotency: replay returns the same result. A different body returns `idempotency_conflict`. The checklist says 409, but the spec says 422.
8. Working ToS and Privacy links.
9. **Allowlist OpenAI's published IP ranges.**

**Security:**

- TLS ≥1.2 on port 443.
- The checkout and feed specs carry no cardholder data, so they are out of PCI scope. Using the PSP's delegated payment (e.g. Stripe SPT) avoids PCI scope changes.
- OpenAI "may require" a PCI **AOC** before production.

**Commercial terms:** OpenAI takes a commission per completed purchase (press at launch in 2025-09; **unverified** current terms). Instant Checkout's 2026 status is in §2.0.

**There is no published automated conformance test suite.** None was found in the repo or the docs. The repo has only `scripts/validate-consistency.js`, which validates the schemas themselves.

---

## 2.15 `unreleased` vs 2026-04-17 (as of 2026-07-17)

*Source: `changelog/unreleased/*.md` and the schema diff.*

| Area | Unreleased change |
|---|---|
| `Item` | `suggested_price {amount*, source*{feed_id*, channel, feed_update_id, updated_at}, observed_at*}` (SEP #197). `url` (SEP #280) |
| Complete request | Optional `fulfillment_details`. Contact-only changes are allowed. Per the changelog, address changes → **409** (SEP #196) |
| Orders (breaking, also back-applied to `spec/*`) | Quantity `{ordered, current*, fulfilled}`. Line status `processing\|partial\|fulfilled\|removed`. Order `delivered` → **`completed`**. Event `returned` → `returned_to_sender`, plus `canceled` and `undeliverable`. Adjustment types `refund\|credit\|return\|exchange\|price_adjustment\|dispute`. All order enums are **open** |
| Feed | `UpsertProductsResponse.feed_update_id`. `Product.last_update_id` (SEP #259) |
| Docs and examples | Delegate-payment error examples unwrapped to the flat shape. Message examples fixed. `Address.company` description fixed. Intent-trace examples added |
| Governance | Meta joined the TSC |

---

## 2.16 Machine-readable artifacts for C# codegen

**There are no official SDKs or reference server implementations** in the repo. None are listed on agenticcommerce.dev, which offers only docs and `/llms.txt`.

- The README calls OpenAI's and Stripe's implementations the "reference implementations".
- Stripe's suite is hosted, not a library.

Generate types from these files:

| File (`spec/2026-04-17/…`) | Contents |
|---|---|
| `json-schema/schema.agentic_checkout.json` | Draft 2020-12, `$defs` bundle. **Most complete**: has `Discovery*`, `order_notes`, `client_reference_id`, `Address.company` |
| `openapi/openapi.agentic_checkout.yaml` | OAS 3.1. Paths, headers, responses |
| `openapi/openapi.agentic_checkout_webhook.yaml` | Webhook receiver. Cross-file `$ref` to `./openapi.agentic_checkout.yaml#/components/schemas/Order` |
| `json-schema/schema.delegate_payment.json`, `openapi/openapi.delegate_payment.yaml` | Delegate payment |
| `json-schema/schema.cart.json`, `openapi/openapi.cart.yaml` | Cross-file `$ref` `schema.agentic_checkout.json#/$defs/...` |
| `json-schema/schema.feed.json`, `openapi/openapi.feed.yaml` | Feed API |
| `json-schema/schema.discount.json`, `json-schema/schema.extension.json` | Discount and extension schemas |
| `json-schema/schema.delegate_authentication.json`, `openapi/openapi.delegate_authentication.yaml` | 3DS2 |
| `openrpc/openrpc.agentic_checkout.json` | MCP tools |
| `examples/2026-04-17/*.json` | Fixtures for tests: checkout, multi-item, MCP, delegate payment, delegate authentication, feed, orders, discount |

Codegen cautions:

- OAS 3.1 needs NSwag ≥14 or Kiota. `oneOf` arrays of messages and fulfillment options need a `type` discriminator converter.
- `Capabilities.extensions` is `oneOf[string[] | object[]]`.
- `additionalProperties: false` everywhere: use lenient deserialization.

**Known spec defects to code around:**

1. `Item` has no `quantity` (§2.4.1).
2. The OpenAPI `PaymentData` example uses the legacy shape.
3. `Signature` is described as "HMAC… webhook".
4. JSON Schema and OpenAPI drift: `Order.client_reference_id` and `Address.company` are in the JSON Schema only.
5. `selected_fulfillment_options.item_ids` uses item IDs in examples but says line item IDs.
6. Error-code status for an idempotency conflict: OpenAI says 409, the spec says 422.
7. Webhook event names: `order_create` vs `order_created`.
8. Delegate-payment `virtual`: required in the RFC, optional in the schema.
9. Token prefixes: `vt_` vs `spt_`.
10. Lowercase currency in checkout vs uppercase in feeds.

---

## 2.17 Sources

**Repo:**

- `README.md`
- `changelog/2025-09-29.md` … `2026-04-17.md`, `changelog/unreleased/*.md`
- `rfcs/rfc.{agentic_checkout, delegate_payment, payment_handlers, capability_negotiation, extensions, discount_extension, discovery, orders, cart, product_feeds, intent_traces, delegate_authentication, seller_backed_payment_handler, marketing_consent, affiliate_attribution}.md`
- `docs/mcp-binding.md`
- `spec/2026-04-17/**`, `spec/unreleased/**`, `examples/2026-04-17/**`
- GitHub API: no releases or tags. Latest commit 2026-07-18Z.

**OpenAI:**

- developers.openai.com/commerce
- …/guides/get-started.md, …/guides/production.md, …/guides/key-concepts.md
- …/specs/checkout.md, …/specs/payment.md
- …/specs/file-upload/overview.md, …/specs/file-upload/products.md
- …/specs/api/overview.md, …/specs/api/feeds.md, …/specs/api/products.md, …/specs/api/promotions.md

**Stripe:**

- docs.stripe.com/agentic-commerce
- …/concepts/shared-payment-tokens.md?agent-seller=seller
- …/sellers/use-cases/retail.md

**Other:**

- agenticcommerce.dev (home page)
- digitalcommerce360.com/2026/03/06/openai-shifts-checkout-plans-agentic-commerce-strategy/ (trade press, secondary)

---

# 3. AP2 — Agent Payments Protocol

> **Sources read (primary):**
>
> - `google-agentic-commerce/AP2` repo, `main` @ `e1ea56d` (2026-04-29), one commit after tag `v0.2.0` (`b4587ac`, 2026-04-28). Spec site source is in `docs/` (rendered at https://ap2-protocol.org/). Also `git show` of tag `v0.1.0` (`bb57b6b`, 2025-09-16) and the last commit before v0.2 (`cc8a968`, 2026-04-24) for the v0.1 material.
> - GitHub releases (`gh release list`, 2026-11-02): **v0.2.0 is "Latest" (2026-04-28)**. Before that there was only v0.1.0 (2025-09-16). Nothing has been merged to `main` since 2026-04-29. Open PRs up to 2026-11-01 are SDK and sample bug fixes only.
> - https://ap2-protocol.org/ap2/specification/ (fetched 2026-11-02): the title reads "Agentic Payment Protocol (v0.2)", with the same headings as `docs/ap2/specification.md`.
> - UCP repo clone `repos/ucp-2026-08-25` (branch `release/2026-08-25` @ `cfba1e7`, 2026-11-02): `docs/specification/payment/extensions/ap2-mandates.md`, `docs/documentation/ucp-and-ap2.md`, `source/schemas/common/payment_ap2_mandate.json`, `docs/specification/shopping/checkout/a2a.md`.
>
> **Spec version described: AP2 v0.2 (released 2026-04-28).** Anything marked *unverified* is inference or comes from a secondary source.

---

## 3.1 Version, maturity and governance

| Item | Value | Source |
|---|---|---|
| Current version | **v0.2** (`0.2.0`, 2026-04-28, "Release of V2", PR #233) | `CHANGELOG.md`, GitHub releases |
| Previous version | v0.1.0 (2025-09-16), labeled "`v0.1-alpha`" in the old A2A extension doc | `CHANGELOG.md`, `v0.1.0:docs/a2a-extension.md` |
| Governance | Google donated AP2 to the **FIDO Alliance** with the v0.2 release. Standardization continues in FIDO's *Agentic Authentication* and *Payments* Technical Working Groups. | `docs/index.md`, `docs/faq.md` ("What's next") |
| Maturity | Pre-1.0. Mandate types carry a version suffix (`.1`). No PyPI package yet (install from git). The SDK is Python only. There are open bug PRs on constraint math (budget units, negative amounts, execution-date comparison). | `README.md`, PRs #368/#377/#378/#391 |
| Positioning | "AP2 operates as a security feature within a Commerce Protocol". Catalog, checkout and transport are out of scope. It is "designed explicitly to be compatible with the Universal Commerce Protocol (UCP)". | `docs/ap2/specification.md` |

Third-party reports say v0.2 was donated to FIDO on 2026-04-28 alongside Mastercard's "Verifiable Intent" framework. Production adoption claims conflict between sources (*unverified*).

### 3.1.1 What changed from v0.1 to v0.2

The CHANGELOG has a single line per release ("Create AP2" / "Release of V2"). The following comparison comes from diffing the docs and code at `cc8a968` against `main`.

| Area | v0.1 (Sep 2025 – Apr 2026) | v0.2 (Apr 2026) |
|---|---|---|
| Mandate set | **IntentMandate**, **CartMandate**, **PaymentMandate** | **Checkout Mandate** and **Payment Mandate**, each in an **open** (constraints plus agent key) or **closed** (bound to one transaction) state. IntentMandate is gone: its job is done by the *open* mandates. |
| Merchant's cart object | `CartContents` holds a **W3C Payment Request**-shaped `payment_request` (`method_data`, `details.display_items`, `total`, `options`), plus `cart_expiry` and `merchant_name`. The merchant signature is `CartMandate.merchant_authorization`, a JWT with `iss/sub/aud/iat/exp/jti/cart_hash` (documented only; the samples used a **fake JWT** string). | Merchant returns a **merchant-signed compact JWT of the Checkout** (`checkout_jwt`). Its payload is out of AP2's scope. **With UCP it MUST be the UCP Checkout object.** The W3C PaymentRequest model is dropped. |
| User signature | `PaymentMandate.user_authorization`: a "verifiable presentation", e.g. an SD-JWT-VC whose KB-JWT carries `transaction_data` hashes of the CartMandate and PaymentMandateContents. Described in docstrings only. | Formal **SD-JWT VC + Key Binding** model with **Delegate SD-JWT** chains (`~~`-joined), selective disclosure, `cnf` agent keys and `sd_hash` binding. Two delegation trust models: *User Credential* (OpenID4VP `transaction_data` type `delegate`) and *Trusted Agent Provider*. |
| Binding | Cart hash (`cart_hash` in the merchant JWT). Payment mandate referenced the cart. | `checkout_hash = b64url(H(checkout_jwt))` inside the Checkout Mandate. **Payment Mandate `transaction_id` = the same hash.** Open Payment Mandate `payment.reference.conditional_transaction_id` = hash of the open Checkout Mandate. |
| Receipts | PaymentReceipt added late in v0.1 (PR #110), sample-level | Normative **Checkout Receipt** (merchant-signed JWT) and **Payment Receipt** (MPP-signed JWT), each with `reference` = hash of the closed mandate |
| Human-not-present | Roadmap only ("V1.x") | Fully specified via open mandates and constraints |
| Constraints | IntentMandate fields (`merchants`, `skus`, `requires_refundability`, `intent_expiry`) | Typed, extensible constraints: `checkout.allowed_merchants`, `checkout.line_items`, `payment.amount_range`, `payment.budget`, `payment.agent_recurrence`, `payment.allowed_payees`, `payment.allowed_payment_instruments`, `payment.allowed_pisps`, `payment.reference`, `payment.execution_date` |
| A2A extension | `docs/a2a-extension.md`: URI `https://github.com/google-agentic-commerce/ap2/tree/v0.1`, `params.roles`, DataPart keys `ap2.mandates.IntentMandate` / `CartMandate` / `PaymentMandate` | **The A2A extension doc was removed.** Samples use URI `https://github.com/google-agentic-commerce/ap2/v1` (no `params`) and DataPart keys `ap2.mandates.CheckoutMandateSdJwt` / `ap2.mandates.PaymentMandateSdJwt` (see §3.8) |
| Schemas | Pydantic models only (`src/ap2/types/*.py`) | **JSON Schema 2020-12** files in `code/sdk/schemas/ap2/*.json` (canonical, `$id` under `https://ap2-protocol.org/schemas/`), with Pydantic models generated from them |
| Roles | User, Shopping Agent, Credentials Provider, Merchant Endpoint, Merchant Payment Processor, Network/Issuer | Five normative roles: SA, CP, Merchant, MPP, **Trusted Surface** (new, MUST be non-agentic). Network/Issuer remain in the overview. |

Legacy v0.1 types are **still present** on `main` in `code/sdk/python/ap2/models/{mandate,payment_request,cart}.py` and in the **Go sample** (`code/samples/go/pkg/ap2/types/mandate.go`, still CartMandate/IntentMandate). They are not part of the v0.2 spec. Do not build on them.

---

## 3.2 Roles

Source: `docs/ap2/specification.md` §Roles, `docs/overview.md` §3, `docs/glossary.md`, `docs/ap2/implementation_considerations.md`.

| Role | Abbr. | Responsibility | Agentic? |
|---|---|---|---|
| User | — | Human who gives financial authority. Approves Mandate Content on a Trusted Surface. | — |
| Shopping Agent | SA | Discovery, builds the checkout, assembles Mandate Content, gets the mandates signed, presents them to verifiers. Manages double-spend and receipts. | expected agentic |
| Credential Provider | CP | Source of payment credentials (wallet, network, or merchant-stored instruments). **Verifies the Payment Mandate** before releasing a scoped token or credential. Stores the Payment Receipt. | MAY be either |
| Merchant | M | Catalog and checkout. **Signs the Checkout JWT**, **verifies the Checkout Mandate**, completes payment with the MPP, **returns a signed Checkout Receipt**. (Glossary legacy name: "Merchant Endpoint (ME) / Remote Endpoint".) | MAY be either |
| Merchant Payment Processor | MPP | Merchant PSP. Verifies that the payment credential is scoped to *this* checkout (e.g. the closed Payment Mandate is inside the token). Processes payment. **Returns a signed Payment Receipt** to SA, CP and the Network. | MAY be either |
| Trusted Surface | TS | Non-agentic UI that shows Mandate Content and gets user authentication and consent, then produces the signed mandate. Examples: a deterministic part of the SA app, a wallet or issuer app, an OS or browser (Digital Credentials API). | **MUST be non-agentic** |
| Network / Issuer | — | Issues credentials and authorizes transactions. MAY receive the Payment Mandate (e.g. agent-specific tokenization). | — |

- A single entity MAY play several roles. Any role MAY delegate its verification duties, for example a Merchant delegating Checkout Mandate verification to its PSP.
- **All validation MUST run in deterministic code**, even when the role itself is agentic.

---

## 3.3 Cryptographic model (applies to both mandates)

Source: `docs/ap2/agent_authorization.md`, `code/sdk/python/ap2/sdk/README.md`, `code/sdk/python/ap2/sdk/sdjwt/*`.

### 3.3.1 Formats

- **Mandates are SD-JWT VCs** (RFC 9901 SD-JWT) with **Key Binding**, chained per the **Delegate SD-JWT** individual draft (G. Oliver, `draft-gco-oauth-delegate-sd-jwt-00`, https://github.com/GarethCOliver/gco-delegate-sd-jwt). Other VDC formats (ISO mdoc) "COULD" be used but are not specified.
- The **Mandate Content** (the claims) goes inside a `delegate_payload` array, each element selectively disclosable. Claims:
  - `vct` (REQUIRED): mandate type plus version, e.g. `mandate.checkout.1`. Verifiers **MUST match the exact string**.
  - `constraints` (OPTIONAL array of `{type, ...}`). Unknown constraint types **MUST fail** evaluation.
  - `cnf` (RFC 7800). **REQUIRED on open mandates**: the agent's public JWK.
  - Any SD-JWT-VC claim (`iat`, `exp`, `iss`, ...) MAY be used.
- **Chain wire format:** `<root SD-JWT>~<disclosures>~~<KB-SD-JWT hop>~<disclosures>~~ … ~~<closed KB-SD-JWT>~<disclosures>~`

| Token | JOSE header `typ` | Signed by | Notes |
|---|---|---|---|
| Root (open or directly-closed) SD-JWT | spec example: `example+sd-jwt` with a `kid` (placeholder; SD-JWT VC normally uses `dc+sd-jwt`, *unverified* which value the final spec will mandate) | User credential issuer / User's holder key / **Agent Provider key** | `_sd_alg` (default `sha-256`) |
| Intermediate hop | `kb+sd-jwt+kb` | Key in the previous hop's `cnf.jwk` | MUST carry `cnf` |
| **Closed (terminal) hop** | **`kb+sd-jwt`** | Agent key (HNP) or user/TS key | MUST NOT carry `cnf`. Payload: `delegate_payload`, **`iat`, `aud`, `nonce`, `sd_hash`** (or `issuer_jwt_hash`), `_sd_alg` |

- **Algorithms:** every example and the SDK use **ES256 (ECDSA P-256 / SHA-256)**. `types/jwk.json` constrains keys to `kty: EC`, `crv: P-256`, `alg: ES256`. The UCP extension says AP2 v0.2 accepts **ES256 / ES384 / ES512**.
- **Checkout JWT signing rule:** "the Checkout JWT MUST be signed using a digital signature scheme (e.g., ECDSA) and not a deterministic signature (e.g., Ed25519)". This prevents rainbow-table inversion of `checkout_hash`. *Conflict:* `security_and_privacy_considerations.md` instead allows a deterministic scheme if the Checkout carries a high-entropy salt. **AP2 issue #268** (open since 2026-05-26) proposes converging on the entropy rule, so Ed25519 might be allowed later. **For now, sign Checkout JWTs with ES256.**
- **Hashes:** the hash input is the **ASCII compact serialization** (base64url string), not re-serialized JSON. Output is **base64url without padding**. The algorithm is `_sd_alg` of the SD-JWT, else `sha-256`. Store mandates in compact form for disputes (`implementation_considerations.md` §Hashes).

### 3.3.2 Mandate delegation (how a mandate gets signed)

| Model | Who signs | How the verifier trusts it |
|---|---|---|
| **User Credential** | User's wallet, using the private key bound to a VDC (e.g. an EMVCo **Digital Payment Credential**, `vct: com.emvco.dpc`). Issued to the wallet via OpenID4VCI or similar. | Verifier trusts the **credential issuer** (bank or network). The SA sends an **OpenID4VP** request (RECOMMENDED over the W3C Digital Credentials API, `response_mode: dc_api`). `transaction_data[]` holds a base64url JSON object `{type:"delegate", format:"dc+sd-jwt", delegate_payload:[<Mandate Content>...], delegate_disclosures?, credential_ids, transaction_data_hashes_alg}`. The KB-JWT of the presentation covers `delegate_payload`. A second `transaction_data` of `type:"payment_card"` drives the confirmation UI. |
| **Trusted Agent Provider** | Agent Provider's **server-side key**, used only via its Trusted Surface (the agent must not be able to use it) | Verifier keeps a **trust list of Agent Provider keys** |

### 3.3.3 Generic verification algorithm (`agent_authorization.md` §Verification and Processing Rules)

1. Verify the SD-JWT chain per Delegate SD-JWT: root signature against a trusted issuer or agent-provider key, each hop's signature against the previous `cnf.jwk`, `sd_hash`/`issuer_jwt_hash` linkage, disclosure digests, and `aud`/`nonce` on the final hop.
2. Claims present in the open Mandate Content must appear **unchanged** in the closed Mandate Content.
3. Evaluate **every constraint** from each open mandate against the closed content. Unknown types fail.

### 3.3.4 Mandate Receipt (generic)

A Mandate Receipt is a **verifier-signed JWT** that MUST be returned on both accept and reject:

| Claim | Req | Meaning |
|---|---|---|
| `iss` | yes | The verifier |
| `result` (generic) / `status` (concrete schemas: `"Success"` or `"Error"`) | yes | outcome |
| `reference` | yes | b64url hash of the received (final, closed) mandate, "calculated in the same manner as `sd_hash`" using `_sd_alg` or sha-256 |
| `error` | if error | `invalid_credential` (terminal), `unresolved_constraint` (fall back to human-present or non-agentic flow), `invalid_mandate` (terminal), `mandates_not_supported` (fall back) |
| `error_description` | opt | human-readable |

Note the inconsistency between the generic text (`result: success|error`) and the concrete schemas (`status: Success|Error`). Implement the concrete schemas (§3.5, §3.6).

Second inconsistency: the SDK computes `reference = sha256_b64url(<closed leaf JWT>)`, meaning the last `~~` segment up to its first `~`, without disclosures. The spec says "same manner as `sd_hash`", which would include disclosures. **Unverified which one the final spec will mandate. Match the SDK for interop today.**

---

## 3.4 Checkout Mandate

Source: `docs/ap2/checkout_mandate.md`, `code/sdk/schemas/ap2/checkout_mandate.json`, `open_checkout_mandate.json`, `checkout_receipt.json`.

**Flow:** the SA creates the Mandate Content, the Trusted Surface renders it and gets the user's signature (or the agent signs it in HNP), and the **Merchant verifies it**. The Merchant is the original author of `checkout_jwt`.

### 3.4.1 Closed Checkout Mandate: `vct = "mandate.checkout.1"`

`$id: https://ap2-protocol.org/schemas/checkout_mandate.json`

| Field | Type | Req | Notes |
|---|---|---|---|
| `vct` | string const `mandate.checkout.1` | ✔ | |
| `checkout_jwt` | string | ✔ | **Merchant-signed compact JWT of the Checkout.** `x-selectively-disclosable-field: true` |
| `checkout_hash` | string | ✔ | b64url(`H(checkout_jwt)`), H = `_sd_alg` or sha-256 |
| `iat` | integer (epoch) | | |
| `exp` | integer (epoch) | | |

In the wire example, `checkout_jwt` is a separate disclosure (`["salt","checkout_jwt","<jwt>"]`) referenced from the mandate's `_sd` array. That lets the mandate be shown without revealing the checkout contents.

### 3.4.2 Open Checkout Mandate: `vct = "mandate.checkout.open.1"`

| Field | Type | Req | Notes |
|---|---|---|---|
| `vct` | const `mandate.checkout.open.1` | ✔ | |
| `constraints` | array of (`allowed_merchants` \| `line_items`) | ✔ | Schema `contains` → **must include a `checkout.line_items` constraint** |
| `cnf` | object (RFC 7800, `{ "jwk": {EC P-256} }`) | ✔ | Agent's key |
| `iat`, `exp` | integer | | `exp` SHOULD be as short as the task allows |

Constraint definitions:

| `type` | Properties | Evaluation |
|---|---|---|
| `checkout.allowed_merchants` | `allowed: Merchant[]` (SD array) | The merchant must be among the **revealed** `allowed` entries. No revealed entries means fail. SDK matching: by `id`, else `name` + non-empty `website`. |
| `checkout.line_items` | `items: LineItemRequirements[]` (minItems 1). Each has `id: string`, `acceptable_items: Item{id,title}[]` (SD array), `quantity: int > 0` | Every requirement must be satisfied by matching checkout items (match on item `id` among revealed `acceptable_items`), with no double use. Spec suggests a **max-flow** check: flow must equal both the total required quantity and the total checkout quantity, so **no extra items are allowed**. Splitting across several checkouts is not supported. |

Shared types: `Merchant {id: string (req), name: string (req), website?: string}`. Open-mandate `Item {id, title}`. `types/item.json` (UCP-aligned) `{id, title, price: int minor units, image_url?}`.

### 3.4.3 Checkout JWT payload (merchant-created)

AP2 is agnostic, but **with UCP it MUST be the UCP Checkout object**. The AP2 SDK validates it against a vendored copy, `code/sdk/schemas/ucp/types/checkout.json` (titled "UCP Checkout object (dev.ucp.shopping.checkout 2026-04-08)"):

- required: `id`, `line_items[]` (`id`, `item{id,title,price,image_url?}`, `quantity`, `totals[]`, `parent_id?`), `status`, `currency`, `totals[]` (`type`, `amount` int signed minor units, `display_text?`), `links[]`
- optional: `merchant` (**AP2 addition for mandate binding**: `{id,name,website}`), `buyer`, `messages`, `expires_at`, `continue_url`
- The Python merchant sample adds JWT `iat` and `exp` (now + 3600) to this payload and signs with header `{"alg":"ES256","typ":"JWT","kid":"merchant-key-1"}`.

The spec's own example payload uses a different, non-UCP shape (`order_id`, `total_price: 199.0` as a float). Treat it as illustrative only.

### 3.4.4 Checkout Receipt (merchant-signed JWT)

`$id: https://ap2-protocol.org/schemas/checkout_receipt.json`

| Field | Type | Req | Notes |
|---|---|---|---|
| `status` | `"Success"` \| `"Error"` | ✔ | |
| `iss` | string | ✔ | Merchant (the sample uses the merchant website URL) |
| `iat` | integer | ✔ | |
| `reference` | string | ✔ | Hash of the closed Checkout Mandate |
| `order_id` | string | iff Success | |
| `error`, `error_description` | string | iff Error | Error codes from §3.3.4 |

The receipt is signed as a compact JWS, ES256, header `{"alg":"ES256","typ":"JWT","kid":...}` (SDK `jwt_helper.create_jwt`).

### 3.4.5 Example: closed Checkout Mandate (decoded, from the spec)

```json
{
  "header": { "alg": "ES256", "typ": "kb+sd-jwt" },
  "payload": {
    "delegate_payload": [ { "...": "7VLY-eKTFSShLoZRXY5jXcD2UHm1JvPmoANYRqqxy34" } ],
    "iat": 1777342376, "aud": "merchant",
    "nonce": "b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4",
    "sd_hash": "FzLoxbbtgQGYZxoSM2NJYJtkFTSsdfUBoVEQ12k7JN8",
    "_sd_alg": "sha-256"
  },
  "disclosures": [
    ["szhpxKrgGJwyqMEN9WI5Sw", { "_sd": ["3A9U…"], "vct": "mandate.checkout.1",
                                 "checkout_hash": "NivWhuqfzcvZNapvIEJ2-3tsdQLkiuIcye2g46WVgX8" }],
    ["w-n1leFT6z8rHTNHwr5Wow", "checkout_jwt", "<merchant compact JWT>"]
  ]
}
```

---

## 3.5 Payment Mandate

Source: `docs/ap2/payment_mandate.md`, `code/sdk/schemas/ap2/payment_mandate.json`, `open_payment_mandate.json`, `payment_receipt.json`, `types/*.json`.

**Flow:** the SA creates it, the TS (or the agent, in HNP) signs it, and it is **verified by the Credential Provider, the Network and the MPP**. **The merchant does not need to parse it.** It travels inside the payment token or credential that the merchant passes to its PSP.

### 3.5.1 Closed Payment Mandate: `vct = "mandate.payment.1"`

| Field | Type | Req | Notes |
|---|---|---|---|
| `vct` | const `mandate.payment.1` | ✔ | |
| `transaction_id` | string | ✔ | **b64url hash of `checkout_jwt`**, i.e. equals the Checkout Mandate's `checkout_hash` |
| `payee` | Merchant `{id, name, website?}` | ✔ | |
| `payment_amount` | Amount `{amount: integer minor units, currency: ISO-4217 alpha-3}` | ✔ | Final value confirmed by the user |
| `payment_instrument` | `{id: string (req), type: string (req), description?: string}` | ✔ | `type` is an open extension point (`card`, `dpc`, `UPI`, x402, ...) |
| `pisp` | `{legal_name, brand_name, domain_name}` (all req) | | Payment Initiation Service Provider (`domain_name` "as secured by the eIDAS QWAC") |
| `execution_date` | string (ISO 8601) | | Absent means immediate |
| `risk_data` | object | | "map of relevant risk signals collected by the trusted surface at time of mandate creation" |
| `iat`, `exp` | integer | | |

### 3.5.2 Open Payment Mandate: `vct = "mandate.payment.open.1"`

Required: `vct`, `constraints` (must **contain a `payment.reference`**), `cnf`. MAY pre-set any closed property (`payee`, `payment_amount`, `payment_instrument`, `pisp`, `execution_date`, `risk_data`, `iat`, `exp`).

| Constraint `type` | Properties | Evaluation |
|---|---|---|
| `payment.reference` | `conditional_transaction_id` (req): digest of the associated **open Checkout Mandate** | The approved Checkout Mandate chain must contain an open Checkout Mandate with that hash |
| `payment.amount_range` | `currency` (req), `max` (req, int minor), `min?` (int minor) | `payment_amount` within range, same currency (the spec example wrongly uses `100.50`) |
| `payment.budget` | `max` (req, **number**; PR #377 open to make it minor units), `currency` (req) | Requested amount plus the sum of earlier closed mandates must be ≤ max. Needs a running total. |
| `payment.agent_recurrence` | `frequency` (req enum `ON_DEMAND, DAILY, WEEKLY, BIWEEKLY, MONTHLY, QUARTERLY, ANNUALLY`), `max_occurrences?` int | Spacing and count tracked across presentations |
| `payment.allowed_payees` | `allowed: Merchant[]` (SD array) | `payee` must be in `allowed` |
| `payment.allowed_payment_instruments` | `allowed: PaymentInstrument[]` (SD array) | `payment_instrument` must be in `allowed` (SDK matches on `id`) |
| `payment.allowed_pisps` | `allowed: Pisp[]` | PISP must be in `allowed` |
| `payment.execution_date` | `not_before?`, `not_after?` (ISO 8601) | `execution_date` must fall in the window |

### 3.5.3 Payment Receipt (MPP-signed JWT)

| Field | Type | Req |
|---|---|---|
| `status` | `Success` \| `Error` | ✔ |
| `iss` | string (SDK: `pisp.domain_name`, else empty) | ✔ |
| `iat` | integer | ✔ |
| `reference` | hash of the closed Payment Mandate | ✔ |
| `payment_id` | string | ✔ |
| `psp_confirmation_id`, `network_confirmation_id` | string | iff Success |
| `error`, `error_description` | string | iff Error |

The receipt goes to the SA, the CP and (if applicable) the Network.

### 3.5.4 Example: closed Payment Mandate disclosure (from the spec)

```json
{
  "vct": "mandate.payment.1",
  "transaction_id": "NivWhuqfzcvZNapvIEJ2-3tsdQLkiuIcye2g46WVgX8",
  "payee": { "id": "merchant_1", "name": "Demo Merchant", "website": "https://demo-merchant.example" },
  "payment_amount": { "amount": 19900, "currency": "USD" },
  "payment_instrument": { "id": "stub", "type": "card", "description": "Card ••••4242" }
}
```

It is wrapped in a `kb+sd-jwt` hop with `aud: "credential-provider"`, a `nonce`, `sd_hash` and `_sd_alg`.

### 3.5.5 Legacy v0.1 shapes (for reference only)

Sources: `cc8a968:docs/specification.md`, `code/sdk/python/ap2/models/mandate.py`, `payment_request.py`.

- `IntentMandate { user_cart_confirmation_required: bool=true, natural_language_description: str, merchants?: str[], skus?: str[], requires_refundability?: bool, intent_expiry: ISO8601 }`
- `CartMandate { contents: CartContents, merchant_authorization?: str }`. Here `CartContents { id, user_cart_confirmation_required: bool, payment_request: PaymentRequest, cart_expiry: ISO8601, merchant_name }`. `merchant_authorization` was documented as a JWT with header `alg`+`kid` and payload `iss, sub, aud, iat, exp (5–15 min), jti, cart_hash` (hash of canonical JSON of CartContents). It was never implemented: the samples used the fake string `"eyJhbGciOiJSUzI1NiIsImtpZIwMjQwOTA..."`.
- `PaymentRequest` (W3C Payment Request API shape): `method_data[{supported_methods, data}]`, `details{id, display_items[PaymentItem{label, amount{currency, value: float}, pending?, refund_period}], shipping_options?, modifiers?, total}`, `options{request_payer_name/email/phone, request_shipping, shipping_type}`, `shipping_address?`
- `PaymentMandate { payment_mandate_contents: {payment_mandate_id, payment_details_id, payment_details_total: PaymentItem, payment_response: PaymentResponse{request_id, method_name, details, shipping_address, shipping_option, payer_name/email/phone}, merchant_agent, timestamp}, user_authorization?: str }`. `user_authorization` = an SD-JWT-VC presentation whose KB-JWT has `aud, nonce, sd_hash, transaction_data:[hash(CartMandate), hash(PaymentMandateContents)]`.

---

## 3.6 Flows

Source: `docs/ap2/flows.md`, `docs/ap2/specification.md` §Modes, `docs/overview.md` §4.

Verifiers **always receive closed Payment and Checkout Mandates**. Only the trust path differs:

- Direct: the closed mandate is signed by the user or TS.
- Autonomous: the closed mandate is signed by the agent key, and trust comes from a user-signed open mandate that carries `cnf` for that key.

### 3.6.1 Human Present ("direct")

Phase 1, Shopping:

1. The user starts shopping with the SA.
2. The SA works with the merchant to assemble a cart.
3. At checkout, the **merchant creates a signed Checkout (`checkout_jwt`)** and requires a mandate to continue.
4. The SA fetches instrument options from the CP and selects one.

Phase 2, Payment:

1. The SA builds the closed Checkout and Payment Mandate Content and asks the TS for approval.
2. The TS renders the content and gets authentication (biometric) and consent.
3. The TS signs both mandates with `user_sk` (or the Agent Provider key). `hash(checkout_jwt)` links the two.
4. The TS returns the mandates to the SA.
5. The SA sends the **Payment Mandate to the CP**. The CP verifies it and creates a payment token, optionally getting a scoped network token by sharing the mandate with the network.
6. The SA sends the **token and the Checkout Mandate to the Merchant**.
7. The **Merchant verifies the Checkout Mandate against the current cart state**, then initiates payment with the token and the `checkout_jwt` hash.
8. The MPP verifies the Payment Mandate embedded in the token and its binding to the `checkout_jwt` hash.
9. The MPP-signed **Payment Receipt** goes to SA, CP and Network. The Merchant-signed **Checkout Receipt** goes to the SA.

If the payment method is push-based (funds pushed to the merchant), the merchant instead receives confirmation of funds sent and confirms receipt.

### 3.6.2 Human Not Present ("autonomous")

Phase 1a, human present:

1. The SA builds **open** Checkout and Payment Mandate Content.
2. The TS gets consent and signs both. Each includes **`cnf` = agent public key**. The open Payment Mandate carries `payment.reference` = hash of the open Checkout Mandate.

Phase 1b, autonomous: the SA builds the cart, and the merchant returns a signed Checkout.

Phase 2, payment:

1. The SA selects applicable open mandates. It **MUST NOT create overlapping closed mandates** until it receives an error receipt for the previous one (double-spend control).
2. The SA signs the closed Checkout and Payment Mandates with `agent_sk`. These are `kb+sd-jwt` hops whose `sd_hash` binds them to the open mandate. Only the needed disclosures are revealed.
3. The SA sends the Payment Mandates (open + closed chain) to the CP, which returns a token.
4. The SA sends the token and the Checkout Mandates (open + closed chain) to the Merchant.
5. The Merchant verifies the closed mandate against the cart, **evaluates the open-mandate constraints**, then initiates payment with the token, the `checkout_jwt` hash and the **open Checkout Mandate hash**.
6. The MPP verifies the Payment Mandates and both bindings.
7. Receipts are returned as in the Human Present flow.

**HNP to HP fallback:** the Merchant (or CP) returns `unresolved_constraint` to bring the user back to approve closed mandates directly. The overview adds: "Merchant can force user confirmation".

### 3.6.3 Challenges, step-up and risk signals

- **v0.2 normative text does not define a challenge protocol.** The executive summary (`docs/overview.md` §4.1) says any party (issuer, CP, merchant) "may choose to challenge the transaction through existing mechanisms like **3DS2**". The challenge is shown to the user on a Trusted Surface (e.g. hosted 3DS), possibly via redirect. v0.1 §5.5 adds: in v0.1 it is a redirect challenge returned to the SA; OTPs are acceptable; the CP should record that a challenge was done so the user is not challenged twice; in HNP a challenge forces the user back into session.
- **Sample implementation** (`merchant_payment_processor_agent/tools.py`): the MPP returns an A2A `input-required` message with a DataPart `{"challenge": {"type": "otp", "display_text": "..."}}`. The SA resubmits with DataPart `challenge_response`. The demo code is `123`.
- **Risk signals:** `risk_data` (an open object) on the Payment Mandate, set by the TS at mandate creation. In the samples, an A2A DataPart `risk_data` travels with the checkout artifact and the `initiate_payment` message (an opaque string in the samples). v0.1 §7.4 says the risk field is "intentionally left open-ended". **AI-agent presence and modality (HP vs HNP)** "must always be shared" with network/issuer (v0.1 §4.1.3). In v0.2 this is implied by the mandate chain shape (an agent-signed closed mandate means HNP). *Unverified:* no field defined for network authorization messages.

---

## 3.7 What a MERCHANT must implement (v0.2)

Sources: `specification.md` §Verification/Merchant and §Dispute, `implementation_considerations.md` §Merchant, `security_and_privacy_considerations.md`, sample `code/samples/python/src/roles/merchant_agent/tools.py` and `merchant_agent_mcp/server.py`.

1. **Expose catalog and checkout** through a commerce protocol. UCP is RECOMMENDED; the merchant may also be an A2A merchant agent. AP2 does not define these APIs.
2. **Hold an ECDSA signing key** (P-256 / ES256 per `types/jwk.json`; ES384/ES512 per the UCP note). Deterministic schemes such as Ed25519 are currently forbidden for the Checkout JWT (see #268).
3. **Create `checkout_jwt`**: a compact JWS (`header.payload.signature`). With UCP, the payload is the **UCP Checkout object** (+ `merchant` {id,name,website} for constraint binding). The sample adds `iat` and `exp` (1 h) and uses header `{"alg":"ES256","typ":"JWT","kid":"<kid>"}`.
   - **No `iss/aud/jti/cart_hash` claims are defined in v0.2.** Those were v0.1 `merchant_authorization` docstrings.
   - Do not produce the checkout JWT until every price-affecting input (e.g. shipping address) is collected. This rule is explicit in v0.1 and implied in v0.2.
   - If you ever use a deterministic signature, put a high-entropy salt in the payload. A unique checkout `id` per session helps either way.
4. **Store the exact `checkout_jwt` string, or its hash, per checkout.** You must check that `checkout_hash` matches "the hash of the **latest** `checkout_jwt`". A re-issued checkout (after a price change) invalidates older mandates.
5. **Publish the verification key.** AP2 does **not** define key discovery; it is left to the commerce protocol. With UCP, keys go in `keys[]` (JWK) of `/.well-known/ucp`. Samples use a local PEM file and `kid: "merchant-key-1"`.
6. **Issue the nonce/audience challenge** for the closed mandate's KB hop (`aud`, `nonce`). The spec does not define how. The samples pre-share `aud = "https://merchant.com"`, `nonce = "merchant-nonce-xyz"`. *Unverified* how UCP conveys a nonce.
7. **Verify the Checkout Mandate before completing** (deterministic code):
   - Verify the chain per §3.3.3. The root key is a trusted issuer (x5c chain to a trusted root, e.g. a DPC issuer cert) or a **trust-listed Agent Provider key**. Check `typ`, signatures, `cnf` hops, `sd_hash`, disclosure digests, `aud`/`nonce` on the final hop, `iat`/`exp`, and `vct == "mandate.checkout.1"` (and `.open.1` for open mandates).
   - Recompute `b64url(sha256(checkout_jwt))` and compare it to `checkout_hash`. Verify that `checkout_jwt` is **your own** signature and that it matches the current cart state (id, totals, line items).
   - For HNP chains, evaluate `checkout.allowed_merchants` and `checkout.line_items` (max-flow, no extras). Unknown constraint types fail with `unresolved_constraint`.
   - Enforce single use: do not accept a second closed mandate from the same open mandate. Track the open-mandate hash.
8. **Complete payment with the MPP.** Pass the CP-issued `payment_token` (which embeds or is scoped to the closed Payment Mandate), the **`checkout_jwt` hash** and, in HNP, the **open Checkout Mandate hash**. The merchant may delegate mandate verification to the MPP.
9. **Return a signed Checkout Receipt JWT** on both success and error (§3.4.4). `reference` is the hash of the closed Checkout Mandate. With the SDK this is `sha256_b64url(MandateClient().get_closed_mandate_jwt(chain))`. Use error codes `invalid_credential`, `unresolved_constraint`, `invalid_mandate` or `mandates_not_supported`.
10. **Retain evidence for disputes**: the compact Checkout Mandate (with disclosures) and the receipt. In a dispute, verifiers recompute `hash(checkout_jwt)`, check that receipt `reference` equals the hash of the closed mandate, and verify that the Payment Mandate's `transaction_id` matches `checkout_hash`. Retention rules are out of scope.

The Python merchant sample only checks the chain signature plus `aud`/`nonce`, and in the MCP sample the constraints. It does **not** compare `checkout_hash` to a stored JWT, so do not copy it as-is.

**If the merchant also plays the MPP role:** receive the token, verify the Payment Mandate (chain, `vct == mandate.payment.1`, `transaction_id == checkout_hash`, open-mandate constraints including `payment.reference == open checkout hash`), process the payment, and issue the signed **Payment Receipt**, sending it to the SA and CP.

---

## 3.8 Transport bindings

### 3.8.1 A2A

Sources: `v0.1.0:docs/a2a-extension.md` (removed in v0.2), current samples `code/samples/python/src/common/{a2a_extension_utils,constants}.py`, `roles/*/agent.json`, `code/samples/go/pkg/roles/merchant_agent/tools.go`.

| Item | v0.1 doc (normative then) | v0.2 samples (only current reference) |
|---|---|---|
| Extension URI | `https://github.com/google-agentic-commerce/ap2/tree/v0.1`. Even earlier spec samples used `https://google-a2a.github.io/A2A/ext/payments/v1`. | **`https://github.com/google-agentic-commerce/ap2/v1`** |
| AgentCard | `capabilities.extensions[{uri, description, required, params:{roles:[...]}}]`. `roles` minItems 1 from `merchant`, `shopper`, `credentials-provider`, `payment-processor`. Merchants SHOULD set `required: true`. | `{uri, description:"Supports the Agent Payments Protocol.", required:true}`, **no params**. Plus a payment-method extension, e.g. `https://sample-card-network.github.io/paymentmethod/types/v1`. Clients send the `X-A2A-Extensions` header. |
| Message DataPart keys | `ap2.mandates.IntentMandate` (Message), `ap2.mandates.CartMandate` (Artifact from merchant), `ap2.mandates.PaymentMandate` (Message), optional `risk_data` | `ap2.mandates.CheckoutMandateSdJwt`, `ap2.mandates.PaymentMandateSdJwt` (compact SD-JWT chains as strings), `ap2.checkout` (merchant artifact: `{cart_id, checkout_jwt, checkout_hash, item_label, amount, amount_cents, currency}`), `ap2.cart`, `ap2.PaymentReceipt`, `risk_data`, `challenge`, `challenge_response` |

**There is no normative v0.2 A2A binding.** Treat the `/v1` URI and keys as sample conventions (*unverified* as a stable contract). The UCP A2A binding (`ucp/.../checkout/a2a.md`) instead uses the DataPart `a2a.ucp.checkout` with `ap2.merchant_authorization` and `ap2.checkout_mandate`.

### 3.8.2 MCP

The v0.2 Python samples include MCP servers for the merchant, MPP and CP (`roles/*_mcp/server.py`). The merchant MCP `complete_checkout` verifies the checkout chain and issues an ES256 checkout receipt. There is no MCP binding spec. The FAQ still says "working on an SDK and a MCP server".

### 3.8.3 UCP: the AP2 Mandates extension

Source: `ucp/docs/specification/payment/extensions/ap2-mandates.md`, `source/schemas/common/payment_ap2_mandate.json`.

- **Capability:** `dev.ucp.common.payment.ap2_mandate`, `extends: "dev.ucp.shopping.checkout"`, declared in `/.well-known/ucp` with `config.vp_formats_supported: {"dc+sd-jwt": {}}`. It is active only if it appears in the platform/business **capability intersection**. Once active, the session is **"Security Locked"**: neither side may fall back.
- **Fields added to Checkout** (everything under an `ap2` object):

| Field | Direction | Type / format |
|---|---|---|
| `ap2.merchant_authorization` | business → platform, in **every** checkout response (omitted from requests) | string `^[A-Za-z0-9_-]+\.\.[A-Za-z0-9_-]+$`: **JWS Detached Content** (RFC 7515 App. F) `<b64url header>..<b64url sig>`. Header `{alg (ES256/ES384/ES512), kid}`, both required. |
| `ap2.checkout_mandate` | platform → business, **required on `complete`** (omitted on create/update) | SD-JWT+kb string. **Contains the full checkout, including `ap2.merchant_authorization`.** |
| payment mandate | platform → business → PSP | inside `payment.instruments[*].credential.token` (a "composite token") |

- **How the business signs:** `payload = checkout minus "ap2"` → **JCS (RFC 8785)** canonicalize → `signing_input = b64url(header) + "." + b64url(jcs_bytes)` → sign → emit `header..signature`. The signature covers the *complete* checkout JSON, including `ucp` namespace members. Platforms MUST verify it before showing the checkout to the user.
- **Business verification on complete:**
  1. Return `mandate_required` if the mandate is missing.
  2. Verify the SD-JWT signature, key binding and expiry per AP2.
  3. Extract the embedded checkout and re-verify **its own** `merchant_authorization` (JCS over checkout minus `ap2`, key by `kid`).
  4. Confirm the terms match the current session (id, totals, line items).
  5. Pass the token to the PSP, which verifies the payment mandate.
- **Platform keys:** under the trusted-platform-provider model, the platform MUST publish at least one key in its profile `keys[]`; otherwise the business returns `agent_missing_key`. Under the user-credential model, the key is bound to the digital payment credential.
- **Error codes:** `mandate_required`, `agent_missing_key`, `mandate_invalid_signature`, `mandate_expired`, `mandate_scope_mismatch`, `merchant_authorization_invalid`, `merchant_authorization_missing`.
- **Mismatch to resolve:** UCP's detached-JWS-over-JCS `merchant_authorization` is not literally AP2's compact `checkout_jwt`. A natural bridge is `checkout_jwt = header + "." + b64url(JCS(checkout − ap2)) + "." + signature`, which is a valid compact JWS from the same signature. That would make `checkout_hash = b64url(sha256(that string))`. **Unverified:** neither spec states this mapping. UCP's own overview (`ucp-and-ap2.md`) still uses older wording ("`checkoutSignature`", "CheckoutMandate contains the hash of the CheckoutObject").

### 3.8.4 x402 (crypto / stablecoin)

- AP2 is payment-method agnostic. x402 is "a type of emerging payment method" (`cc8a968:docs/topics/ap2-and-x402.md`). The FAQ points to the separate repo **google-agentic-commerce/a2a-x402**.
- v0.2 samples (`scenarios/a2a/human-{present,not-present}/x402`, `roles/x402_*_mcp`): the CP returns a bundle containing the **mandate chain plus an EIP-3009 `TransferWithAuthorization`** payload (USDC on Base Sepolia, `0x036CbD53…CF7e`). The merchant calls a PSP `settle-payment` endpoint with `{payment_token, checkout_jwt_hash, open_checkout_hash}` and gets back a receipt and `tx_hash`. No x402-specific mandate fields: x402 is just another `payment_instrument.type` (*unverified* exact type string).

### 3.8.5 ACP

The AP2 repo and docs **never mention** the Agentic Commerce Protocol (OpenAI/Stripe). No relationship is defined.

---

## 3.9 Reference implementations, schemas and .NET options

| Asset | Path | Notes |
|---|---|---|
| **JSON Schemas (canonical, draft 2020-12)** | `code/sdk/schemas/ap2/{checkout_mandate, open_checkout_mandate, payment_mandate, open_payment_mandate, checkout_receipt, payment_receipt}.json`, `ap2/types/{amount, item, jwk, merchant, payment_instrument, pisp, receipt_status}.json`, plus vendored UCP `ucp/types/{checkout, line_item, total, buyer, link, message*}.json` | Use these to generate C# types. Custom keywords `x-selectively-disclosable-field` / `x-selectively-disclosable-array` mark SD claims. Constraint arrays use `oneOf`/`anyOf` keyed by a `type` const, so a polymorphic `[JsonPolymorphic]` discriminator on `type` works. Receipts use `oneOf` on `status`. |
| Python SDK | `code/sdk/python/ap2/sdk/` (`MandateClient.create/present/verify`, `ReceiptClient`, `sdjwt/{sd_jwt,kb_sd_jwt,chain}.py`, `constraints.py`, `max_flow_helper.py`, `checkout_mandate_chain.py`, `payment_mandate_chain.py`) | The best executable reference for chain verification and constraint semantics. Install with `uv pip install git+https://github.com/google-agentic-commerce/AP2.git@main`. **Not on PyPI.** Tests in `code/sdk/python/ap2/tests/` are a good source of conformance vectors. |
| Python samples (A2A + ADK + Gemini; MCP servers) | `code/samples/python/src/roles/*` | Merchant, MPP, CP, shopping agent v2. Scenarios: HP/HNP × cards/x402. |
| Go sample | `code/samples/go` | **Still the v0.1 model** (CartMandate). Not useful for v0.2. |
| Android (Kotlin) | `code/samples/android/shopping_assistant` | DPC / OpenID4VP via the Credential Manager / Digital Credentials API (`DpcHelper.kt`) |
| Web client | `code/web-client` (Vite/React, TS `sdJwtDecoder.ts`) | Mandate viewer and approval UI |
| Sample certs | `code/samples/certs/{issuer_cert_sdjwt.pem, ds_cert_sdjwt.pem}` | Trusted root for DPC x5c in the merchant sample |

**For .NET (no official SDK exists).** The building blocks below are *unverified* as to which libraries are best and need evaluation:

- **ES256 JWS** (checkout JWT, receipts): `System.Security.Cryptography.ECDsa` (P-256, IEEE-P1363 r‖s, 64-byte signature) or `Microsoft.IdentityModel.JsonWebTokens`. Hash via `SHA256.HashData(Encoding.ASCII.GetBytes(compactJwt))`, then base64url without padding.
- **SD-JWT / KB-JWT / Delegate SD-JWT chains:** you will likely need your own verifier (~300 lines in Python: split on `~~`, then on `~`; verify each JWS; recompute disclosure digests; resolve `{"...": digest}` array elements and `_sd`; check `sd_hash`/`issuer_jwt_hash`, `typ`, `cnf.jwk`, `aud`, `nonce`, `iat`/`exp`). Port the SDK tests as conformance vectors. Community .NET SD-JWT libraries exist (*unverified*) but will not know the Delegate-SD-JWT chain or the `kb+sd-jwt+kb` typ.
- **JCS (RFC 8785)** for UCP `merchant_authorization`: not in the BCL. Use a JCS library or write one (sorted keys and ES6 number formatting). *Unverified* library choice.
- **Constraint evaluation:** port `constraints.py` and `max_flow_helper.py` (line-item max-flow).
- **Key trust:** an x5c chain validator (`X509Chain`) for credential-issuer roots, and a configurable trust list of Agent Provider JWKs.

---

## 3.10 Open issues and uncertainties (as of 2026-11-02)

1. **Algorithm rule conflict** (AP2 issue #268, open): ECDSA-only vs "any algorithm plus entropy" for `checkout_jwt`.
2. **Receipt `reference` computation:** the spec says "same manner as `sd_hash`" (implies disclosures are included); the SDK hashes the leaf JWT only.
3. **Amount units:** schemas say integer minor units, but spec examples use decimals (`100.50`, `price: 199.0`), and `payment.budget.max` is a `number` (PR #377 open).
4. **No normative A2A binding in v0.2.** The sample URI `https://github.com/google-agentic-commerce/ap2/v1` differs from v0.1's `.../tree/v0.1`, and `params.roles` was dropped in the samples.
5. **Nonce/audience issuance** for KB hops and **merchant key discovery** are not defined by AP2. They belong to the commerce protocol (UCP `keys[]`).
6. **UCP ↔ AP2 object mapping** (detached JWS over JCS vs compact `checkout_jwt`; checkout mandate "contains the full checkout") is not spelled out. UCP's overview page still uses older terms.
7. **Root SD-JWT `typ`** in the examples is the placeholder `example+sd-jwt`; the UCP example uses `vc+sd-jwt`; SD-JWT VC uses `dc+sd-jwt`.
8. Schema descriptions say `vct` "MUST be 'mandate.checkout'", but the `const` is `mandate.checkout.1`. **Use the const.**
9. Challenge/3DS and risk-signal formats are not standardized in v0.2.
10. FIDO standardization (Agentic Authentication / Payments TWGs) may change the wire format before 1.0. One commentator projects FIDO specs in H2 2026 to early 2027 (*unverified*).

Secondary sources consulted for §3.1 governance and adoption context only: [ppc.land: FIDO forms working groups](https://ppc.land/fido-alliance-forms-working-groups-to-lock-down-ai-agent-payments/), [agentic-readiness.lumar.io AP2 notes](https://agentic-readiness.lumar.io/docs/commerce/ap2), [stellagent.ai (JA)](https://stellagent.ai/ja/insights/google-ap2-fido-alliance-agent-payments).

---

# 4. How the three relate, and what a merchant should share between them

## 4.1 UCP ⇄ AP2

- **AP2 v0.2 positions itself as "a security feature within a Commerce Protocol".** It is "designed explicitly to be compatible with the Universal Commerce Protocol (UCP)" and recommends UCP for catalog and checkout (AP2 `docs/ap2/specification.md`).
- **UCP carries AP2** through the `dev.ucp.common.payment.ap2_mandate` extension (UCP `docs/specification/payment/extensions/ap2-mandates.md`):
  - The business adds `ap2.merchant_authorization` to each checkout response: a detached JWS over the JCS-canonicalized checkout, ES256/384/512, `kid` taken from `keys[]` in `/.well-known/ucp`.
  - The platform sends `ap2.checkout_mandate` (SD-JWT+KB) on complete. The payment mandate rides inside `payment.instruments[].credential.token`.
  - See section 3.8.3. Once negotiated, the session is "security locked" with no fallback.
- UCP's overview names AP2 the "Recommended Flow for Agents" for autonomous (human-not-present) purchases. Otherwise UCP checkout "has to be finalized manually by the user through a trusted UI" (`checkout/index.md` Overview).
- **Version skew to watch:** many blog posts describe AP2 v0.1's *Intent/Cart/Payment* mandates. v0.2 replaced these with **Checkout Mandate** (`mandate.checkout.1`) and **Payment Mandate** (`mandate.payment.1`), each with an "open" (constraints) variant for human-not-present flows. The request's assumed "Cart Mandate signed by the merchant" corresponds in v0.2 to the merchant-signed `checkout_jwt` / UCP `merchant_authorization`.

## 4.2 AP2 ⇄ x402

- x402 is just a payment method under AP2: an EIP-3009 stablecoin transfer authorization bundled with the mandate chain. It lives in the separate repo `google-agentic-commerce/a2a-x402`.
- Stripe also supports x402 and "Machine Payments Protocol (MPP)" for pay-per-call APIs (docs.stripe.com/payments/machine). This is unrelated to retail checkout.

## 4.3 ACP ⇄ UCP (overlap)

The two checkout APIs are structurally very close. A merchant can run **one internal checkout-session engine with two protocol adapters**.

| Concern | UCP 2026-08-25 | ACP 2026-04-17 |
|---|---|---|
| Base path | `{endpoint}/checkout-sessions` (hyphen) | `/checkout_sessions` (underscore) |
| Update | `PUT /checkout-sessions/{id}` (full replacement) | `POST /checkout_sessions/{id}`: a partial update where an absent key means unchanged, `null` clears the field, and `line_items` replaces the whole list |
| Complete / cancel | `POST …/{id}/complete` (200), `POST …/{id}/cancel` (200; an error message if it can't be canceled) | same sub-paths. Complete returns 200; cancel returns 200, or **405** if not cancelable |
| Create status | 201 | 201 |
| Versioning | Profile `ucp.version` + capability intersection per request | `API-Version` header |
| Status enum | `incomplete, requires_escalation, ready_for_complete, complete_in_progress, completed, canceled` | `incomplete, not_ready_for_payment, requires_escalation, authentication_required, ready_for_payment, pending_approval, complete_in_progress, completed, canceled, in_progress, expired` |
| Line item request | `{item:{id}, quantity}` | `Item {id, name?, unit_amount?}`. **2026-04-17 schema defect: no `quantity` on Item** (examples send one; see section 2) |
| Totals | `totals[] {type (open string), display_text?, amount (**signed** minor units), lines?}`. Exactly one `subtotal` + one `total` | `totals[] {type (enum: items_base_amount, items_discount, subtotal, discount, fulfillment, tax, fee, gift_wrap, tip, store_credit, total, amount_refunded), display_text*, amount (discounts **positive**, shown as subtractive), breakdown?}` |
| Messages | `{type: error\|warning\|info, code (open), content, path (JSONPath), severity: recoverable\|requires_buyer_input\|requires_buyer_review\|unrecoverable}` | `{type: info\|warning\|error, code (enum), content_type*, content, param (JSONPath), severity: info..critical, resolution: recoverable\|requires_buyer_input\|requires_buyer_review}` |
| Links | `links[] {type (open: privacy_policy, terms_of_service, refund_policy, shipping_policy, faq), url, title?}` | `links[] {type (enum: terms_of_use, privacy_policy, return_policy, shipping_policy, contact_us, about_us, faq, support), url, title?}` |
| Fulfillment | Extension: methods → destinations → groups → options | `fulfillment_options[]` + `fulfillment_details` + `selected_fulfillment_options[]` |
| Payment | Payment handlers advertised in profile and response. Credential in `payment.instruments[]` | Payment handlers in `capabilities.payment.handlers[]` (since 2026-01-30). `payment_data` on complete |
| Idempotency | `Idempotency-Key` (OpenAPI: required). 24 h. Mismatch → **409** | `Idempotency-Key` required on all POSTs. Mismatch → **422**, in-flight → 409 |
| Webhooks | Full Order snapshot, RFC 9421 signed, `Webhook-Id` / `Webhook-Timestamp` | `order_create` / `order_update`, HMAC-SHA256 `Merchant-Signature` |
| Request auth | OAuth / API key / mTLS / RFC 9421 (permissionless) | Bearer API key issued to the agent (pre-registration) |
| Discovery | `/.well-known/ucp` (required for discovery) | `/.well-known/acp.json` (optional) |
| MCP | Tools `create_checkout`, … with `meta.ucp-agent` | Tools `create_checkout_session`, … with `meta.api_version` |
| Catalog | Live `catalog.search` / `lookup` capabilities **plus** Merchant Center feeds (Google) | Product **feed** pushed to the agent (file or Feed API) |

**Shared payment rail:** Stripe's Shared Payment Token (`spt_…`) is used by both. In ACP it comes from delegated payment / Stripe. In UCP it arrives through the `com.stripe.payments` handler, where the platform tokenizes at `commerce.stripe.com/api/ucp/tokenize`. The merchant charges it identically in both cases: `PaymentIntent` + `payment_method_data[shared_payment_granted_token]`, header `Stripe-Version: 2026-09-30.preview`. Google Pay tokens (`com.google.pay`, `PAYMENT_GATEWAY`) go to whatever gateway is configured in the handler's `tokenization_specification`.

**Do merchants implement both?**
- Stripe's Agentic Commerce Suite exposes a seller to multiple agents from one integration: a catalog feed plus Stripe-managed checkout.
- Stripe's "Use your own checkout" path is now documented **only for UCP** (docs.stripe.com/agentic-commerce/sellers/use-cases/retail.md, fetched 2026-11-02). Stripe's top-level agentic-commerce page lists UCP as its only protocol page.
- OpenAI's developer docs still document ACP checkout. Onboarding there is currently feed-first and partner-gated.
- **Unverified:** how many merchants run both natively. Platform vendors (Shopify, Stripe, commercetools-type platforms) appear to abstract both. No primary-source statistics were found.

**Practical recommendation:** build UCP natively; Google is the live, documented consumer. Keep the domain model (session, line items, totals, fulfillment groups, messages, order events) protocol-neutral, add an ACP adapter, and treat AP2 as an optional UCP extension.

## 4.4 Codegen and validation assets usable from .NET

| Asset | Path / URL | Suggested use |
|---|---|---|
| UCP JSON Schemas (2020-12) | `ucp/source/schemas/**`, published at `https://ucp.dev/2026-08-25/schemas/...` | Generate C# models. Mind the `ucp_request` per-op annotations, `allOf` extension composition and open (`additionalProperties: true`) objects |
| UCP OpenAPI | `ucp/source/services/shopping/rest.openapi.json` (also `common/`) | Headers and status codes. NSwag can generate controllers or contracts |
| UCP OpenRPC (MCP, embedded) | `ucp/source/services/shopping/{mcp,embedded}.openrpc.json` | MCP tool schemas |
| UCP `ucp-schema` CLI (Rust) | github.com/Universal-Commerce-Protocol/ucp-schema | `resolve --request --op create` gives per-op flattened schemas → NJsonSchema |
| UCP conformance tests | github.com/Universal-Commerce-Protocol/conformance | Black-box test your Orchard Core server |
| UCP samples / Python + JS SDKs | github.com/Universal-Commerce-Protocol/{samples,python-sdk,js-sdk} | Behavioural reference |
| ACP JSON Schema + OpenAPI + OpenRPC | `acp/spec/2026-04-17/json-schema/*.json` (the most complete), `openapi/*.yaml`, `openrpc/*.json` | Generate C# models. Work around the documented defects (section 2) |
| AP2 JSON Schemas | `AP2/code/sdk/schemas/ap2/**` | Mandate and receipt models |
| Crypto in the .NET BCL | `System.Security.Cryptography.ECDsa` (P-256, IEEE P1363 r‖s output, required by both RFC 9421 and JWS ES256), `SHA256`, `HMACSHA256` | RFC 9421 signing and verification is not in the BCL; implement the signature-base construction (RFC 9421 §2.5) yourself. RFC 8941 structured-field parsing is also not in the BCL. **Unverified:** maturity of community NuGet packages for RFC 9421, RFC 8785 (JCS) and SD-JWT |
| Google Pay handler schema | `https://pay.google.com/gp/p/ucp/2026-01-11/schemas/config.json` | Handler config model |
| Stripe UCP handler schemas | `https://ucp.stripe.com/payments/2026-06-25/schema.json` (+ config, instrument and credential files) | Handler config model |

## 4.5 Consolidated list of unverified / uncertain items

1. **OpenAI Instant Checkout status** (scaled back or "moving to Apps", 2026-03). Source is trade press only. OpenAI's own docs no longer foreground checkout onboarding.
2. **Which ACP `API-Version` ChatGPT sends in production.** OpenAI's docs lag the repo (they show 2025-09-12/2025-09-29 shapes).
3. **ACP request signing** (`Signature` / `Timestamp`): the algorithm, canonicalization and key distribution are not specified.
4. **The ACP 2026-04-17 `Item` schema lacks `quantity`**, and there are other schema/example inconsistencies (section 2).
5. **Google's UCP version**: the Native checkout guide targets 2026-04-08. When Google accepts 2026-08-25 is unverified. Google's onboarding steps beyond the guide (Merchant Center attributes, approval, webhook and signing requirements) were not found on the fetched page.
6. **UCP `Idempotency-Key` / `Request-Id`** are `required: true` in OpenAPI but described as SHOULD in prose. Treat them as required.
7. **Shop Pay handler details** (`dev.shopify.shop_pay`) are known only from UCP spec examples.
8. **AP2:**
   - Ed25519 vs ECDSA-only for `checkout_jwt` (issue #268).
   - No normative v0.2 A2A binding.
   - Nonce issuance is undefined.
   - The UCP detached-JWS ↔ AP2 `checkout_jwt` mapping is not spelled out.
   - The receipt `reference` hash differs between the spec and the SDK.
9. **Market adoption** of merchants running both ACP and UCP: there are no primary-source data.

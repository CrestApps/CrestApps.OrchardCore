---
name: crestapps-agentic-commerce
description: >
  Skill for extending the CrestApps.OrchardCore Agentic Commerce module, which lets AI agents buy from the store with
  the Universal Commerce Protocol (UCP 2026-08-25 and 2026-04-08), the Agentic Commerce Protocol (ACP 2026-04-17 and
  2026-01-30) and the AP2 mandates extension (preview). Covers the protocol-neutral agent checkout
  (IAgentCheckoutService, AgentCheckoutContext, AgentCheckoutInput, AgentCheckoutUpdate, AgentCheckoutView,
  AgentCheckoutStatus) on ICheckoutEngine, how the UCP and ACP mappers and request filters map onto it, adding a
  payment handler (IAgentPaymentHandler, AgentPaymentHandlerDescriptor, AgentPaymentPreparation,
  CheckoutPaymentDataKeys.DelegatedCredential, PaymentProviderCapabilities.AcceptsDelegatedCredentials), adding a UCP
  capability or extension (IUcpProfileContributor, UcpCapability, IUcpCheckoutExtension, negotiation), sending a
  protocol's order updates (IAgentOrderPublisher, IWebhookOutbox, WebhookDirectDelivery, IWebhookDeliveryFormatter),
  the store's signing keys (IMerchantSigningKeyService), platform sign-in, IdempotencyPolicy, escalation and the
  hand-off link, and the traps (the agent principal must not own the store checkout, money only through the checkout
  engine, SanitizeProviderData drops delegated keys, profile fetches through the egress guard, minor units, totals
  that add up). Use this skill when asked to add an agent payment method, a UCP capability or extension, another
  agentic protocol or binding (ACP MCP, A2A), an order webhook format, or to change how agents check out.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Extending Agentic Commerce

The Agentic Commerce module (`src/Modules/CrestApps.OrchardCore.AgenticCommerce`, with
`src/Core/CrestApps.OrchardCore.AgenticCommerce.Core` and `src/Abstractions/CrestApps.OrchardCore.AgenticCommerce.Abstractions`)
lets AI agents buy for a buyer. Read the docs page first: `src/CrestApps.Docs/docs/modules/agentic-commerce.md`
(Technical Manual) and `src/CrestApps.Docs/docs/user-manual/commerce/ai-agents.md` (User Manual). The protocol notes
the code was built from are in `docs/engineering/commerce/agentic-commerce-protocols-reference.md`.

## The shape of the module

```
UCP REST (UcpEndpoints)   UCP MCP (UcpMcpEndpoints)   ACP REST (AcpEndpoints)   AI tools (AI/*.cs)
        |  UcpRequestFilter  |                               | AcpRequestFilter        |
        |  UcpCheckoutMapper |                               | AcpCheckoutMapper       |
        +--------------------+---------------+---------------+-------------------------+
                                             |
                          IAgentCheckoutService (AgentCheckoutService, Core)
                                             |
      ICartService (pricing) -> IOrderService (order, channel "agent") -> ICheckoutEngine (session, payment)
      ICheckoutContactStep (Customers)   ICheckoutFulfillmentStep (Shipping)   CheckoutCouponService
                                             |
                       IAgentPaymentHandler -> BeginPaymentAsync with DelegatedCredential
```

- **Request filters** (`UcpRequestFilter`, `AcpRequestFilter`) read the body once (`AgentRequestBody`, 1 MB), sign the
  platform in (`AgentPlatformAuthenticator`: API key `agk_`, OAuth bearer token through the `Api` scheme, or for UCP an
  RFC 9421 signature with a key from the platform's profile), check the IP allowlist, negotiate the version, build an
  `AgentRequest` with an `AgentCheckoutContext`, and set `HttpContext.User` to `AgentPrincipal.Create(platformId)` so
  idempotency keys are kept per platform.
- **Mappers** turn protocol JSON into `AgentCheckoutInput` / `AgentCheckoutUpdate` / `AgentPayment`
  (`ReadInput`, `ReadUpdate`, `ReadPayment`) and an `AgentCheckoutResult` back into protocol JSON (`WriteAsync`).
  Statuses map in `ToStatus` (UCP `ready_for_complete`, ACP `ready_for_payment`, and so on).
- **`AgentCheckoutService`** owns everything else: the `AgentCheckout` record, pricing, the order and store checkout,
  the steps, discount codes, totals, messages, escalation, payment, expiry and the per-checkout lock.

A new binding (ACP over MCP, UCP over A2A) is a new endpoint plus a mapper over `IAgentCheckoutService`. Never call
carts, orders or providers from an endpoint.

## Add a payment handler (required pieces)

1. **`IAgentPaymentHandler`** (Abstractions, namespace `CrestApps.OrchardCore.AgenticCommerce.Services`):
   - `GetName(protocol)`: the handler's name in that protocol (UCP reverse-DNS such as `com.stripe.payments`, ACP such
     as `dev.acp.tokenized.card`).
   - `DescribeAsync(AgentPaymentHandlerContext)`: return `null` when it can't be offered. **Return `null` when
     `context.TestMode` is true** unless the handler only takes test credentials. Fill `Id`, `Name`, `Version`, `Spec`,
     `Schema`, `InstrumentTypes`, `Config` (public values only), `Psp`, `DisplayName`, and `RequiresDelegatePayment` for
     ACP's tokenized card shape.
   - `PrepareAsync(context, AgentPayment)`: return `AgentPaymentPreparation` with the checkout provider's `ProviderKey`
     and `ProviderData` holding `CheckoutPaymentDataKeys.DelegatedCredential` and `DelegatedCredentialType`. Fail with
     `AgentPaymentPreparation.Fail(message)` for an empty credential.
2. **The checkout provider** (`ICheckoutPaymentProvider` with that key) sets
   `PaymentProviderCapabilities.AcceptsDelegatedCredentials = true` and, in `BeginAsync`, charges the credential on the
   server when `DelegatedCredential` is present: confirmed at once, idempotent on `attempt.IdempotencyKey`, one-time
   only (refuse `SavePaymentMethod` and recurring obligations), `RequiresAction` with a `RedirectUrl` when the bank asks
   for 3-D Secure, `Decline` otherwise. Model on `StripeCheckoutPaymentProvider.ChargeDelegatedCredentialAsync` and
   `TestCheckoutPaymentProvider.BeginDelegated`. For delegated 3-D Secure (ACP `authentication_required`), also set
   `AcceptsExternalAuthentication`, read `ThreeDSecureResult.From(context.ProviderData)`, finish the attempt's existing
   payment (`attempt.ProviderReference`) with the result instead of making a second one, and report
   `PaymentBeginResult.CardBrand` so the directory server is known.
3. **A new credential kind** goes in `DelegatedCredentialTypes` (Checkout.Abstractions).
4. **Register** in a startup with `[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]` and
   `[RequireFeatures(<the provider's feature>)]`: `services.AddScoped<IAgentPaymentHandler, YourHandler>()`. See
   `StripePaymentsStartup` and `TestPaymentsStartup` in `Startup.cs`.
5. The platform's **Payment handlers** list filters by `descriptor.Name`; the agent may name the handler by `Id` or
   `Name` when it completes (`AgentCheckoutService.PayAsync`).

Reference implementations: `Payments/StripeSharedPaymentTokenHandler.cs`, `Payments/GooglePayPaymentHandler.cs`,
`Payments/TestAgentPaymentHandler.cs`.

## Add a UCP capability or extension

1. **`IUcpProfileContributor.ContributeAsync(UcpProfileContext)`**: add a `UcpCapability` (`Name`, `Version` =
   `context.Version`, `Spec`, `Schema`, `Extends` for an extension, `Config`) or a `UcpService` (a transport). Names can
   differ by version (see `Ap2ProfileContributor.GetName`).
2. Negotiation (`UcpNegotiator`) keeps a capability only when the platform's profile offers it in the same version, and
   drops an extension whose parent isn't kept. In code, check `AgentRequest.Has(name)` before reading or writing the
   extension's members.
3. **`IUcpCheckoutExtension`** for checkout extensions: `WritingAsync` adds members to every answer
   (`UcpCheckoutContext.Json`), `CompletingAsync` returns an `AgentCheckoutMessage` to refuse a complete request
   (the endpoint answers the checkout with `ucp.status = "error"`), `CompletedAsync` keeps evidence
   (`AgentCheckout.SetProperty`, then `IAgentCheckoutStore.UpdateAsync`). `UcpCheckoutContext.Items` carries state from
   `CompletingAsync` to `CompletedAsync`.
4. If the extension reads checkout input, extend `UcpCheckoutMapper.ReadInput`/`ReadUpdate` and add the fields to the
   abstractions models; if it is a checkout extension, add it to `UcpCheckoutMapper.IsCheckoutExtension`.
5. Register each with `AddScoped<IUcpProfileContributor, ...>()` / `AddScoped<IUcpCheckoutExtension, ...>()` in the
   feature's startup (see `Ap2Startup`, `UcpCatalogStartup`).

Reference: `Ap2/Ap2ProfileContributor.cs`, `Ap2/Ap2CheckoutExtension.cs`, `Ucp/UcpShoppingProfileContributor.cs`.

## Send a protocol's order updates

1. **`IAgentOrderPublisher`** (`Protocol`, `PublishAsync(checkout, order, created)`): `AgentOrderUpdates` calls the
   publishers whose `Protocol` equals `AgentCheckout.Protocol`, for orders on the `agent` channel, when the order is
   paid (`created: true`), canceled, refunded or returned, and, with Shipping, shipped, delivered or returned.
2. Queue with **`IWebhookOutbox.EnqueueAsync(WebhookDirectDelivery)`**: `OwnerId` (the platform id), `Url`,
   `EventName`, `Body` (sent byte for byte), `FormatterName`, and `Data` with **ids only, never secrets**.
3. **`IWebhookDeliveryFormatter`** (`Name` = the `FormatterName`): at every attempt, read the secret or key yourself
   and return `WebhookFormattedRequest.Headers`, or `WebhookFormattedRequest.Fail(message)` when it can't sign. Sign the
   exact `context.Body` with `context.Timestamp`. The dispatcher retries with the subscription backoff and fails a
   delivery whose formatter isn't registered.
4. Register the publisher and the formatter in the protocol's startup (see `UcpStartup`, `AcpStartup`).

Reference: `Ucp/UcpOrderPublisher.cs` + `Ucp/UcpOrderWebhookFormatter.cs` (RFC 9421 with
`IMerchantSigningKeyService.GetSigningKeyAsync`), `Acp/AcpOrderPublisher.cs` + `Acp/AcpOrderWebhookFormatter.cs`
(`TimestampedHmacSignature`, secret protected with `AgenticCommerceStorage.SecretProtectionPurpose`).

## Idempotency

Mutating endpoints use `.WithIdempotency(IdempotencyPolicy)` from `CrestApps.OrchardCore.Commerce.Api.Core`. A
protocol sets its own `Lifetime`, `MaxKeyLength`, `MissingKey`, `InvalidKey`, `Fingerprint` (UCP: SHA-256 of the raw
body; ACP: SHA-256 of the RFC 8785 canonical JSON), `Conflict` (UCP 409, ACP 422), `InFlight`, `ReplayedHeader` and
`EchoKey`. See `UcpEndpoints.Idempotency` and `AcpEndpoints.Idempotency`. Don't hand-roll key handling.

## Traps

- **The agent principal must not own the store checkout.** Requests run as `agent-platform:{id}`, and
  `ICheckoutEngine.StartAsync` takes the current user as owner. `AgentCheckoutService.StartAsync` resets
  `CheckoutSession.OwnerId` to the linked buyer (or `null`) and clears `GuestTokenHash`; the hand-off controller gives a
  guest session to the buyer's browser. Keep both when you change checkout creation.
- **Money only moves through `ICheckoutEngine`.** Handlers prepare provider data; they never call a gateway. The ledger,
  reconciliation, refunds and issue recording depend on it.
- **Delegated keys are server-only.** `PaymentEndpoints.SanitizeProviderData` drops `DelegatedCredential`,
  `DelegatedCredentialType` and the off-session keys from browser and API requests. A new key of that kind must be added
  there too.
- **Profiles and webhooks go through the egress guard.** Fetch platform profiles with `UcpProfileClient` (named client
  `AgenticCommerce.Profiles`, `AddEgressGuardedHttpClient`, no redirects, 256 KB, 10 s) and send webhooks through the
  outbox. Never use a plain `HttpClient` on an address a platform gave.
- **Minor units.** Protocol money is integers in the currency's minor units: `AgentJson.ToMinor` /
  `CurrencyScale.ToMinorUnits`. Never multiply by 100.
- **Totals must add up.** subtotal - discount + fulfillment + tax + fee = total (`AgentCheckoutService.BuildTotals`),
  and AP2 matches the mandate's total in minor units. Don't add a total line without keeping the sum.
- **Business errors are not HTTP errors in UCP.** Answer the checkout with HTTP 200 and messages; only not found (404),
  busy (409 + `Retry-After`) and too many (429) are HTTP errors.
- **Test mode.** A test-mode platform must only see test handlers; every real handler returns `null` for it.
- **Credentials never travel in recipes.** `AgentPlatformHandler.Populate` doesn't read `ApiKeyHash` or
  `ProtectedWebhookSecret`.
- **The signed body is the read body.** Verify signatures and digests against `AgentRequest.Body`, never a re-serialized
  object.

## Docs and tests

Update `src/CrestApps.Docs/docs/modules/agentic-commerce.md` (and the User Manual page for any screen change), the
changelog matching `VersionPrefix`, and `docs/feature-reference.md` for a new feature. Add unit tests for mappers,
handlers and formatters in `tests/CrestApps.OrchardCore.Tests/AgenticCommerce`, modeled on the tests there.

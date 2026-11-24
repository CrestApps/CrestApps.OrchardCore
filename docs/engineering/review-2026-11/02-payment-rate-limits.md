# Workstream RL: payment rate limits and card-testing protection

Part of the [November 2026 review plan](README.md). Paths are relative to `src/`. `M/X` is `Modules/CrestApps.OrchardCore.X`, `C/X` is `Core/CrestApps.OrchardCore.X`, and `A/X` is `Abstractions/CrestApps.OrchardCore.X`.

Goal: an attacker cannot use the store to test stolen cards, guess coupon or gift-card codes, or enumerate orders. Request limits are an extension of Orchard Core's `OrchardCore.RateLimits` module, delivered as a "Payment Rate Limits" feature that depends on it, so an administrator sees and tunes them under **Tools → Rate Limits**. The protection that decides whether a payment may start runs inside the checkout engine and is always on.

This design was changed after an independent challenge (see [`06-review-log.md`](06-review-log.md)): no new platform module, no hard dependency of Checkout on the limiter middleware, no distributed limiter source, a hotfix that ships first, looser per-network numbers, and a store-wide decline signal.

## 1. What exists today (verified in code)

| # | Finding | Evidence | Effect |
| --- | --- | --- | --- |
| RL-F1 | The payment throttle key contains the checkout session id | `M/Checkout/Endpoints/PaymentEndpoints.cs:214-215` (`{ip}:{sessionId}`), `M/Checkout/Api/CheckoutApiEndpoints.cs:271-272` (`{ip}:{id}`) | Every new checkout gets a fresh budget of 20 attempts in 5 minutes. A card tester starts a checkout per batch and is never throttled. This is the main gap |
| RL-F2 | One limit for every scope, and it cannot be configured | `A/Checkout.Abstractions/Services/PaymentRateLimitOptions.cs` (20 per 5 minutes); nothing calls `Configure<PaymentRateLimitOptions>`; there is no settings page | Coupon guessing, gift-card guessing, order lookup, status polling and payment share the same numbers; an operator under attack can change none of them without a code change |
| RL-F3 | Declines are not counted | No failed-attempt counter in `C/Checkout.Core`, `A/Checkout.Abstractions` or `C/Transactions.Core` | The limiter counts requests, not failures. Twenty declined cards in a row look the same as one slow buyer |
| RL-F4 | Rate-limit groups are tagged but nothing limits them | Tagged: `checkout`, `checkout-payment` (`A/Checkout.Abstractions/CheckoutConstants.cs:141-152`), `Cart` (`C/Carts.Core/CartConstants.cs:16`), `find-order` (`C/Orders.Core/OrderConstants.cs:54`). Defined and never applied: `wizard` (`A/Wizard.Abstractions/WizardConstants.cs:78`). The only `AddGroupRateLimit` calls in the solution are in Addresses, AI.WebMcp and NLWeb | With `OrchardCore.RateLimits` on, commerce endpoints get only the default global policy (1,000 requests a minute per client). With it off they get nothing from it |
| RL-F5 | No commerce feature depends on, enables or mentions `OrchardCore.RateLimits` | No `Manifest.cs` or recipe names `OrchardCoreFeatureIds.RateLimits` | A store can run in production without the limiter middleware and nothing tells the operator |
| RL-F6 | "Begin payment" and "payment status" share one group | `PaymentEndpoints.cs:36,42` and `CheckoutApiEndpoints.cs:57,66` all carry `checkout-payment` | A group policy tight enough to matter for payments would break status polling, so the group cannot be used as it is |
| RL-F7 | The limiter is a generic attempt counter with a payment name, owned by Checkout and used by other modules | `IPaymentAttemptLimiter` is called for referral links (`M/Customers/Controllers/ReferralController.cs:108`), order lookup (`M/Orders/Controllers/OrderViewController.cs:127,167-168`), guest account creation (`M/Orders/Controllers/OrderAccountController.cs:78`), gift cards (`M/Orders/Services/GiftCardCheckoutService.cs:65`) and coupons (`C/Checkout.Core/Services/CheckoutCouponService.cs:76`); 16 files in all | Customers and Orders depend on Checkout for something that is not checkout; `M/Users/Services/EmailSignInCodes.cs:88-113` copied the algorithm without its lock (SEC-P1) |
| RL-F8 | Money and secret endpoints with no throttle and no group | `M/Subscriptions/Endpoints/InstallmentPlanPaymentEndpoints.cs:29,33` (begin and status), `M/Shipping/Api/ShippingCheckoutApiEndpoints.cs:31` (each quote is a paid carrier call), `M/Checkout/Controllers/DiscountLinkController.cs:42` (says whether a code is redeemable), `M/PointOfSale/Api/PosApiEndpoints.cs:72`, `M/AgenticCommerce/Acp/AcpEndpoints.cs:55-59`, `M/AgenticCommerce/Ucp/UcpEndpoints.cs:61-65`, `M/Stripe/Endpoints/CreateWebhookEndpoint.cs:65`, `M/Shipping/Endpoints/EasyPostWebhookEndpoint.cs:26`, `M/Downloads/Controllers/DownloadsController.cs:17` | See the group table in section 3. Store credit, points and the B2B terms account spend the caller's own balance and validate no secret: they need only the general `checkout` group |
| RL-F9 | The client key is the raw remote address | `PaymentEndpoints.cs:215`, `CheckoutApiEndpoints.cs:268-269`, `OrderViewController.cs:127,167` | An IPv6 client rotates addresses inside its /64 and never repeats a key. Behind a proxy without forwarded headers, every visitor shares one key and the store throttles itself |
| RL-F10 | Rejections have three shapes and no `Retry-After` | `PaymentEndpoints` returns `CheckoutErrorResponse`, the API returns `ApiProblems`, `OrchardCore.RateLimits` returns an empty 429 | The storefront script and API clients cannot tell the buyer when to retry |
| RL-F11 | The limiter is scoped per tenant, fails closed and is tested | `C/Checkout.Core/Services/PaymentAttemptLimiter.cs:50` (tenant name in the key), `:57-62` (no lock: refuse); `tests/CrestApps.OrchardCore.Tests/Checkout/PaymentAttemptLimiterTests.cs`, `Commerce/CommerceTenantIsolationTests.cs:217` | Keep these properties. It holds across nodes only when the tenant uses a shared cache and lock (Orchard's Redis cache and Redis lock features); otherwise `IDistributedCache` and `IDistributedLock` are per node |

Why two layers: `OrchardCore.RateLimits` uses ASP.NET Core's limiter, which counts in one node's memory and sees only the request (route, user, address). It cannot know that a payment was *declined*. The attempt counter knows outcomes and, with a shared cache, holds across nodes, but it runs after routing and model binding. Each layer gets one job.

## 2. Design

### 2.1 Where the pieces live

No new module. The reuse ladder puts platform infrastructure in `CrestApps.OrchardCore.Abstractions` and `CrestApps.OrchardCore.Core`, beside `SignedTokenService` and `EgressNetwork`.

| Piece | Home | Notes |
| --- | --- | --- |
| `IAttemptLimiter`, `AttemptLimitPolicy`, `AttemptLimitResult`, `AttemptLimitOptions` | `A/Abstractions/Throttling` (namespace `CrestApps.OrchardCore.Throttling`) | The contract other modules use |
| `DistributedAttemptLimiter`, `ClientNetworkKey`, `services.AddAttemptLimiter()` (registered with `TryAdd`) | `C/Core/Throttling` | Needs only `IDistributedCache`, `IDistributedLock`, `IClock` and `ShellSettings` |
| `AddClientGroupRateLimit`, `AddJsonRejectionHandler` (extensions on `RateLimitsOptions`) | `C/Core/Throttling` | `C/Core` takes a package reference to `OrchardCore.RateLimits.Core` (a library, not a module). Called only from startups that require the Rate Limits feature |
| `PaymentVelocityGate`, `PaymentDeclineRecorder`, the attempt policies for payments | `C/Checkout.Core`, registered by the Checkout feature | Always on; no middleware needed |
| The "Payment Rate Limits" feature | `M/Checkout/Manifest.cs`, id `CheckoutConstants.Features.RateLimits` (`CrestApps.OrchardCore.Checkout.RateLimits`), category Commerce, `Dependencies = [CheckoutConstants.Features.Area, OrchardCoreFeatureIds.RateLimits]` | The extension of `OrchardCore.RateLimits`: enabling it enables the limiter middleware, registers Checkout's built-in group limits and rejection handlers, and seeds the stored policies in 3.4 |
| Built-in group limits of other modules (Orders, Carts, Shipping, Subscriptions, AgenticCommerce, Downloads, Point of Sale, Stripe) | Each module's own `[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]` startup, as Addresses does today (`M/Addresses/Startup.cs:53-62`) | Each module owns its groups |

Contracts:

```csharp
namespace CrestApps.OrchardCore.Throttling;

public interface IAttemptLimiter
{
    // Counts one attempt against the named policy and key. Refuses when the policy's window is full
    // or the key is blocked. Fails closed when the counter cannot be reached.
    Task<AttemptLimitResult> AcquireAsync(string policyName, string key, CancellationToken cancellationToken = default);

    // Reads the state without counting (for example, to decide whether to show a challenge).
    Task<AttemptLimitResult> PeekAsync(string policyName, string key, CancellationToken cancellationToken = default);

    // Counts one failure (a decline, a wrong code). When the policy's limit is reached the key is
    // blocked for the policy's BlockDuration.
    Task<AttemptLimitResult> RecordFailureAsync(string policyName, string key, CancellationToken cancellationToken = default);

    // Clears the counter after a success, for policies that set ResetOnSuccess.
    Task ResetAsync(string policyName, string key, CancellationToken cancellationToken = default);
}

public sealed class AttemptLimitPolicy
{
    public string Name { get; set; }
    public int PermitLimit { get; set; }          // <= 0 disables the policy
    public TimeSpan Window { get; set; }
    public TimeSpan BlockDuration { get; set; }   // zero: refuse until the window ends
    public bool ResetOnSuccess { get; set; }
}

public readonly record struct AttemptLimitResult(bool Allowed, int Remaining, TimeSpan RetryAfter);

public sealed class AttemptLimitOptions
{
    public IDictionary<string, AttemptLimitPolicy> Policies { get; }
    public AttemptLimitOptions AddPolicy(string name, int permitLimit, TimeSpan window, TimeSpan blockDuration = default, bool resetOnSuccess = false);
}
```

Rules for the implementation:

- The cache key is `{tenant}:AttemptLimit:{policy}:{SHA-256 of the key, base64url}`. Hashing keeps e-mail addresses and codes out of cache keys and bounds the key length.
- Counting stays under a short distributed lock, as today, and still refuses when the lock cannot be taken (`PaymentAttemptLimiter.cs:57-62`). Use `IClock` for time.
- A policy that is not registered throws `InvalidOperationException` (a missing policy must not mean "unlimited").
- `AttemptLimitOptions` is bound from the shell configuration section `CrestApps_Throttling:AttemptPolicies` after the code registrations, so an operator can override any policy by name without a deployment. Add the section to the Checkout module's `ConfigurationSchema.json` and document it.
- Telemetry through `CrestAppsOrchardCoreTelemetry.CreateMeter`: a counter of refusals tagged with the policy name only. Never tag the key, an address or an e-mail (see `BannedMetricTagKeys`).
- Document that the counters hold across nodes only with a shared cache and lock (RL-F11).

`ClientNetworkKey.NetworkOnly(HttpContext)` returns the IPv4 address or the IPv6 address masked to /64, read through `RateLimitPartitionHelpers.GetRemoteIpAddress` so proxy handling stays in one place. `ClientNetworkKey.From(HttpContext)` returns the signed-in user's key through `RateLimitPartitionHelpers.GetClientPartitionKey`, and the network key for an anonymous request. Payment limits use `NetworkOnly` even for a signed-in buyer, because a card tester can register an account.

```csharp
// A built-in group limit partitioned by ClientNetworkKey; shows read-only under Tools → Rate Limits.
public static RateLimitsOptions AddClientGroupRateLimit(this RateLimitsOptions options, string groupName, int permitLimit, TimeSpan window, bool networkOnly = false);

// One problem+json 429 body with a Retry-After header for the named routes.
public static RateLimitsOptions AddJsonRejectionHandler(this RateLimitsOptions options, params string[] routeNames);
```

`AddRouteRejectionHandler` in Orchard Core is per route name, so every limited endpoint needs a stable route name (`WithName` or `Name =`); RL-3 adds the missing ones.

### 2.2 Feature dependency: optional, but visible

Checkout does not take `OrchardCore.RateLimits` as a hard dependency. A hard dependency would enable the limiter middleware on every existing store at upgrade and seed Orchard's **Default Global Policy** (1,000 requests per 60 seconds per client). A store behind a proxy that has not configured forwarded headers would then count all its visitors as one client and throttle its whole site.

What is used:

- The card-testing gate and the attempt policies (section 3.2 and 3.3) are registered by the Checkout feature itself and need no middleware. A store is protected against card testing whether or not Rate Limits is on.
- The request layer is the **Payment Rate Limits** feature (2.1). The store recipes enable it for new stores (RL-7).
- A store setup step, "Protect payments" (an `IStoreSetupStep`, the contract that exists today), is incomplete while the Payment Rate Limits feature is off, and warns when the request that renders it carries an `X-Forwarded-For` header while Orchard's Reverse Proxy feature is off (RL-6).

### 2.3 Two layers, one owner each

| Layer | Mechanism | Stops | Counted per | Always on |
| --- | --- | --- | --- | --- |
| Attempt | `IAttemptLimiter` policies and the payment gate, called from the service that knows the outcome | Card testing, code guessing, enumeration | Network, checkout, customer, code target, store | Yes |
| Request | Built-in group limits registered through `RateLimitsOptions`, and stored Group policies an administrator adds | Floods, scripted loops, polling storms | Client (user, or network when anonymous) | When the Payment Rate Limits feature (or Rate Limits) is on |

## 3. Groups, limits and owners

Every group constant lives in its owner's constants class. Existing group names are not renamed (`Cart` stays `Cart`: a rename would stop stored policies matching, for a cosmetic gain). New groups use lower-case kebab case. The defaults are starting points; each is a named constant next to its group and can be overridden (request limits by an administrator's stored Group policy, attempt policies by configuration). They are deliberately loose per network, because carrier-grade NAT, offices and campuses put many honest buyers behind one IPv4 address; the decline counters are the real signal.

### 3.1 Request layer (built-in group limits)

| Group (constant) | Owner | Endpoints | Default | Partition |
| --- | --- | --- | --- | --- |
| `checkout-payment` (`CheckoutConstants.RateLimitGroups.Payment`) | Checkout | `POST checkout/{sessionId}/payment/begin` (`PaymentEndpoints.cs:32`), `POST api/.../checkout/{id}/payment` (`CheckoutApiEndpoints.cs:54`) | 30 per 5 minutes | Network only |
| `checkout-payment-status` (new: `RateLimitGroups.PaymentStatus`) | Checkout | `POST checkout/{sessionId}/payment/status` (`PaymentEndpoints.cs:38`), `POST .../checkout/{id}/complete` (`CheckoutApiEndpoints.cs:64`) | 60 per minute | Client |
| `checkout-start` (new: `RateLimitGroups.Start`) | Checkout | Session creation: `CartCheckoutController` POST (`M/Orders/Controllers/CartCheckoutController.cs:33`), `POST api/.../checkout` (`M/Orders/Api/OrderCheckoutApiEndpoints.cs:23`, today tagged `Cart`), subscription checkout start (`M/Subscriptions/Controllers/SubscriptionsController.cs:66`) | 30 per 10 minutes | Network only |
| `checkout` (`RateLimitGroups.Checkout`) | Checkout | Checkout page and steps (`M/Checkout/Controllers/CheckoutController.cs:83,126`); contact, shipping, B2B, store credit, points and terms-account endpoints | 120 per minute | Client |
| `checkout-code` (new: `RateLimitGroups.Code`) | Checkout | Coupon apply (`CheckoutApiEndpoints.cs:34`), gift card apply (`OrderCheckoutApiEndpoints.cs:38`), `GET discount/{code}` (`DiscountLinkController.cs:42`), and the checkout page's posts that apply a code | 20 per 5 minutes | Client |
| `checkout-quote` (new, `ShippingConstants.RateLimitGroups.Quote`) | Shipping | `POST checkout/{id}/shipping-rates` (`ShippingCheckoutApiEndpoints.cs:31`) | 30 per minute | Client |
| `Cart` (`CartConstants.RateLimitGroup`) | Carts | `CartApiEndpoints.cs:33,40,48`, `CartController.cs:85,91,134,140` | 120 per minute | Client |
| `find-order` (`OrderConstants.FindRateLimitGroup`) | Orders | `OrderViewController.cs:118,153` | 10 per 10 minutes | Network only |
| `installment-payment` (new) | Subscriptions | Installment `BeginPayment` and `PaymentStatus` (`InstallmentPlanPaymentEndpoints.cs:29,33`) | 60 per minute | User |
| `agent-checkout` (new, `AgenticCommerceConstants.RateLimitGroups.Checkout`) | AgenticCommerce | ACP and UCP create, update, cancel (`AcpEndpoints.cs:55-59`, `UcpEndpoints.cs:61-65`, both MCP routes, the A2A route) | 120 per minute | Agent identity (the verified key id), else network |
| `agent-checkout-complete` (new) | AgenticCommerce | ACP and UCP `complete` | 30 per 5 minutes | Agent identity, else network |
| `payment-webhook` (new, `PaymentConstants.RateLimitGroups.Webhook` in `A/Payments.Abstractions`) | Stripe, Shipping | `POST stripe/webhook` (`CreateWebhookEndpoint.cs:65`), EasyPost webhook (`EasyPostWebhookEndpoint.cs:26`) | 600 per minute | Network only |
| `download` (new) | Downloads | `DownloadsController` | 60 per minute | Client |
| `pos-payment` (new) | PointOfSale | `POST pos/sales/{checkoutId}/pay` (`PosApiEndpoints.cs:72`), `pos/returns` (`:90`) | 60 per minute | User |

The endpoint table in `01-security-findings.md` (Commerce endpoints that move money or validate a secret) is the source for any endpoint this table misses; RL-3 ends with a test that fails when such an endpoint has no group.

### 3.2 Attempt layer (`IAttemptLimiter` policies)

| Policy name | Registered by | Key | Counts | Default | Replaces |
| --- | --- | --- | --- | --- | --- |
| `payment-begin-network` | Checkout | Network | Each begin-payment call, across all checkouts | 30 per 10 minutes | `checkout-begin`, `api-checkout-begin` |
| `payment-begin-checkout` | Checkout | Checkout session id | Each begin-payment call in one checkout's lifetime | 8 per 24 hours | New |
| `payment-decline-network` | Checkout | Network | Each declined or failed card payment | 5 per 30 minutes, then blocked 30 minutes | New |
| `payment-decline-customer` | Checkout | Customer id (signed-in buyers only) | Each declined or failed card payment | 5 per hour, then blocked 1 hour | New |
| `payment-decline-store` | Checkout | The tenant | Each declined or failed card payment, store-wide | 30 per 10 minutes, then the action in 3.3 for 30 minutes | New |
| `payment-status` | Checkout | Network + checkout | Status polls | 120 per 5 minutes | `checkout-status`, `api-checkout-complete` |
| `checkout-start-network` | Checkout | Network | Checkouts started | 60 per hour | New |
| `coupon-code` | Checkout | Network | Wrong codes only | 10 per 10 minutes, then blocked 10 minutes | `coupon-code` |
| `gift-card-code` | Orders | Network | Wrong codes only | 5 per 10 minutes, then blocked 30 minutes | `gift-card-code` |
| `order-lookup-network`, `order-lookup-email` | Orders | Network; e-mail | Lookups | 10 per 10 minutes; 5 per hour | `track-order`, `find-order`, `find-order-email` |
| `order-create-account` | Orders | Network | Attempts | 5 per hour | `order-create-account` |
| `referral-link` | Customers | Network | Attempts | 20 per hour | `referral-link` |
| `webhook-signature-failure` | Stripe, Shipping | Network + provider | Requests whose signature fails | 20 per 5 minutes, then blocked 15 minutes | New |

`payment-decline-customer` is keyed by customer id only. Keying it by the contact e-mail would let anyone lock a real customer out by typing their address, while a tester simply rotates addresses.

### 3.3 The card-testing gate

A new `PaymentVelocityGate : ICheckoutPaymentGate` in `C/Checkout.Core/Handlers/`, registered first in `M/Checkout/Startup.cs` (before `CouponHoldPaymentGate` at line 92, so a refused attempt holds no coupon or tender). `ICheckoutPaymentGate.BeginningAsync` runs inside the checkout's lock before any provider is contacted (`A/Checkout.Abstractions/Services/ICheckoutPaymentGate.cs:10-13`), so the gate covers every channel that pays through `ICheckoutEngine`: the browser, the commerce API, agents and installments.

- It reads the network key from a new `CheckoutFlow` property that the endpoint sets when the checkout is started and refreshes on each begin-payment call (`ClientNetworkKey.NetworkOnly`), not from `IHttpContextAccessor` inside the engine, so background and agent paths work. Agent checkouts use the verified agent key id.
- It refuses (`context.Refuse(...)`, `ICheckoutPaymentGate.cs:66`) when `payment-decline-network` or `payment-decline-customer` is blocked, or when `payment-begin-network` or `payment-begin-checkout` has no permit. The message is one localized sentence that does not say which limit was hit: "We can't take another payment attempt right now. Please try again later or use another way to pay."
- It skips checkouts where nothing is paid (`context.ProviderKey` is null) and providers that take no card online: add `IsCardNotPresent` to `PaymentProviderCapabilities` (true for Stripe and any other online card provider; false for cash, terms, store credit, offline methods and the Point of Sale counter).

Counting declines: a new `PaymentDeclineRecorder` called where the engine learns a card payment failed. Find every place `DefaultCheckoutEngine`, `CheckoutReconciliationService` and the payment event handlers move a `PaymentAttempt` to `Failed` (a verification result, a provider event, a 3-D Secure failure) and call `RecordFailureAsync` for the three decline policies there, once per attempt: guard on the attempt's state transition so a replayed webhook does not count twice.

When a network or customer policy first blocks a key:

- record a `PaymentIssue` through `IPaymentIssueRecorder` (`A/Transactions.Abstractions/Services/IPaymentIssueRecorder.cs:19`), kind `SuspectedCardTesting`, with the masked network, the number of declines and the checkout ids, de-duplicated per key and block window, so it appears under **Commerce > Payment issues**;
- write an Audit Trail event when Audit Trail is enabled (`[RequireFeatures]` startup);
- log at warning level with `SanitizeLogValue`.

**Store-wide signal.** A tester who rotates through residential proxies defeats every per-network key. `payment-decline-store` counts declines for the whole store. When it trips, the gate applies `PaymentProtectionSettings.OnDeclineSpike` for the block duration and records one `PaymentIssue` (kind `DeclineSpike`):

| Value | Effect |
| --- | --- |
| `Notify` (default) | Record the issue only |
| `RequireSignIn` | Guest card payments are refused with "Please sign in to pay by card"; signed-in buyers continue |
| `PauseGuestCards` | Guest card payments are refused; other payment methods continue |

A challenge (Orchard's reCAPTCHA) is the natural next option; `PeekAsync` is the hook for it. It is not in this plan.

`PaymentProtectionSettings` is a site settings group under the store's settings (one driver, the `OnDeclineSpike` choice and a link to **Tools → Rate Limits**), with a recipe step and a deployment step as `building-blocks.md` section 4 requires.

### 3.4 Managing limits where administrators already look

- Built-in group limits appear read-only under **Tools → Rate Limits** automatically.
- The Payment Rate Limits feature's recipe step adds stored, editable Group policies with `CreateOrUpdateRateLimitPolicies` for `checkout-payment`, `checkout-start`, `checkout-code` and `find-order`, disabled by default and described ("Enable when the store is under a card-testing attack"), so an operator can turn on a stricter limit in one click without knowing group names.
- The attempt policies are listed, with their defaults and the configuration key that overrides each, on the docs page (RL-7).

## 4. Work items

Each item is one commit, test first (see *Test-Driven Development* in `AGENTS.md`). Build only the changed projects and run only the tests that cover them.

### RL-0. Hotfix: stop the per-checkout reset (S, ship first and alone)

Change the key in `PaymentEndpoints.cs:215` and `CheckoutApiEndpoints.cs:271-272` so begin-payment is counted by network only (keep the session in the key for status polling). With today's single limit of 20 per 5 minutes this caps a network at 20 begin-payment calls across all its checkouts. Nothing else changes. This closes SEC-C7's main gap without waiting for the building block.

Tests: extend `PaymentAttemptLimiterTests` and the endpoint tests: the 21st begin from one address across 21 checkouts is refused; status polling in another checkout is unaffected.

### RL-1. The attempt limiter as platform infrastructure (M)

1. Add the contracts to `A/Abstractions/Throttling` and the implementation to `C/Core/Throttling` (2.1). Move `PaymentAttemptLimiter` there as `DistributedAttemptLimiter` and generalize it: named policies, failures, blocks, `PeekAsync`, `ResetAsync`, `RetryAfter`, cancellation.
2. Keep `IPaymentAttemptLimiter` for one release as an `[Obsolete]` adapter in `C/Checkout.Core` that maps `AcquireAsync(scope, discriminator)` to `IAttemptLimiter`, with every old scope registered as a policy of 20 per 5 minutes, so the public API changes by addition only. Mark `PaymentRateLimitOptions` obsolete.
3. Add `ClientNetworkKey` with tests for IPv4, IPv6 (/64), IPv4-mapped IPv6, a missing address (`"unknown"`), and an authenticated user.
4. Add `AddClientGroupRateLimit` and `AddJsonRejectionHandler`; move the three identical registrations in `M/Addresses/Startup.cs:58`, `M/AI.WebMcp/Startup.cs:83` and `M/NLWeb/Startup.cs:118` onto the first.
5. Add the catalog row to `docs/engineering/building-blocks.md` (Platform infrastructure: "Attempt limits and rate-limit groups") and the rule to `AGENTS.md`: "Never write another attempt counter or 429 response; register an attempt policy or a group limit."
6. Add a ratchet to `DuplicatePatternRatchetTests`: no new `Status429TooManyRequests` outside `C/Core/Throttling`, `ApiProblems`, the AgenticCommerce protocol results and the provider ingress limiter; no `RemoteIpAddress?.ToString()` outside `ClientNetworkKey` in commerce projects.

Tests (`tests/CrestApps.OrchardCore.Tests/Throttling/`): port `PaymentAttemptLimiterTests`; window expiry; block duration; failure counting; reset on success; an unregistered policy throws; a lock that cannot be taken refuses; two tenants do not share a counter (extend `CommerceTenantIsolationTests`); a configuration override by policy name.

### RL-2. Checkout's policies, groups and the Payment Rate Limits feature (M)

1. `M/Checkout/Manifest.cs`: add the feature in 2.1. `M/Checkout/Startup.cs`: register the attempt policies Checkout owns (always). A new `[Feature(CheckoutConstants.Features.RateLimits)]` startup registers Checkout's group limits and `AddJsonRejectionHandler` for the payment, status, start and code route names.
2. Split the status endpoints into `checkout-payment-status` (RL-F6); add `RateLimitGroups.PaymentStatus`, `Start` and `Code` to `CheckoutConstants`.
3. Replace the limiter calls in `PaymentEndpoints.cs:68,137` and `CheckoutApiEndpoints.cs:195,235` with the policies `payment-begin-network` and `payment-status`. Remove `BuildDiscriminator`, `GetDiscriminator` and `GetClientAddress`.
4. Return `Retry-After` on every 429 from these endpoints, from `AttemptLimitResult.RetryAfter`.

Tests: endpoint unit tests with a fake `IAttemptLimiter`. Integration (`tests/CrestApps.OrchardCore.Commerce.IntegrationTests`, new `PaymentRateLimitTests.cs`): the begin-payment call after the limit, from one address across different checkouts, gets 429 with `Retry-After` and a JSON body; status polling in one checkout succeeds while begin is blocked; a second tenant is unaffected; with the feature off the attempt policy still refuses.

### RL-3. Tag every money-moving and secret-validating endpoint (M)

1. Apply the groups in 3.1 to the endpoints listed there. Give each a stable route name where it has none.
2. Each owner module registers its group's built-in limit in a `[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]` startup with `AddClientGroupRateLimit`.
3. Add `EndpointRateLimitGroupArchitectureTests` in `tests/CrestApps.OrchardCore.Tests`: build the endpoint data source for the commerce features (the feature-activation test host does this for Contact Center) and fail when an endpoint that is anonymous or antiforgery-exempt, or whose route name is in a "moves money" list, carries no `IRateLimitGroupMetadata` (the metadata type in the `OrchardCore.RateLimits.Core` package that `[RateLimitGroup]` and `WithRateLimitGroup` add). Start with an allowlist that may only shrink.

### RL-4. The card-testing gate, decline counting and the store-wide signal (M)

Implement 3.3. Files: `C/Checkout.Core/Handlers/PaymentVelocityGate.cs`, `C/Checkout.Core/Services/PaymentDeclineRecorder.cs`, `A/Checkout.Abstractions/Services/PaymentProviderCapabilities.cs` (`IsCardNotPresent`), `CheckoutFlow` (network key), `PaymentProtectionSettings` with its driver, recipe step and deployment step, `M/Checkout/Startup.cs`, `M/Stripe/Services/StripeCheckoutPaymentProvider.cs` (capability), the two `PaymentIssue` kinds and their admin labels.

Tests: the gate refuses after five recorded declines from one network and the provider is never called (assert on a fake provider); a successful payment by another network is unaffected; a replayed failure event counts once; cash, terms, store-credit and counter checkouts skip the gate; one payment issue per block window; an agent checkout uses the agent key; a guest's declines do not block a customer by e-mail; the store-wide policy trips at its limit and each `OnDeclineSpike` value behaves as its row says. Integration: with the Test payment provider set to decline, the sixth attempt across six checkouts returns the refusal message and a payment issue exists.

### RL-5. Move the remaining callers and delete the old limiter (S)

Move coupons, gift cards, order lookup, guest account creation and referral links to their policies in 3.2. Coupons and gift cards change from "count every try" to "count wrong codes": call `PeekAsync` before the lookup and `RecordFailureAsync` on a miss. `GET discount/{code}` (`DiscountLinkController.cs:42`, SEC-C17a) uses the same `coupon-code` policy and answers the same for a wrong and an unusable code. Customers and Orders then use the platform contract instead of a Checkout one. Remove `IPaymentAttemptLimiter`, `PaymentAttemptLimiter` and `PaymentRateLimitOptions` in the release after RL-1.

### RL-6. Proxy and address correctness (S)

1. Document in the commerce docs and the Checkout `README.md` that a store behind a proxy, load balancer or CDN must enable Orchard Core's Reverse Proxy feature and trust only its own proxies; link the `orchardcore-reverse-proxy` skill.
2. Add the "Protect payments" store setup step as an `IStoreSetupStep` (2.2). When the setup-guide building block (DRY-9) exists it becomes a provider of that; this item does not wait for it.
3. `ClientNetworkKey` is the only place an address becomes a key (RL-F9), enforced by the ratchet in RL-1.

### RL-7. Recipes and documentation (S)

1. Store recipes: enable the Payment Rate Limits feature (which enables `OrchardCore.RateLimits`) and add the stored Group policies of 3.4 (disabled).
2. Docs: a "Rate limits and card-testing protection" page under `src/CrestApps.Docs/docs/` (what is limited, the defaults, how to tune, how to read a Payment issue, the proxy requirement, the shared-cache requirement for multi-node sites); a changelog entry; module READMEs; `ConfigurationSchema.json` for `CrestApps_Throttling`.
3. Skill: `.agents/skills/crestapps-rate-limits/SKILL.md` (how to add a group limit, an attempt policy and a rejection handler).

### RL-8. Other consumers (S each; tracked in workstreams SEC and DRY)

Payments is the first consumer. These follow, each with its own finding. None of them has to wait for RL-1: each finding carries a local fix, and moves onto the shared limiter afterwards.

| Consumer | Finding | What it uses |
| --- | --- | --- |
| E-mail code sign-in (`M/Users/Services/EmailSignInCodes.cs:88-113`, a copy of the limiter without the lock) | SEC-P1 | Attempt policies for sends and verifies |
| Public booking page | SEC-P9 | A `scheduling-booking` group and an attempt policy per network |
| AI completion API and document upload | SEC-A5 | Groups on the routes; a quota per API key as an attempt policy |
| Wizard start (the `wizard` group constant is never applied) | SEC-A18 | Apply the group and register a built-in limit |
| Kiosk PIN lockout (`DefaultTimeClockService.cs:386-419`) | DRY-1 | An attempt policy with a block duration |
| The three identical group registrations in Addresses, AI.WebMcp and NLWeb | DRY-1 | `AddClientGroupRateLimit` (RL-1 step 4) |

Not consumers (they stay as they are): `C/Webhooks.Core/Services/ProviderWebhookIngressLimiter.cs` (a concurrency gate and per-provider token buckets, a different job), the AI chat rate-limit tiers (enforced in the CrestApps.Core package), the dialer's attempt limit (a dialing rule), and `AgentCheckoutService.cs:147` (a quota on open checkouts).

Dropped after review: a distributed `IRateLimiterSource` for stored policies. It would add a cache and lock round trip to the limiter path of every request, the attempt layer already covers the money paths across nodes, and without a shared cache it would be per node anyway.

## 5. Acceptance for the workstream

- With a store recipe applied, a run of declined cards from one network across many checkouts is stopped by the gate at the sixth decline, and a payment issue names the event. A run spread across many networks trips the store-wide signal.
- With the Payment Rate Limits feature off, the gate and the attempt policies still work.
- Every endpoint in 3.1 carries a group, and the architecture test proves no new anonymous or money-moving commerce endpoint ships without one.
- `git grep -n "IPaymentAttemptLimiter"` returns only the obsolete adapter (then nothing, after RL-5's removal).
- **Tools → Rate Limits** lists every built-in commerce limit; the docs page explains each limit and each attempt policy.
- No metric, log or cache key contains a raw address, e-mail or code.

## 6. Open decisions for the owner

1. **Dependency.** This plan recommends the Payment Rate Limits feature (which depends on `OrchardCore.RateLimits`) enabled by recipe and flagged by a setup step, with the gate always on. The alternative is a hard dependency of Checkout on `OrchardCore.RateLimits`, which protects every store but changes the traffic handling of existing stores at upgrade (2.2).
2. Whether payment limits count the network even for signed-in buyers (recommended) or the user.
3. The default action when the store-wide decline signal trips (`Notify` is recommended as the default; `RequireSignIn` is the stronger choice for stores that have accounts).
4. The default numbers in section 3.

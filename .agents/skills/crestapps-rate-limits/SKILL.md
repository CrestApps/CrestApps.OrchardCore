---
name: crestapps-rate-limits
description: >
  Adds or changes a request limit or an attempt limit in CrestApps.OrchardCore: a request group of Orchard Core's
  Rate Limits on a route (WithRateLimitGroup or [RateLimitGroup], a built-in limit with AddClientGroupRateLimit in a
  startup that requires the Rate Limits feature, a JSON 429 with AddJsonRejectionHandler), or an attempt policy of the
  platform IAttemptLimiter (AddAttemptPolicy, AcquireAsync, RecordFailureAsync, PeekAsync, ResetAsync) for counting
  payments, declines, wrong codes, sign-in codes or lookups by network, user or key. Covers ClientNetworkKey, the
  card-testing gate, IsCardNotPresent, configuration overrides under CrestApps_Throttling:AttemptPolicies, and the
  architecture test that fails when a guest-reachable commerce endpoint has no group. Use whenever a route can be
  called by a guest or another site, moves money, or checks a secret, and before writing any counter or 429 response.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps rate limits

There are two layers, and one owner for each. Never write another counter, cache-backed limiter or 429 response.

| Layer | Use it for | Owner |
| --- | --- | --- |
| **Request group** | How often a route can be called, per client and window | Orchard Core's Rate Limits feature; the built-in limit is registered by the module that owns the route |
| **Attempt policy** | Counting something that matters across routes and nodes: payments begun, declines, wrong codes, sign-in codes, lookups | `IAttemptLimiter` (`CrestApps.OrchardCore.Abstractions`, implemented in `CrestApps.OrchardCore.Core/Throttling`) |

## A request group on a route

1. Add the group name as a constant in the owner's constants class (lower-case kebab case, such as `checkout-quote`).
2. Tag the route and give it a stable name:

   ```csharp
   group.MapPost("checkout/{id}/shipping-rates", QuoteAsync)
       .AllowGuests()
       .WithRateLimitGroup(ShippingConstants.RateLimitGroups.Quote)
       .WithName("QuoteCheckoutShipping");
   ```

   An MVC action uses `[RateLimitGroup(...)]` and `[HttpPost("...", Name = ...)]`.
3. Register the built-in limit in a startup that only runs with the Rate Limits feature:

   ```csharp
   [RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
   public sealed class RateLimitsStartup : StartupBase
   {
       public override void ConfigureServices(IServiceCollection services)
           => services.Configure<RateLimitsOptions>(options => options
               .AddClientGroupRateLimit(ShippingConstants.RateLimitGroups.Quote, 30, TimeSpan.FromMinutes(1))
               .AddJsonRejectionHandler("QuoteCheckoutShipping"));
   }
   ```

   `networkOnly: true` counts a signed-in user by network too (payments, lookups, webhooks). Two modules may register
   the same group; the first registration stands. The limit is listed read-only under **Tools › Rate Limits**.
4. API routes get `AddJsonRejectionHandler(routeNames)` (problem+json with `Retry-After`).

`EndpointRateLimitGroupArchitectureTests` fails when a commerce endpoint that is anonymous, open to guests or
antiforgery-exempt has no group, and when a money-moving MVC route on its list has none.

## An attempt policy

```csharp
services.AddAttemptPolicy("gift-card-code", permitLimit: 10, window: TimeSpan.FromMinutes(10), blockDuration: TimeSpan.FromMinutes(10));
```

- `AcquireAsync(policy, key)` counts an attempt and refuses at the limit (an allowed result went ahead, the last permit
  included).
- `RecordFailureAsync(policy, key)` counts a failure and blocks the key for `blockDuration` at the limit. For codes:
  `PeekAsync` before the lookup, `RecordFailureAsync` on a miss, so a buyer who types the right code is never counted.
- `ResetAsync` clears a key (a policy with `resetOnSuccess`).
- A policy that isn't registered throws; a `PermitLimit` of 0 turns one off.
- A lock that can't be taken refuses. Keys are hashed and carry the tenant's name.
- Operators override any policy by name under `CrestApps_Throttling:AttemptPolicies:{name}`; call
  `AddAttemptPolicyOverrides(shellConfiguration.GetSection(AttemptLimitOptions.SectionName))` once in the owner's startup.

Keys: `ClientNetworkKey.NetworkOnly(httpContext)` (IPv4, or the /64 of IPv6), `ClientNetworkKey.From(httpContext)` (the
user, else the network), or an id such as a checkout's or a user's. `ClientNetworkKey` is the only place an address
becomes a key; never log or store a raw address, e-mail or code.

## Payments

- A payment provider where the buyer types a card online sets `PaymentProviderCapabilities.IsCardNotPresent = true`;
  only those are counted and stopped by the card-testing protection (`PaymentVelocityGate`, `PaymentDeclineRecorder`).
- A begin-payment endpoint passes `BeginPaymentOptions.ClientNetworkKey`; a staff or server-side charge leaves it empty.
- A new place that marks an attempt failed calls `IPaymentDeclineRecorder.RecordAsync` once, on the transition
  (`PaymentDeclineRecorder.IsNewDecline`).

## Behind a proxy

Every limit counts by address, so a site behind a proxy must enable Orchard Core's Reverse Proxy feature and trust only
its own proxies (see the `orchardcore-reverse-proxy` skill); the store setup guide's **Protect payments** step warns
when it isn't. Several servers need a shared cache and lock (Redis) for the counters to hold across nodes.

Docs: `src/CrestApps.Docs/docs/modules/commerce-rate-limits.md`.

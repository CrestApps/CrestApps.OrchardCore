---
sidebar_label: Rate limits and card testing
sidebar_position: 12
title: Rate limits and card-testing protection
description: How the commerce modules limit payment, code and lookup routes, stop card testing, and how to tune the limits.
---

Card testers run stolen card numbers through a store's checkout to learn which ones work, and guessers try discount and gift
card codes. The commerce modules defend against both in two layers, each with one owner:

| Layer | What it counts | Needs | Where you see it |
| --- | --- | --- | --- |
| **Attempt policies** (always on) | Declined card payments, payments begun, wrong codes, order lookups, by network, customer, checkout or store | Nothing; counters live in the distributed cache | Payment issues, logs and the `crestapps.throttling.refusals` metric |
| **Request groups** (Payment Rate Limits feature) | Requests to a route, per client, per window | Orchard Core's Rate Limits feature (enabled with it) | **Tools › Rate Limits** |

## The card-testing protection

Every store has it, with or without the Payment Rate Limits feature. It applies to payment methods where the buyer types a
card online (Stripe, and the Test payments provider); a card reader, cash, a cheque or Pay Later is never stopped.

- A network (an IPv4 address, or the /64 of an IPv6 address) with **5 declined card payments in 30 minutes** can't begin
  another card payment for 30 minutes.
- A signed-in customer with **5 declines in an hour** is stopped for an hour, from any network.
- One checkout can begin **8 payments in 24 hours**.
- When card payments are declined **30 times in 10 minutes across the store**, from any number of networks, the store's
  choice under **Settings › Commerce › Checkout › Payment protection** applies for 30 minutes:
  - *Record a payment issue only* (the default);
  - *Ask guests to sign in to pay by card*;
  - *Pause card payments for guests*.
  Signed-in customers always pay as usual.

A stopped buyer reads *We can't take another payment attempt right now. Please try again later or use another way to pay.*
The provider is never asked to charge the card.

The first decline that stops a network or a customer records a **Suspected card testing** payment issue, and the store-wide
limit records **Card declines across the store**. Both are listed under **Commerce › Payment issues**. Check the payment
provider's dashboard for the declines, and consider turning on the stronger store-wide action while it lasts.

Each decline is counted once, when its payment attempt becomes failed, whether the provider said so at once, through a
webhook, or when the reconciliation sweep asked.

## Attempt policies

Each policy has a name, and can be changed without a deployment in the tenant's configuration under
`CrestApps_Throttling:AttemptPolicies:{name}` (`PermitLimit`, `Window`, `BlockDuration`, `ResetOnSuccess`). A
`PermitLimit` of 0 turns a policy off.

| Policy | Default | Counts |
| --- | --- | --- |
| `payment-begin-network` | 30 per 10 minutes | Payments begun from one network |
| `payment-begin-checkout` | 8 per 24 hours | Card payments begun on one checkout |
| `payment-decline-network` | 5 per 30 minutes, then blocked 30 minutes | Declined card payments from one network |
| `payment-decline-customer` | 5 per hour, then blocked an hour | Declined card payments of one signed-in customer |
| `payment-decline-store` | 30 per 10 minutes, then 30 minutes | Declined card payments across the store |
| `payment-status` | 120 per 5 minutes | Status polls per network and checkout |
| `checkout-start-network` | 60 per hour | Checkouts started from one network |
| `coupon-code` | 10 wrong codes per 10 minutes, then blocked 10 minutes | Wrong discount codes from one network |

The Orders and Customers modules add their own for gift card codes, order lookups, guest accounts and referral links.

```json
{
  "OrchardCore": {
    "Default": {
      "CrestApps_Throttling": {
        "AttemptPolicies": {
          "payment-decline-network": { "PermitLimit": 3, "BlockDuration": "01:00:00" }
        }
      }
    }
  }
}
```

## Request groups (Payment Rate Limits)

Enable **Payment Rate Limits** (Commerce). It enables Orchard Core's Rate Limits feature and registers built-in limits for
the groups below; each is listed read-only under **Tools › Rate Limits**, where an administrator can add a stricter
policy for the same group. A refused API call gets a `429` problem+json body with a `Retry-After` header.

| Group | Routes | Default |
| --- | --- | --- |
| `checkout-payment` | Beginning a payment, in the browser and over the API | 30 per 5 minutes per network |
| `checkout-payment-status` | Payment status, completing a checkout | 60 a minute per client |
| `checkout-start` | Starting a checkout from the cart, the API or a subscription sign-up | 30 per 10 minutes per network |
| `checkout` | The checkout page and its steps, and the checkout API's reads and edits | 120 a minute per client |
| `checkout-code` | Applying a discount code or a gift card, and the discount link | 20 per 5 minutes per client |
| `checkout-quote` | Quoting shipping rates (Shipping) | 30 a minute per client |
| `Cart` | The cart forms and API (Carts) | 120 a minute per client |
| `find-order` | Find my order and Track your order (Orders) | 10 per 10 minutes per network |
| `installment-payment` | An installment plan's down payment (Subscriptions) | 60 a minute per user |
| `agent-checkout`, `agent-checkout-complete` | The ACP and UCP routes, MCP and A2A (Agentic Commerce) | 120 a minute; completing 30 per 5 minutes; per network |
| `payment-webhook` | The Stripe and EasyPost webhooks | 600 a minute per network |
| `download` | Download links (Downloads) | 60 a minute per client |
| `pos-payment` | Paying and refunding at the till (Point of Sale) | 60 a minute per user |

"Per client" counts a signed-in user by their account and a guest by their network.

The setup guide's **Protect payments** step is done once the feature is on.

## Behind a proxy, a load balancer or a CDN

Every limit counts by the client's address. Behind a proxy, every request comes from the proxy's address unless the
**Reverse Proxy** feature is on and trusts the proxy, and then the whole store shares one limit. Turn on Reverse Proxy and
list only your own proxies; the **Protect payments** step warns when a request carries `X-Forwarded-For` and the feature
is off.

## More than one server

The counters live in the tenant's distributed cache and are taken under its distributed lock. Across several servers, use
a shared cache and lock (Orchard Core's Redis features); otherwise each server counts on its own.

## For developers

- `IAttemptLimiter` (`CrestApps.OrchardCore.Throttling`) is the one attempt counter: `AcquireAsync` counts an attempt,
  `RecordFailureAsync` counts a failure and blocks at the limit, `PeekAsync` reads without counting and `ResetAsync`
  clears. Register a policy with `services.AddAttemptPolicy(name, permitLimit, window, blockDuration)`.
- `ClientNetworkKey` is the only place an address becomes a key.
- `RateLimitsOptions.AddClientGroupRateLimit(group, permitLimit, window, networkOnly)` registers a built-in group limit,
  from a startup that requires the Rate Limits feature, and `AddJsonRejectionHandler(routeNames)` answers a refusal with
  JSON. Tag the route with `.WithRateLimitGroup(group)` or `[RateLimitGroup(group)]` and give it a route name.
- A payment provider that takes a card online sets `PaymentProviderCapabilities.IsCardNotPresent`.
- An architecture test fails when a commerce endpoint a guest can call has no group.

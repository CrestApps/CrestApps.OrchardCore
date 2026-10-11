# CrestApps OrchardCore Stripe

The Stripe module is the payment provider that connects a Stripe account to the provider-agnostic payment
and checkout frameworks. Nothing outside this module knows Stripe exists.

## Why this module exists

Every gateway has its own vocabulary, its own money representation, and its own idea of what an
notification means. Left unchecked, that vocabulary leaks into the rest of an application and the site can
never change gateway or support two.

So the boundary is deliberately narrow, and a few decisions are enforced here rather than left to callers:

- **Money crosses the boundary once.** Stripe does not use a universal hundredths conversion: a zero-decimal
  currency such as JPY is exchanged in whole units and a three-decimal one such as KWD in thousandths that
  must be a multiple of ten. Every conversion goes through one helper, so no call site can overcharge a
  customer a hundredfold.
- **A webhook is a hint, not proof.** A notification can be replayed, delayed, or forged. Signatures are
  verified, event ids are recorded and de-duplicated under a distributed lock, and settlement is always
  confirmed against Stripe's own API.
- **Retries are safe.** Every write carries a deterministic idempotency key derived from the attempt, so a
  retried begin resumes the same PaymentIntent or subscription instead of charging the customer twice.

## Features

- **Stripe** (`CrestApps.OrchardCore.Stripe`) — the payment and recurring providers, the checkout card
  panel, the webhook endpoint, the connect flow, and the workflow events.

## What it provides to the checkout

| Capability | How |
| --- | --- |
| One-time payment | A PaymentIntent confirmed in the browser, verified server-side before anything is recorded as paid. |
| Recurring payment | A real Stripe subscription with the price defined **inline**, so any amount can be sold without synchronizing a price to Stripe first. |
| Verification | Reads the PaymentIntent or the subscription's latest invoice back from Stripe, so an agreement settles only when its first cycle is genuinely paid. |
| Cancellation | Voids an unsettled intent, refunds a settled one, or cancels the subscription when a checkout is rolled back. |
| Refunds | Through the durable refund service, never straight to the gateway. |

## Card details never reach the site

The card is entered into Stripe's own iframe and confirmed from the browser, which is what keeps the site
out of PCI scope. For a recurring agreement the browser tokenizes the card first and hands back only the
token, and the first invoice is then confirmed against that same payment method rather than attaching a
second one.

## Configuration

Under **Settings → Stripe**, supply the secret and publishable keys. Verifying the secret key provisions the
webhook automatically, so there is no separate endpoint to register by hand.

## Documentation

Full documentation: <https://docs.crestapps.com/docs/modules/payments>

## License

This project is licensed under the MIT License.

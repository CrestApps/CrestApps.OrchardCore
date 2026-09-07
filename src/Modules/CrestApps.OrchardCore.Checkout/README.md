# CrestApps OrchardCore Checkout

The Checkout module is the one place money moves. It owns the checkout session, the durable payment ledger,
the reconciliation that decides whether an obligation is really settled, and the customer-facing pages that
take a purchase from a cart to a receipt.

It is deliberately provider-agnostic and purchase-agnostic. Subscriptions, an outstanding balance being
settled online, and a future storefront all run through the same code, because the hard parts of taking a
payment are the same for all of them and getting them right twice is how they end up different.

## Why this module exists

Payment code goes wrong in a small number of expensive ways, and each one is a design decision rather than a
bug to fix later:

- **A charge that nothing knows about.** Contacting a gateway before writing anything down means a crash
  between the two leaves real money with no local record. So a durable attempt is written *first*, and the
  gateway's reference is stored the moment it comes back.
- **Trusting a notification.** A webhook can be replayed, delayed, or forged. Completion therefore asks the
  gateway's own API what happened rather than believing what arrived.
- **Half a purchase.** When one obligation settles and another fails, the customer has paid for something
  they will not get. The settled ones are refunded automatically.
- **Blocking a web request until the gateway decides.** That ties up a worker per customer and still times
  out behind a proxy. Completion returns immediately and is driven from the browser, a webhook, and a
  background sweep, any of which can finish the job.

## Features

- **Checkout** (`CrestApps.OrchardCore.Checkout`) — the checkout engine, the durable payment and refund
  ledgers, reconciliation, the public checkout pages, and the JSON endpoints the payment step uses.

## Key contracts

| Contract | Purpose |
| --- | --- |
| `ICheckoutEngine` | The single entry point for start, begin payment, complete, and cancel. Nothing else may call a provider or the ledger. |
| `ICheckoutHandler` | Contribute steps and billing items, and react to completion. |
| `ICheckoutPaymentProvider` | Take a one-time payment through a gateway. |
| `ICheckoutRecurringPaymentProvider` | Establish, change, and cancel a recurring agreement. |
| `ICheckoutPaymentRefundProvider` | Give money back at the gateway. |
| `ICheckoutRefundService` | The only way to issue a refund, so the ledger, tax allocation, and over-refund protection always apply. |

## Adding a step

Implement `ICheckoutHandler` to contribute the step and its billing items, then derive a display driver from
`CheckoutFlowDisplayDriver` and register it as an `IDisplayDriver<CheckoutFlow>`. The driver renders only
while its own step is current, so a step never has to know about the ones around it.

## Adding a payment provider

Implement `ICheckoutPaymentProvider`, register it, and add an `IDisplayDriver<CheckoutFlowPaymentMethod>`
grouped by your provider key for the panel the customer sees. The framework supplies the ledger,
reconciliation, idempotency, and compensation.

Declaring a capability you do not implement is refused rather than silently downgraded: a provider that
claims recurring support without an `ICheckoutRecurringPaymentProvider` cannot be selected for a
subscription, because the alternative is charging the customer once and never billing them again.

## Documentation

Full documentation: <https://docs.crestapps.com/docs/modules/checkout>

## License

This project is licensed under the MIT License.

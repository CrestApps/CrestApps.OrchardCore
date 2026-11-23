---
sidebar_label: Pay Later
sidebar_position: 13
title: Pay Later
description: Adds an offline "pay later" payment option to the Orchard Core Checkout framework, reusable by subscriptions and one-time goods purchases, and records the balance as a trackable outstanding transaction.
---

| | |
| --- | --- |
| **Feature Name** | Pay Later |
| **Feature ID** | `CrestApps.OrchardCore.PayLater` |
| **Category** | Commerce |
| **Dependencies** | `CrestApps.OrchardCore.Checkout`, `CrestApps.OrchardCore.Transactions` |

The **Pay Later** module contributes a single, offline payment option to the provider-agnostic [Checkout](checkout) framework. Enabling it makes a *Pay Later* method available to **any** checkout — recurring [Subscriptions](subscriptions) as well as one-time goods purchases — without wiring up an external gateway.

Because Pay Later is a standalone module rather than a per-scenario sub-feature, the same option is defined once and reused everywhere. Enable it alongside whatever purchase module you use, and the option appears automatically.

## What it does

Pay Later records an offline commitment instead of moving money through a processor. It flows through the **exact same durable payment ledger and reconciliation** as a real gateway, so the money-safety guarantees are identical:

- Its verification reports that it is **not** the authoritative source of a charged amount, so the checkout records the commitment on the strength of a recorded transaction id alone — without an amount cross-check.
- It never fabricates a *paid* record that a processor could later contradict.
- In non-production environments it records its commitments in `Testing` gateway mode, mirroring how a real gateway reports its mode.

This makes Pay Later suitable for manual/deferred billing, invoicing, purchase orders, or trials that are confirmed without an immediate card charge.

### Not for paying an existing balance

Pay Later sets `PaymentProviderCapabilities.DefersPayment`: it records a promise to pay, and collects nothing. That is a fine way to buy something new, because the promise becomes a balance to collect. It can't pay off a balance that already exists, though. If it could, paying an outstanding transaction "later" would mark the transaction paid with no money received.

So a checkout that settles an existing balance (`CheckoutSession.SettlesExistingBalance`, set by the Transactions module when a customer pays a transaction from **My Transactions** or a pay link) never offers Pay Later. The engine also refuses to begin a payment with it there. As a last guard, the settlement ignores any attempt from a provider that defers payment.

On the checkout payment step Pay Later contributes its own panel explaining what the customer is agreeing to. It collects nothing, but it is still a display driver rather than a special case in the checkout page, which is what keeps that page provider-agnostic.

## Tracking outstanding balances

:::note Applies to checkout purchases
Balance tracking runs on the [Checkout](checkout) framework's completion pipeline, so it applies to any purchase made through the public checkout. A **Pay Later subscription bought through the legacy [Subscriptions](subscriptions) signup flow does not yet create a transaction**, because that flow still records commitments through its own pipeline. A subscription bought through the public checkout does.
:::

Because Pay Later never moves money at a gateway, every succeeded Pay Later payment leaves a balance the customer still owes. Pay Later records that balance in the provider-agnostic [Transactions](transactions) ledger when the checkout completes, so an outstanding Pay Later commitment is a first-class, trackable obligation:

- The customer sees it on their **My Transactions** statement and can **pay it online** later (through the Checkout framework) once they are ready.
- An administrator sees it in the **Commerce → Transactions** report alongside every other outstanding balance, and can send a reminder, record an offline payment, mark it paid, cancel it, or add a note.
- The [Transactions](transactions) reminder pipeline chases unpaid Pay Later balances automatically on the configured cadence, through the notification channel each user prefers.

Recording is **idempotent per obligation**, so a checkout that completes more than once never duplicates the debt, and settlement checkouts (paying an existing transaction) never create a new balance.

### Recurring commitments

Pay Later can back a subscription as well as a one-off purchase. There is no gateway keeping the schedule, so the ledger carries it: a recurring commitment records the billing cycle it covers, when that period ends, which cycle number it is, and how many cycles the customer agreed to.

A sweep runs every thirty minutes and invoices the next period once the current one has ended, copying the amount and the tax forward rather than re-rating the agreement with today's rules. Without it a recurring Pay Later commitment would be invoiced exactly once and then quietly stop, leaving the customer with what they subscribed to and the site owner with nothing to chase.

Each cycle is created exactly once. The sweep takes a lock on the one commitment it advances, marks the cycle it came from as spawned before committing, and gives the new period its own obligation id, so a retry, a second node, or a restart cannot invoice the same period twice. A cancelled agreement, and one that has billed every cycle it was sold for, stop producing new periods.

### Configuring the payment term

A Pay Later balance falls due the way an invoice does, and the way B2B checkouts and accounting systems set terms: the most specific term wins. `PayLaterDueDateResolver` decides, for the first balance and for every renewal cycle:

1. **The payer's own payment term**, from their billing profile (`PartyBillingProfile.PaymentTermId`, such as an account that buys on Net 60). A guest has none.
2. **The payment term chosen for Pay Later** (`PayLaterSettings.PaymentTermId`), under **Settings → Commerce → Offline Payments → Payment term**. The field is shown when the [Payment Terms](transactions#payment-terms) feature is on, which Invoicing turns on.
3. **The net term (days)** (`PayLaterSettings.NetTermDays`, 30 by default): how many days after checkout, or after the start of the cycle, the balance is due. `0` records it with no due date.

A payment term gives a date (Net 15, the end of the month, a day of the next month), so the balance is due at the end of that day in the site's time zone, as an invoice is. A disabled or deleted term is skipped. Without the Payment Terms feature only the number of days applies, as before. A trial or a delayed start counts from the day its first real cycle begins. The due date drives the reminder cadence configured in the [Transactions](transactions) settings.

`OfflineCheckoutPaymentProvider.GetDueUtcAsync(DateTime now, PartyRef payer, CancellationToken)` is what the checkout handler asks; Pay Later overrides it, and Bank transfer and Cash on delivery keep their own number of days. `PayLaterSettings.Enabled` (off by default) offers the option at checkout; until it's on, enabling the feature adds nothing to checkout.

### With Invoicing

With [Invoicing](invoicing) on, every Pay Later balance gets an invoice that takes the debt over, so the customer receives a numbered invoice, and pays and is reminded about one amount:

- **An order's balance** gets the order's invoice, made from its lines ([Orders paid on account](invoicing#orders-paid-on-account)).
- **What another checkout left owing**, such as a subscription's first cycle or a membership, gets an invoice with one line for what was bought, over the period when it bills a cycle ([Deferred debts](invoicing#deferred-debts-renewals-and-other-checkouts)).
- **Each renewal cycle** gets an invoice for its period.

The invoice is due when the balance is, its number is the one the reminders give, and paying it settles the balance. An invoice never offers Pay Later as a way to pay it: a promise to pay later can't settle a balance (`DefersPayment`).

## Bank transfer and cash on delivery

The feature also ships two one-time offline methods on the same base, **`OfflineCheckoutPaymentProvider`**, which records a commitment without a gateway (`DefersPayment`, `ReportsAuthoritativeAmount = false`) and leaves a balance due when its settings say:

| Provider | Key | Available | Settings |
| --- | --- | --- | --- |
| `BankTransferCheckoutPaymentProvider` | `bank-transfer` | When `BankTransferSettings.Enabled` and its `Instructions` are set | `Enabled`, `Instructions` (the bank details), `NetTermDays` (default 7) |
| `CashOnDeliveryCheckoutPaymentProvider` | `cash-on-delivery` | When `CashOnDeliverySettings.Enabled` and the checkout has a `CheckoutShippingInfo` address | `Enabled`, `Instructions` |

Each implements `ICheckoutPaymentProviderAvailability`, so the payment step leaves it out of a checkout it doesn't apply to and the checkout engine refuses to begin a payment with it, and `ICheckoutPaymentProviderDescription` for the line under its name. `PayLaterTransactionCheckoutHandler` records the balance of any `OfflineCheckoutPaymentProvider` (each registered as one), titled after the order (`ICheckoutReferenceDescriber`), with the provider's key as the transaction source. `OfflinePaymentConfirmationDisplayDriver` puts the bank details and the reference on the confirmation, and, with Orders, `OrderOfflinePaymentDisplayDriver` shows what is still owed on the order's pages: how to pay for the buyer, and a **Record payment** link to the transaction for the store.

## Enabling the feature

Add the package to your Orchard Core project:

```bash
dotnet add package CrestApps.OrchardCore.PayLater
```

Then, in the **Orchard Core Admin Dashboard** under **Tools → Features**, enable **Pay Later**. It brings in the [Checkout](checkout) framework and the [Transactions](transactions) ledger as dependencies. Once enabled, the *Pay Later* option is offered by any checkout on the tenant (for example the [Subscriptions](subscriptions) checkout), and each Pay Later balance is tracked as an outstanding transaction.

## Related modules

- [Checkout](checkout) — the provider-agnostic checkout framework this option plugs into.
- [Transactions](transactions) — the provider-agnostic ledger that tracks, reports, reminds about, and settles the outstanding balances Pay Later records.
- [Subscriptions](subscriptions) — a consumer of checkout that can offer Pay Later at signup.
- [Payments](payments) — the lower-level payment contracts and the Stripe provider.

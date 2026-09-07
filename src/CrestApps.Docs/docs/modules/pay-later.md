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

On the checkout payment step Pay Later contributes its own panel explaining what the customer is agreeing to. It collects nothing, but it is still a display driver rather than a special case in the checkout page, which is what keeps that page provider-agnostic.

## Tracking outstanding balances

:::note Applies to checkout purchases
Balance tracking runs on the [Checkout](checkout) framework's completion pipeline, so it applies to any purchase made through the public checkout — including settling an existing balance. A **Pay Later subscription bought through the legacy [Subscriptions](subscriptions) signup flow does not yet create a transaction**, because that flow still records commitments through its own pipeline. A subscription bought through the public checkout does.
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

Under **Settings → Commerce → Pay Later** you can set the **net term (days)** — how many days after checkout a recorded Pay Later balance is due. The due date drives the reminder cadence configured in the [Transactions](transactions) settings. A value of `0` records the balance with no due date.

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

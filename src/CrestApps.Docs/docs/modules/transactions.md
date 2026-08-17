---
sidebar_label: Transactions
sidebar_position: 14
title: Transactions
description: A provider-agnostic ledger that tracks, reports, and settles outstanding financial obligations from any payment provider, with customer statements, an administrator report and management console, and payment reminders delivered through the notification system.
---

| | |
| --- | --- |
| **Feature Name** | Transactions |
| **Feature ID** | `CrestApps.OrchardCore.Transactions` |
| **Reminders Feature** | `CrestApps.OrchardCore.Transactions.Notification` |
| **Abstractions** | `CrestApps.OrchardCore.Transactions.Abstractions` |
| **Core** | `CrestApps.OrchardCore.Transactions.Core` |
| **Category** | Commerce |
| **Depends on** | [Commerce](commerce) |

The **Transactions** module is a provider-agnostic ledger of *money owed*. It answers a single question for the whole tenant — **"what has not been paid yet?"** — no matter which payment provider or purchase flow created the obligation. A transaction is created whenever a purchase is committed without being settled immediately (for example an offline [Pay Later](pay-later) commitment), and it is updated as reminders are sent and payments are recorded until it reaches a terminal state.

Because the ledger is generic, one report, one customer statement, and one reminder pipeline serve every provider. A provider only needs to create a `Transaction` for a balance it leaves outstanding; the Transactions module supplies all of the reporting, settlement, and reminder machinery.

## What it does

- Records outstanding obligations as durable **`Transaction`** ledger entries, persisted in the tenant database so they survive cache eviction and node failure.
- Gives every customer a **"My Transactions"** statement to view what they owe and to pay an outstanding balance online.
- Gives administrators a **report and management console** to see everything outstanding, record payments, mark obligations paid, cancel them, add notes, and send reminders.
- Sends **payment reminders through the [notification system](https://docs.orchardcore.net/en/latest/reference/modules/Notifications/)**, so each reminder honors the owner's preferred channel (email, and any other channel method they have enabled) rather than assuming email only. Reminders are an **opt-in feature** (see below).
- Runs a **scheduled reminder sweep** on a cadence you configure in settings, when the reminders feature is enabled.

## Concepts

### Transaction

A **`Transaction`** is the customer- and administrator-facing record of a single financial obligation. It carries the amounts (`Amount`, `TaxAmount`, `TotalAmount`, `AmountPaid`, and the computed `OutstandingAmount`), the owner, an optional due date, the provider-neutral origin (`Source`), and a neutral `ReferenceType` / `ReferenceId` / `ReferenceVersionId` triple that points back to whatever the obligation is for (an order, a subscription, and so on). It also keeps an audit timeline of **`TransactionEvent`** entries.

A transaction moves through a `TransactionStatus` lifecycle:

| Status | Meaning |
| --- | --- |
| `Pending` | Recorded but not yet due for collection. |
| `Outstanding` | Owed in full and not paid — the primary state a customer settles and an administrator chases. |
| `PartiallyPaid` | Part of the balance has been paid; a balance remains. |
| `Paid` | Paid in full. |
| `Canceled` | Canceled before payment; no longer collectable. |
| `Failed` | A collection attempt failed at the provider. |
| `Abandoned` | Left unpaid past its collection window. |
| `Refunded` | Paid and later refunded. |

### Settlement

An outstanding transaction can be settled two ways:

- **Online** — the customer chooses *Pay* on an outstanding transaction. The module starts a [Checkout](checkout) session that references the transaction (`ReferenceType` = `Transaction`), contributes the outstanding balance as a one-time billing item, and marks the transaction **Paid** when the checkout completes at a real gateway. This reuses the exact durable ledger and reconciliation the Checkout framework already provides, so a settlement is never recorded as paid unless the gateway confirms it.
- **Offline** — a manager records a payment or marks the transaction paid from the admin console (for example after receiving a bank transfer or cash). The settlement is recorded with an *offline* method and an audit event.

Online settlement is only available when the **[Checkout](checkout)** feature is enabled; the customer *Pay* action degrades gracefully (with a message) when it is not.

### Reminders

Reminders are delivered by `ITransactionReminderService` through OrchardCore's **`INotificationService`**, so a reminder reaches the owner on whichever channel they have configured. A reminder is recorded on the transaction timeline and increments its reminder count. Managers can send a reminder manually from the admin console, and a background task sweeps outstanding transactions and sends reminders automatically on the configured cadence.

Reminders are gated behind the separate **Transaction Reminders** feature (`CrestApps.OrchardCore.Transactions.Notification`), which depends on the OrchardCore **Notifications** feature. The core Transactions ledger, report, statements, and settlement work without it; enable the reminders feature only when you want manual and scheduled reminders. When it is disabled, the *Send reminder* action and the reminder settings are not shown.

## Using the module

### Customer statement — "My Transactions"

Authenticated users with the **View own transactions** permission get a **My Transactions** entry in the admin navigation. Consistent with the administrator report, it offers a search bar, a status filter dropdown (including an *outstanding* view), and a pager, and lets them open a transaction and **Pay** an outstanding balance online.

### Administrator report and console

Users with the **Manage transactions** permission get a **Commerce → Transactions** report. It filters by status (including an *outstanding* view), searches by title, and filters by **source** through a dropdown of the sources registered by the enabled features. Opening a transaction reveals its full timeline and the management actions:

- **Send reminder** — deliver a payment reminder now. Available only when the **Transaction Reminders** feature is enabled.
- **Record payment** — record a full or partial payment received offline.
- **Mark paid** — settle the remaining balance offline.
- **Cancel** — cancel an uncollectable obligation.
- **Add note** — attach a free-form note to the timeline.

Because the report is provider-agnostic, an administrator can see and manage an unpaid balance the same way regardless of which module created it. If a customer never pays, a manager has one place to chase, settle, cancel, or annotate the obligation, and can pair it with whatever downstream action a consuming module exposes (for example disabling a service or canceling a subscription).

## Registering a transaction source

The `Source` stored on a transaction is a technical key. To have it appear with a friendly, localizable name in the report table and its **source** filter dropdown, register the source from a module's `Startup` using `AddTransactionSource`:

```csharp
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.Extensions.Localization;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransactionSource("pay-later", source =>
        {
            source.DisplayName = S["Pay Later"];
            source.Description = S["Outstanding balances committed through the offline Pay Later option."];
        });
    }
}
```

The `name` must match the value the provider assigns to `Transaction.Source`. Registered sources populate the report's source filter; an unregistered source still appears in the table using its raw key.

## Configuring reminders

Reminder settings appear only when the **Transaction Reminders** feature (`CrestApps.OrchardCore.Transactions.Notification`) is enabled. Under **Settings → Commerce → Transactions** (requires the **Manage transaction settings** permission) you can configure the scheduled reminder sweep:

| Setting | Purpose | Default |
| --- | --- | --- |
| **Enabled** | Turns the scheduled reminder sweep on or off. | `true` |
| **First reminder delay (days)** | Days to wait after a transaction becomes due before the first reminder. | `0` |
| **Reminder interval (days)** | Days to wait between reminders. | `7` |
| **Maximum reminders** | Maximum reminders per transaction (`0` = no limit). | `3` |

The background task runs the sweep on a schedule and sends a reminder only when a transaction is due for one under this cadence, up to the maximum.

## Permissions

| Permission | Grants |
| --- | --- |
| **Manage transactions** | View and manage every tenant transaction: send reminders, record payments, mark paid, cancel, and add notes from the administration report. |
| **Manage transaction settings** | Configure the transaction reminder settings. |
| **View own transactions** | View and pay your own transactions. |

## Enabling the feature

Add the package to your Orchard Core project:

```bash
dotnet add package CrestApps.OrchardCore.Transactions
```

Then, in the **Orchard Core Admin Dashboard** under **Tools → Features**, enable **Transactions**. Enabling it also enables the **[Commerce](commerce)** feature it depends on, which owns the shared Commerce admin menu and icon.

Enable **[Checkout](checkout)** as well if you want customers to settle outstanding balances online. To send manual and scheduled payment reminders, enable the **Transaction Reminders** feature (`CrestApps.OrchardCore.Transactions.Notification`); it depends on the OrchardCore **[Notifications](https://docs.orchardcore.net/en/latest/reference/modules/Notifications/)** feature, which is enabled automatically as a dependency.

## Related modules

- [Commerce](commerce) — owns the shared Commerce admin menu and icon this module contributes to.
- [Pay Later](pay-later) — records offline commitments as outstanding transactions in this ledger.
- [Checkout](checkout) — the provider-agnostic checkout framework used to settle outstanding transactions online.
- [Subscriptions](subscriptions) — a consumer of checkout that can leave balances a transaction tracks.
- [Payments](payments) — the lower-level payment contracts and the Stripe provider.

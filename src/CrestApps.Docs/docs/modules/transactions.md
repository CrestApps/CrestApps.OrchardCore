---
sidebar_label: Transactions
sidebar_position: 14
title: Transactions
description: A provider-agnostic ledger that tracks, reports, and settles outstanding financial obligations from any payment provider, with customer statements, an administrator report and management console, and payment reminders delivered through the notification system.
user_manual:
  - user-manual/commerce/transactions
  - user-manual/commerce/invoices-and-receipts
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

<YouTube video="commerce" title="Narrated video of the Transactions report, a transaction, and the reminder settings" start={408} end={454} />

- Records outstanding obligations as durable **`Transaction`** ledger entries, persisted in the tenant database so they survive cache eviction and node failure.
- Gives every customer a **"My Transactions"** statement to view what they owe and to pay an outstanding balance online.
- Gives administrators a **report and management console** to see everything outstanding, record payments, mark obligations paid, cancel them, add notes, and send reminders.
- Sends **payment reminders through the [notification system](https://docs.orchardcore.net/en/latest/reference/modules/Notifications/)**, so each reminder honors the owner's preferred channel (email, and any other channel method they have enabled) rather than assuming email only. Reminders are an **opt-in feature** (see below).
- Runs a **scheduled reminder sweep** on a cadence you configure in settings, when the reminders feature is enabled.

## Concepts

### Transaction

A **`Transaction`** is the customer- and administrator-facing record of a single financial obligation. It carries the amounts (`Amount`, `TaxAmount`, `TotalAmount`, `AmountAdjusted`, `AmountPaid`, and the computed `OutstandingAmount = TotalAmount - AmountAdjusted - AmountPaid`, never negative), the owner, an optional due date, the provider-neutral origin (`Source`), and a neutral `ReferenceType` / `ReferenceId` / `ReferenceVersionId` triple that points back to whatever the obligation is for (an order, a subscription, and so on). It also keeps an audit timeline of **`TransactionEvent`** entries.

When the transaction's own reference names an intermediate record, `ParentReferenceType` and `ParentReferenceId` name the record it ultimately belongs to. For example, every payment of an [installment plan](installment-plans) references the plan, and its parent reference names the record the plan pays for, such as a registration. The parent reference is indexed in its own table (`TransactionParentReferenceIndex`, created by `TransactionParentReferenceMigrations`; only transactions that carry one have a row), and `ITransactionStore.GetByParentReferenceAsync(parentReferenceType, parentReferenceId)` returns every transaction of a record, oldest first. The inherited `Properties` dictionary carries values the module that created the transaction attached; only that module interprets them.

The owner is described by an `OwnerId` and an `OwnerKind` (`Authenticated` or `Guest`). A purchase made by a signed-in user is owned by that user's id; a purchase made without an account is owned by a stable, tenant-scoped **guest customer id** — so a guest obligation is attributable and queryable instead of being orphaned under a null owner. A guest transaction also carries the contact (name and email) captured at purchase time, used to reach the guest for reminders.

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

- **Online** — the customer chooses *Pay* on an outstanding transaction. The module starts a [Checkout](checkout) session that references the transaction (`ReferenceType` = `Transaction`), contributes the outstanding balance as a one-time billing item, and settles the transaction when the checkout completes at a real gateway. Settlement is applied against the amount the payment provider **actually confirmed** (read from the durable payment-attempt ledger), never the amount the checkout requested, and the attempt currency must match the transaction currency — a mismatch is rejected rather than converted. A confirmed amount that covers the full balance marks the transaction **Paid**; a smaller confirmed amount marks it **Partially paid** and leaves the remainder outstanding. Settlement is **idempotent** (re-completing the same checkout never double-applies a payment) and records the settling `PaymentAttemptId` so a payment can always be reconciled against the gateway. Concurrent writes are guarded by optimistic concurrency, so two nodes settling the same transaction can never silently overwrite each other. This reuses the exact durable ledger and reconciliation the Checkout framework already provides, so a settlement is never recorded as paid unless the gateway confirms it.
- **Offline** — a manager records a payment or marks the transaction paid from the admin console (for example after receiving a bank transfer or cash). The settlement is recorded with an *offline* method and an audit event.

A promotion code can be used on a settlement checkout. What the invoice took off is recorded on the transaction as a `Discount` adjustment of exactly that amount, so a 100% code settles it with no payment and a partial code plus the payment settles it in full; the code is redeemed only once the transaction is settled. A coupon can also be limited to transaction sources, reference types or one customer. See [Coupons on a checkout that settles an existing balance](checkout.md#coupons-on-a-checkout-that-settles-an-existing-balance).

Choosing *Pay* starts a checkout through `ICheckoutEngine` and takes the customer straight into it, with the outstanding balance already contributed as the checkout's billing item. The balance is carried by a concealed step whose key is `TransactionsConstants.SettlementStepKey` (`TransactionSettlement`), so another feature can find it: when installment plans are enabled and installments are offered for the transaction, a **Choose how to pay** step comes first, and choosing installments moves the checkout onto the plan's first payment (see [Installments at checkout](installment-plans#installments-at-checkout)). Online settlement is only available when the **[Checkout](checkout)** feature is enabled; the *Pay* action degrades gracefully (with a message) when it is not.

### Paying several transactions at once

One checkout can settle several outstanding transactions of one customer with one payment. Start it with `StartCheckoutRequest.References` instead of `ReferenceId`:

```csharp
var session = await checkoutEngine.StartAsync(new StartCheckoutRequest
{
    References = transactionIds
        .Select(id => new CheckoutReference
        {
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = id,
        })
        .ToList(),
    ReturnUrl = Url.Action("Index", "Payments"),
    ReturnText = S["Back to my payments"],
    Title = S["Payment received"],
});
```

The session keeps the list in `CheckoutSession.References`; its `ReferenceType` is `Transaction` and its `ReferenceId` is empty. A list with one transaction starts the same checkout as `ReferenceId` does.

- **What is checked.** Every transaction must exist, still have something to pay and still be collectable, and all of them must belong to one customer (the same owner kind and owner id, or the same guest email when there is no owner id) and be in one currency. Otherwise `StartAsync` throws `CheckoutRejectedException` and nothing is saved; its `Errors` say why, in words a customer can read. Check that the signed-in customer owns the transactions before you start the checkout, as the single *Pay* action does.
- **What the customer sees.** The concealed `TransactionSettlement` step carries one line per transaction, at what it still owes, so the checkout summary lists each one. No promotion code is offered: the session sets `SettlesExistingBalance` and leaves `AcceptsDiscountsOnBalance` off, as described in [Coupons on a checkout that settles an existing balance](checkout.md#coupons-on-a-checkout-that-settles-an-existing-balance). Installments are not offered either; they apply to a checkout for one transaction.
- **How the payment is applied.** The customer pays once, so the payment ledger has one payment attempt. Its `ReferenceType` is `Transaction` and its `ReferenceId` is empty. When the checkout completes, the confirmed amount is split across the transactions in the order they were listed, each up to what the checkout asked for it and what it still owes, through `ITransactionSettlementService.ApplyShareAsync`. Each transaction records its share as a payment of its own that names the attempt, so its status, its receipt, the payment handlers and the lifecycle events work as for a transaction paid on its own. When the gateway confirms less than was asked, the first transactions are paid in full and the rest are left partly paid. Completing the checkout again applies nothing twice.
- **Refunds stay per transaction.** **Refund** on a transaction gives back only that transaction's share of the payment, less what was already refunded for it. The refund names the transaction (`PaymentRefund.ReferenceType` and `ReferenceId`), so it is recorded on that one only. A refund of the whole payment, started from **Commerce → Payments** or at the gateway, names no transaction: it is split back over the transactions in the order they were listed, each up to its share.

:::note[Finding the checkout of one transaction]
`ICheckoutSessionStore.GetByReferenceAsync("Transaction", id)` and `IPaymentAttemptStore.GetByReferenceAsync("Transaction", id)` find only checkouts for that one transaction. A payment that settled it together with others is found through the transaction's timeline: each `PaymentRecorded` event names its `PaymentAttemptId`.
:::

### Reminders

Reminders are delivered by `ITransactionReminderService`, which hands them to `ICustomerOwnerNotifier` (the notifier receipts and installment plan notices use too). An **authenticated** owner is reminded through OrchardCore's **`INotificationService`**, so the reminder reaches them on whichever channel they have configured. A **guest** owner (who has no account) is reminded by **email** through the optional email service, using the contact captured at purchase time; when no email service is registered or no guest contact is available, the guest reminder is skipped gracefully. A reminder is recorded on the transaction timeline and increments its reminder count. Managers can send a reminder manually from the admin console, and a background task sweeps outstanding transactions and sends reminders automatically on the configured cadence.

Reminders are gated behind the separate **Transaction Reminders** feature (`CrestApps.OrchardCore.Transactions.Notification`), which depends on the OrchardCore **Notifications** feature. The core Transactions ledger, report, statements, and settlement work without it; enable the reminders feature only when you want manual and scheduled reminders. When it is disabled, the *Send reminder* action and the reminder settings are not shown.

Besides chasing what is overdue, the sweep tells the owner a payment is **coming due**, once, a few days before
its due date (`SendUpcomingReminderAsync`). A transaction that will be collected without the owner acting — an
installment charged to a saved card — carries a `TransactionAutoCollection` describing the card, and its notice
says which card will be charged and when instead of asking the owner to pay.

### Payment receipts

Printable receipts and invoices come with the [Receipts](receipts) module: whenever it is enabled with Transactions,
each payment on a transaction's timeline links to its printable receipt, and a transaction links its invoice. They only
show what the ledger already holds, so they need no feature of their own.

The **Send Payment Receipts** feature (`CrestApps.OrchardCore.Transactions.Receipts`, which needs the Receipts module
and Notifications) sends the owner a receipt for every payment applied to a transaction: settled online,
recorded offline, or marked paid. A receipt covers the money in that one payment, with that payment's share of the
transaction's tax, so a balance paid in three parts gets three receipts that add up. A signed-up owner receives it
through the notification system with an HTML body and a link to the receipt's page (see
[Links in messages](#links-in-messages)); a guest owner receives it by email.

Each receipt gets a short sequential number per site, `R-1001`, `R-1002` and so on, issued once when it is first sent
and shown on every copy after that (`TransactionEvent.ReceiptNumber`). The numbers come from
`IFinancialDocumentNumberGenerator`; the shipped generator keeps the last number of each series durably and issues
the next one under a distributed lock, so no two receipts share a number even across nodes. A payment that fails
after taking a number leaves a gap rather than reusing it.

The same feature tells the owner when a payment is refunded, however the refund finished: confirmed by the gateway
straight away, confirmed later by a gateway notification, or recorded by an administrator who paid it back by hand.
The notice names the amount and what it was for, and the transaction's timeline records that it was sent.

### Invoices and pay links

<YouTube video="commerce" title="Narrated video of an invoice, its pay link, and a customer paying through the link" start={452} end={501} />

A transaction its owner pays themselves (one that is not charged automatically) is invoiced:

- **Invoice numbers.** The first time the owner is told about the payment, by the invoice an administrator sends
  (**Send invoice**, `ITransactionCommandService.SendInvoiceAsync`), the reminder before the due date, the
  notice on the due date, or a reminder an administrator sends, the transaction is numbered `INV-1001`, `INV-1002`
  and so on (`Transaction.InvoiceNumber`, from `ITransactionInvoiceService`). The number never changes, and a notice
  that could not be delivered still keeps the number it was given.
- **Pay links.** Each of those notices carries a link that pays the transaction without signing in, as text and as a
  button. The link names one transaction, is signed with the site's data protection keys so it cannot be altered,
  and expires after 60 days; every notice sends a fresh one. It opens a public page with the invoice and a **Pay**
  button that starts the normal checkout, so a customer an administrator created (with no password) or a guest can
  pay. The checkout it starts returns to the page (`StartCheckoutRequest.ReturnUrl`), with *Back to the payment page*
  on the payment step and the confirmation, and the confirmation is headed *Payment received*. A link that was
  altered or expired shows nothing about any transaction. Links use the site's **Base URL**
  from the general settings, so set it to the public address; without it, a link made outside a request is left out
  and the notice asks the owner to sign in instead. `ITransactionPayLinkService` creates and reads them.
- **The invoice page.** With the Receipts module enabled, the transaction's page links its invoice: the same layout
  as a receipt, headed *Invoice*, with a *Due* status until it is paid. Administrators also see the pay link there,
  to send it by hand. The receipt for the payment names the invoice it settled.
- **The first reminder on the due date** reads as the payment being due today; later ones chase the unpaid balance.

### Reacting to a payment

`ITransactionPaymentHandler.PaymentRecordedAsync` is raised after a payment is applied to a transaction, however it
was paid. Receipts and installment plans use it; implement it to react to payments without caring which screen or
process took the money. Each recorded payment is a `TransactionEvent` with an `Id`, the `Amount` applied and the
`Method` (`online` or `offline`). A handler that fails is logged and never undoes the payment.

### Reacting to a refund

`IPaymentRefundHandler.RefundSucceededAsync` is raised once for every refund that succeeds, from each place a refund
can finish. The refund names the payment it returned (`OriginalAttemptId`), and that payment names what it paid for.
Refund notices use it, and so does the module itself: `TransactionRefundHandler` records each refund on the
transaction it returns money for. It adds a `Refunded` timeline event, keyed by the refund's identifier so a refund
reported twice counts once, and adds to `Transaction.AmountRefunded`. Once everything paid has been refunded, it moves
the transaction to `TransactionStatus.Refunded`. A refund never lowers `AmountPaid`, so a refunded transaction never
becomes collectable again. The transaction is found through the refund's own reference (a refund of one transaction's share of a payment that
settled several), the payment's reference or, for a purchase checkout, through the refund's session and obligation. A
refund of a whole payment that settled several transactions is split back over them (see
[Paying several transactions at once](#paying-several-transactions-at-once)). A handler that fails is logged and never undoes the refund.

### Transaction commands

`ITransactionCommandService` (in `CrestApps.OrchardCore.Transactions.Abstractions`, registered by the **Transactions** feature) runs the actions staff take on a transaction, so a module can offer them on its own screens with the same timeline entries, receipts and handler calls as the Commerce console. The console's **Record payment**, **Mark as paid**, **Cancel transaction**, **Adjust** and **Send invoice** actions call it and hold no logic of their own.

| Command | What it does |
| --- | --- |
| `RecordOfflinePaymentAsync(id, amount, reference, note)` | Records money received outside the site. An amount above what is owed records only what is owed; `TransactionEvent.Reference` keeps the reference. Raises `ITransactionPaymentHandler.PaymentRecordedAsync` (so a receipt is sent). |
| `MarkPaidAsync(id, note)` | Records what is still owed as an offline payment and marks the transaction `Paid`. |
| `CancelAsync(id, reason)` | Cancels a collectable transaction; the reason goes to `TransactionEvent.Reason` and `ITransactionEventHandler.CanceledAsync`. |
| `AdjustAsync(id, amount, kind, reason)` | Lowers what is owed without money being received. `kind` is a `TransactionAdjustmentKind`: `Discount`, `Waiver`, `Credit` or `WriteOff`. Adds to `Transaction.AmountAdjusted` and records a `TransactionEventType.Adjusted` event (`Amount`, `AdjustmentKind`, `Reason`). An adjustment can never exceed `OutstandingAmount`. When nothing is left to pay, the transaction becomes `Paid` with `SettlementMethod = "adjusted"`, so it is never reminded. No payment handler runs, because no money moved. Adjustments are printed as negative lines on the invoice and noted on each receipt. |
| `SendInvoiceAsync(id)` | Issues the invoice number when there is none and sends an *Invoice INV-1001* notice (not a reminder) with the pay link, through `ITransactionReminderService.SendInvoiceAsync`. It records a `TransactionEventType.InvoiceSent` event and counts as the first notice (`ReminderCount` and `LastReminderSentUtc`). Needs the **Transaction Reminders** feature; refused for a transaction collected automatically (`AutoCollection`). |
| `RefundAsync(id, amount?, reason)` | Finds the confirmed payment attempts made for the transaction (from its timeline and `IPaymentAttemptStore.GetByReferenceAsync("Transaction", id)`), and refunds them through `ICheckoutRefundService`, newest first, each up to what it still has to give back, until `amount` (or everything refundable when `null`) is refunded. Attempts from a provider that defers payment, or with a refund waiting for manual settlement, are skipped. From a payment that settled several transactions at once, only this transaction's share can be refunded, and the refund names this transaction. An amount above what can be refunded is refused before anything is refunded. `IPaymentRefundHandler` then records each refund on the transaction. Needs the **Checkout** feature. |
| `ChargeSavedPaymentMethodAsync(id, savedPaymentMethodId)` | Charges what is still owed to a card the transaction's owner kept (`ISavedPaymentMethodStore`, see [Kept payment methods](checkout.md#kept-payment-methods)), without the owner present. See [Charging a kept card](#charging-a-kept-card). Needs the **Checkout** feature and a provider that can charge saved payment methods. |

Each command returns a `TransactionCommandResult`: `Succeeded`, `Errors` (messages ready to show), the `Transaction` as saved (`null` when no transaction has that id), the timeline `Event` it recorded, the `Refunds` it requested, and, for a charge of a kept card, its `ChargeStatus`. A command takes a distributed lock on the transaction (`IDistributedLock`), loads it, checks the transaction accepts the command (`TransactionStateMachine`), saves it through `ITransactionManager` (which raises the lifecycle events) and commits the session before releasing the lock, so concurrent commands never overwrite each other; a conflict comes back as an error. Payment handlers run after that commit. The service does not check permissions; the caller decides who may run a command. The signed-in user is recorded as the event's actor.

```csharp
public sealed class RegistrationPayments
{
    private readonly ITransactionCommandService _commands;

    public RegistrationPayments(ITransactionCommandService commands)
    {
        _commands = commands;
    }

    public async Task<IReadOnlyList<string>> ApplyFinancialAidAsync(string transactionId, decimal amount)
    {
        var result = await _commands.AdjustAsync(transactionId, amount, TransactionAdjustmentKind.Waiver, "Financial aid");

        return result.Errors.ToArray();
    }
}
```

#### Charging a kept card

`ChargeSavedPaymentMethodAsync` lets a module collect a transaction from the card its owner agreed to keep, for example to renew a subscription charged to the card on file, and fall back to the invoice when that doesn't work:

1. It refuses, without contacting the provider, a transaction that owes nothing or can't take a payment, a kept card that belongs to anyone but the transaction's owner (`Transaction.OwnerId` must match `CustomerPaymentMethod.OwnerId`), and an expired card. `ChargeStatus` is then `NotCharged`.
2. A charge of the same transaction still settling at the provider is finished (`ICheckoutEngine.TryCompleteAsync`) instead of being repeated, so calling the command twice never charges the owner twice. Charges of one transaction take their own lock, `Transactions:Charge:{id}`.
3. It starts a checkout for the transaction on the owner's behalf (`StartCheckoutRequest.OwnerId`) and begins the payment with `CheckoutPaymentDataKeys.ForOffSessionCharge(card)`. The money moves through the checkout, so a charge that goes through is verified against the provider, recorded on the transaction as a `PaymentRecorded` event by the settlement handler, receipted, and raised to `ITransactionPaymentHandler` and `ITransactionEventHandler` like any checkout payment.
4. A charge that is not taken is recorded on the timeline as a `TransactionEventType.PaymentFailed` event (**Payment failed**) with the amount, the card's description, the provider's reason and the failed attempt (`PaymentAttemptId`). The transaction stays as it was.

| `ChargeStatus` | Meaning | `Succeeded` |
| --- | --- | --- |
| `Succeeded` | The payment was taken and recorded. `Event` is the recorded payment. | `true` |
| `Pending` | The provider accepted the charge but hasn't confirmed it. The reconciliation sweep records it once it settles; don't charge again. | `true` |
| `Declined` | The provider refused the payment. | `false` |
| `RequiresAuthentication` | The card's bank asked the cardholder to approve the payment (`PaymentBeginResult.RequiresAuthentication`), which can't be done without them. The owner has to pay it, for example from the invoice's pay link. | `false` |
| `Failed` | The payment couldn't be started, for example the provider was unreachable. Nothing was charged; try again later. | `false` |
| `NotCharged` | The command was refused before the provider was asked. | `false` |

```csharp
public sealed class RenewalCollector
{
    private readonly ITransactionCommandService _commands;
    private readonly ISavedPaymentMethodStore _paymentMethods;

    public RenewalCollector(ITransactionCommandService commands, ISavedPaymentMethodStore paymentMethods)
    {
        _commands = commands;
        _paymentMethods = paymentMethods;
    }

    public async Task CollectAsync(Transaction renewal, CancellationToken cancellationToken)
    {
        var card = await _paymentMethods.GetDefaultAsync(renewal.OwnerId, cancellationToken);

        var result = card is null
            ? null
            : await _commands.ChargeSavedPaymentMethodAsync(renewal.ItemId, card.ItemId, cancellationToken);

        // No card, a decline or a bank that wants the cardholder to approve: send the invoice instead.
        if (result?.ChargeStatus is not (SavedPaymentMethodChargeStatus.Succeeded or SavedPaymentMethodChargeStatus.Pending))
        {
            await _commands.SendInvoiceAsync(renewal.ItemId, cancellationToken);
        }
    }
}
```

The command spends a card without its owner present, so only server code may call it; never pass it a kept payment method id taken from a request without checking whose card it is. The [test payment provider](checkout.md#magic-amounts) declines an off-session charge ending in `02` and refuses one ending in `03` as needing the cardholder's approval, so all three outcomes can be tested.

### Reacting to lifecycle changes

`ITransactionEventHandler` (in `CrestApps.OrchardCore.Transactions.Abstractions`) tells a module about every change in a transaction's state, with the status it had before. Derive from `TransactionEventHandlerBase` and override only the events you need, then register the handler as `ITransactionEventHandler` (scoped):

| Event | Raised |
| --- | --- |
| `CreatedAsync(transaction)` | After `ITransactionManager.CreateAsync` saved a new transaction. |
| `StatusChangedAsync(transaction, previous, current)` | After `ITransactionManager.UpdateAsync` saved a status different from the one the transaction was loaded with (or last saved with). Once per such save; a save that leaves the status as it was raises nothing, so reminders, notes and invoice numbers never raise it. |
| `CanceledAsync(transaction, reason)` | Right after `StatusChangedAsync` reported a change to `Canceled`. The reason is the `TransactionEvent.Reason` of the newest `Canceled` timeline event, or `null` when none was given. |
| `BecameOverdueAsync(transaction)` | Once per transaction, by the reminder sweep, the first time it finds the transaction `Outstanding` or `PartiallyPaid` with money owed after the day it was due (a payment due today is not overdue yet). `Transaction.BecameOverdueUtc` records that it was raised, so it is never raised twice. |

Every place a transaction changes goes through the manager, so the events cover the console (**Cancel**, **Mark paid**, **Record payment**), an online settlement, a refund that brings what was kept to zero (`Refunded`), an installment charge that was declined (`Failed`, then `Outstanding` once no retries are left, or `Paid` when a retry goes through), and a canceled installment plan. The manager diffs the status itself, so a consumer no longer needs a catalog handler that re-reads every save.

Handlers run after the save, and they are resolved when the event is raised (not injected into the manager), so a handler may depend on `ITransactionManager`. A handler that throws is logged and does not undo the change or stop the other handlers, the same as `ITransactionPaymentHandler`. The overdue sweep is part of the **Transaction Reminders** feature's background task (`TransactionReminderBackgroundTask`, every 6 hours). It runs even when **Enabled** is turned off in the reminder settings, which only stops the reminders themselves.

```csharp
public sealed class RegistrationPaymentStatusHandler : TransactionEventHandlerBase
{
    public override Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
    {
        // Move the registration this transaction charges to unpaid, paid or refunded.
        return Task.CompletedTask;
    }

    public override Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        // Flag the registration as overdue.
        return Task.CompletedTask;
    }
}

services.AddScoped<ITransactionEventHandler, RegistrationPaymentStatusHandler>();
```

## Using the module

### Customer statement — "My Transactions"

Authenticated users with the **View own transactions** permission get a **My Transactions** entry in the admin navigation. Consistent with the administrator report, it offers a search bar, a status filter in the list header (including an *outstanding* view), and a pager, and lets them open a transaction and **Pay** an outstanding balance online. Paying opens the checkout straight on the payment step: the balance being paid is shown in the order summary, so there is no separate step for it.

The same statement, and the customer's invoices and receipts, are also available in the site theme; see [Customer pages in the site theme](#customer-pages-in-the-site-theme).

### Administrator report and console

Users with the **Manage transactions** permission get a **Commerce → Transactions** report. It searches by title, and its list header filters by status (including an *outstanding* view) and by **source**, from the sources registered by the enabled features. Each row shows the owner, source, total, outstanding balance and due date as badges. Opening a transaction reveals its full timeline and the management actions, each of which asks for confirmation before it changes anything:

- **Send reminder** — deliver a payment reminder now. Available only when the **Transaction Reminders** feature is enabled.
- **Record payment** — record a full or partial payment received offline, with an optional reference.
- **Mark paid** — settle the remaining balance offline.
- **Adjust or waive** — lower what is owed by a discount, waiver, credit or write-off, never more than is outstanding.
- **Send invoice** — send the invoice now, with its pay link. Available only when the **Transaction Reminders** feature is enabled, and not for a transaction collected automatically.
- **Cancel** — cancel an uncollectable obligation.
- **Add note** — attach a free-form note to the timeline.

Because the report is provider-agnostic, an administrator can see and manage an unpaid balance the same way regardless of which module created it. If a customer never pays, a manager has one place to chase, settle, cancel, or annotate the obligation, and can pair it with whatever downstream action a consuming module exposes (for example disabling a service or canceling a subscription).

## Customer pages in the site theme

The customer pages above are admin pages marked `[CustomerAdminPage]`, so they render in the admin theme. A customer who never uses the admin gets the same documents in the site theme, at front-end routes that need no admin access:

| Route | Route name (`TransactionsConstants.RouteNames`) | Shows | Needs |
| --- | --- | --- | --- |
| `my-transactions` | `Statement` (`TransactionsStatement`) | The signed-in customer's statement, filtered by `search` and `outstanding=true`, paged with `page` and `pagesize`. | Transactions |
| `my-transactions/invoices/{itemId}` | `Invoice` (`TransactionsInvoice`) | The printable invoice, with **Pay now** (the transaction's signed pay link) while it is due. | Transactions and Receipts |
| `my-transactions/receipts/{itemId}/{paymentId}` | `Receipt` (`TransactionsReceipt`) | The printable receipt of one payment. | Transactions and Receipts |

The admin routes keep working and are named too: `AdminStatement` (`Admin/my-transactions`), `AdminInvoice` (`Admin/transaction-invoices/{itemId}`) and `AdminReceipt` (`Admin/transaction-receipts/{itemId}/{paymentId}`).

### Ownership rules

The admin pages and the site pages build their documents through the same `ITransactionCustomerPageService`, so they apply the same rules:

- A visitor who is not signed in is sent to the sign-in page by the site pages (the admin redirects them as it does for any admin page).
- The statement needs **View own transactions** and lists only transactions owned by the viewer's account (`OwnerKind` `Authenticated` and `OwnerId` equal to the user's identifier).
- An invoice or a receipt needs **View own transactions** or **Manage transactions**; neither answers `403`. A customer sees only a transaction their account owns. A guest transaction is never shown to an account, even one that shares its identifier: a guest reaches their invoice through the signed [pay link](#invoices-and-pay-links). A user with **Manage transactions** sees any invoice or receipt, and the model's `IsAdministrator` is `true`.
- Someone else's transaction, an invoice that was never issued (no `InvoiceNumber`), an event that is not a recorded payment, and a site without the Receipts module all answer `404`, so identifiers cannot be probed.

### Shapes

Each site page renders a shape. A theme overrides how it looks with a template of the same name (for example `Views/TransactionInvoice.cshtml` in the theme):

| Shape (`TransactionsConstants.Shapes`) | Model |
| --- | --- |
| `TransactionStatement` | `TransactionStatementViewModel`: the page's `Entries` (each a `TransactionStatementEntry` with the `Transaction`, its `PayUrl`, its `InvoiceUrl` and its `Receipts`), `TotalCount`, `Search`, `OutstandingOnly` and the `Pager` shape. |
| `TransactionInvoice` | `TransactionInvoiceViewModel`: the `Transaction`, the `Invoice` (`ReceiptDocument`), `IsAdministrator` and `PayUrl`. |
| `TransactionReceipt` | `TransactionReceiptViewModel`: the `Transaction`, the `Payment` event, the `Receipt` (`ReceiptDocument`) and `IsAdministrator`. |

### Showing the documents from another module

`ITransactionCustomerPageService`, `ITransactionInvoiceBuilder`, `ITransactionReceiptBuilder`, the view models and `TransactionsConstants` live in `CrestApps.OrchardCore.Transactions.Abstractions`, so a module can show a customer their documents in its own pages without referencing the Transactions module. The builders are registered only while the Receipts module is enabled, so resolve them as optional. A portal page that shows a customer's invoice:

```csharp
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;

public sealed class PortalPaymentsController : Controller
{
    private readonly ITransactionCustomerPageService _pages;
    private readonly IShapeFactory _shapeFactory;

    public PortalPaymentsController(ITransactionCustomerPageService pages, IShapeFactory shapeFactory)
    {
        _pages = pages;
        _shapeFactory = shapeFactory;
    }

    [HttpGet("portal/payments/{itemId}/invoice")]
    public async Task<IActionResult> Invoice(string itemId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var result = await _pages.BuildInvoiceAsync(User, itemId, HttpContext.RequestAborted);

        if (result.Status == TransactionCustomerPageStatus.Forbidden)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            return NotFound();
        }

        // The view renders the shape with @await DisplayAsync(Model).
        return View(await _shapeFactory.CreateAsync<TransactionInvoiceViewModel>(TransactionsConstants.Shapes.Invoice, result.Model.CopyTo));
    }
}
```

`BuildStatementAsync` takes a `TransactionStatementRequest` (`Page`, `PageSize`, `Search`, `OutstandingOnly`) and leaves `Pager` empty, so the caller can set a pager that links to its own page. `BuildReceiptAsync` takes the transaction and payment event identifiers.

### Links in messages

The receipt sent by **Send Payment Receipts** and the reminders sent by **Transaction Reminders** link a signed-up customer to their document: the receipt carries a **View your receipt online** link, and a reminder for an invoiced transaction carries **View your invoice** (only while the Receipts module is enabled). Guests get neither, because both pages need an account; their reminders keep the signed pay link.

`ITransactionCustomerLinkService` creates these links. They open the admin pages by default; set `LinkTarget` to `Site` to open the site pages instead. Like pay links, they use the site's **Base URL** when it is set, and the current request otherwise; a link made outside a request on a site without a base URL is left out.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Transactions": {
        "CustomerPages": {
          "LinkTarget": "Site"
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Transactions__CustomerPages__LinkTarget=Site
```

| Key | Values | Default |
| --- | --- | --- |
| `LinkTarget` | `Admin` (the `Admin/...` customer pages) or `Site` (the `my-transactions/...` pages in the site theme) | `Admin` |

A module can also set it in code with `services.Configure<TransactionCustomerPagesOptions>(options => options.LinkTarget = TransactionCustomerLinkTarget.Site)` (`CrestApps.OrchardCore.Transactions.Core`). See also [Configuration](../configuration.md#transaction-customer-pages).

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
| **Remind before the due date (days)** | How many days before a payment falls due the owner is told it is coming due, once per payment (`0` = off). | `3` |

The background task runs the sweep on a schedule and sends a reminder only when a transaction is due for one under this cadence, up to the maximum.

## Payments and refunds

With [Checkout](checkout) enabled, **Commerce → Payments** lists every payment the suite has attempted, filtered by state and payment method, showing what was expected, what actually settled, and the gateway's own transaction reference. It answers "did this customer actually pay?" without reading the database.

It is also the only place a refund starts. **Refund** isn't offered on a payment from a provider that defers payment (`PaymentProviderCapabilities.DefersPayment`, such as Pay Later), since nothing was collected to give back. A refund the service refuses, because another refund for the same payment holds the lock, an earlier one still needs manual settlement, or the checkout is gone, comes back as a message rather than an error page. The form shows what was collected and what has already been given back, refuses an amount above what is still refundable, and issues the refund through the refund service so the durable ledger, the original payment's tax allocation, and the over-refund protection all apply. Counting an in-flight refund against the remaining amount is deliberate: ignoring one would let an operator start a second refund for money already on its way back.

**Commerce → Refunds** lists what came of them. A refund against a payment method that has no executable refund operation is recorded as **needing manual settlement** rather than silently dropped, and this screen is where an operator records the reference once they have moved the money by hand. Only a refund in that state can be closed this way; marking a failed gateway refund as done would tell the ledger the customer was paid when they were not.

Both screens require the **Manage payments and refunds** permission.

## Report Builder data source

When **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) is enabled, as it is with the [Report Builder](report-builder/index.md), the **Transactions** feature registers the **Transactions** data source (`Transactions`), so reports and data pipelines can read the ledger, the outstanding balances, and the payments and refunds. The payments and refunds data sets are registered only while [Checkout](checkout), which keeps those ledgers, is enabled too. Each data set reads its newest records first, a page at a time, through its index. Money is in each record's own currency, which the **Currency** field next to it names. Identifier fields reference the data sets they point to, so checkout sessions, orders, users and payments join automatically.

| Data set | One row per | Feature | Permission |
| --- | --- | --- | --- |
| **Transactions** (`Transactions`) | Transaction of the ledger | Transactions | **Manage transactions** (`ManageTransactions`) |
| **Payments** (`PaymentAttempts`) | Payment the checkout tried to take | Transactions with Checkout | **Manage payments and refunds** (`ManageRefunds`) |
| **Refunds** (`PaymentRefunds`) | Refund of a payment | Transactions with Checkout | **Manage payments and refunds** (`ManageRefunds`) |

The outstanding transactions are those with the status `Outstanding` or `PartiallyPaid`; a status filter narrows the read in the database, as do the date range a report puts on the main date of transactions and payments and the keys of a join. The refunds index holds no creation date, so the refunds are read newest first and their date range is applied to the rows read. A transaction's history and notes, its settlement reference, and the keys a payment or refund is made with are never exposed.

### Transactions

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Transaction ID | |
| `Title`, `Status`, `Source`, `InvoiceNumber` | Title, Status, Source, Invoice number | `Source` is the provider that recorded the transaction, such as Pay Later. |
| `ReferenceType`, `ReferenceId` | Reference type, Reference ID | What the transaction is for. |
| `OrderId` | Order ID | The reference when it is an order. Joins **Orders**. |
| `ParentReferenceType`, `ParentReferenceId` | Parent reference type, Parent reference ID | |
| `CheckoutSessionId` | Checkout session ID | Joins **Checkout sessions**. |
| `IsRecurring`, `IsAutoCollected`, `ReminderCount` | Recurring, Collected automatically, Reminders sent | |
| `Currency` | Currency | |
| `Amount`, `TaxAmount`, `TotalAmount`, `AmountPaid`, `AmountAdjusted`, `AmountRefunded` | Amount before tax, Tax, Total, Paid, Adjusted or waived, Refunded | In the transaction's currency. |
| `OutstandingAmount` | Outstanding | The total less what was adjusted and paid, rounded to the currency, in the transaction's currency. |
| `SettlementMethod` | Settled by | `online`, `offline` or `adjusted`. |
| `CreatedUtc` | Recorded | The main date. |
| `DueUtc`, `BecameOverdueUtc`, `SettledUtc`, `LastReminderSentUtc`, `UpdatedUtc` | Due, Became overdue, Settled, Last reminder, Last changed | |
| `OwnerId`, `OwnerKind`, `IsGuest` | Owner ID, Owner kind, Guest | A guest's owner ID is their guest customer ID. |
| `UserId` | User ID | The owner when they have an account. Joins **Users**. |
| `GuestContactName`, `GuestContactEmail` | Guest name, Guest email | As the transactions list shows a guest owner. |

### Payments

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Payment ID | |
| `State` | State | `Created`, `Pending`, `Succeeded`, `Failed` or `Canceled`. |
| `ProviderKey`, `GatewayMode` | Payment method, Gateway mode | |
| `ProviderReference` | Gateway reference | As the payments ledger shows it. |
| `FailureReason`, `IsSuperseded`, `RiskLevel` | Why it failed, Replaced by a later payment, Payment risk | |
| `SessionId` | Checkout session ID | Joins **Checkout sessions**. |
| `ReferenceType`, `ReferenceId` | Reference type, Reference ID | What the payment was for. |
| `TransactionId` | Transaction ID | The reference when it is a transaction. Joins **Transactions**. |
| `OrderId` | Order ID | The reference when it is an order. Joins **Orders**. |
| `Currency` | Currency | |
| `ExpectedAmount`, `ExpectedTaxAmount` | Expected amount, Expected tax | What was asked for, in the payment's currency. |
| `ConfirmedAmount`, `ConfirmedTaxAmount`, `ConfirmedTotal` | Confirmed amount, Confirmed tax, Confirmed total | What the gateway confirmed, in the payment's currency. |
| `CreatedUtc` | Started | The main date. |
| `UpdatedUtc` | Last changed | |

### Refunds

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Refund ID | |
| `Status` | Status | `Requested`, `Pending`, `Succeeded`, `Failed`, `Canceled` or `PendingManualReview`. |
| `ProviderKey`, `GatewayMode` | Payment method, Gateway mode | |
| `Reason`, `FailureCode`, `FailureReason` | Reason, Failure code, Why it failed | |
| `OriginalAttemptId` | Payment ID | The payment it gave back. Joins **Payments**. |
| `SessionId` | Checkout session ID | Joins **Checkout sessions**. |
| `ReferenceType`, `ReferenceId`, `TransactionId`, `OrderId` | Reference type, Reference ID, Transaction ID, Order ID | As for payments. |
| `Currency` | Currency | |
| `RefundGrossAmount`, `RefundTaxAmount`, `RefundTaxableAmount` | Amount, Tax refunded, Taxable amount refunded | In the refund's currency. The amount includes the tax. |
| `CreatedUtc` | Asked for | The main date. |
| `CompletedUtc`, `UpdatedUtc` | Completed, Last changed | |

## Permissions

| Permission | Grants |
| --- | --- |
| **Manage transactions** | View and manage every tenant transaction: send reminders and invoices, record payments, mark paid, adjust or waive, cancel, and add notes from the administration report. |
| **Manage transaction settings** | Configure the transaction reminder settings. |
| **View own transactions** | View and pay your own transactions. |
| **Manage payments and refunds** | View the payment and refund ledgers and issue a refund. It is deliberately separate from managing transactions, because giving money back is the one action in the suite that cannot be undone and should not come free with the ability to read the outstanding report. |

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

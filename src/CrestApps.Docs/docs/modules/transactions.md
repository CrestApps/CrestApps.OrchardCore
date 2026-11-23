---
sidebar_label: Transactions
sidebar_position: 14
title: Transactions
description: A provider-agnostic ledger that tracks, reports, and settles outstanding financial obligations from any payment provider, with customer statements, an administrator report and management console, and payment reminders delivered through the notification system.
user_manual:
  - user-manual/commerce/transactions
  - user-manual/commerce/invoices-and-receipts
  - user-manual/commerce/payment-terms
---

| | |
| --- | --- |
| **Feature Name** | Transactions |
| **Feature ID** | `CrestApps.OrchardCore.Transactions` |
| **Reminders Feature** | `CrestApps.OrchardCore.Transactions.Notification` |
| **Abstractions** | `CrestApps.OrchardCore.Transactions.Abstractions` |
| **Core** | `CrestApps.OrchardCore.Transactions.Core` |
| **Category** | Commerce |
| **Depends on** | [Commerce Foundation](commerce#the-commerce-foundation-and-commerce-features) |

The **Transactions** module is a provider-agnostic ledger of *money owed*. It answers a single question for the whole tenant — **"what has not been paid yet?"** — no matter which payment provider or purchase flow created the obligation. A transaction is created whenever a purchase is committed without being settled immediately (for example an offline [Pay Later](pay-later) commitment), and it is updated as reminders are sent and payments are recorded until it reaches a terminal state.

Because the ledger is generic, one report, one customer statement, and one reminder pipeline serve every provider. A provider only needs to create a `Transaction` for a balance it leaves outstanding; the Transactions module supplies all of the reporting, settlement, and reminder machinery.

<YouTube video="invoices-receipts" title="Narrated video overview of invoices and receipts" />

## What it does

This video shows the transaction screens in the admin:

<YouTube video="payments" title="Video overview of payments" />

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
- **Refunds stay per transaction.** **Refund** on a transaction gives back only that transaction's share of the payment, less what was already refunded for it. The refund names the transaction (`PaymentRefund.ReferenceType` and `ReferenceId`), so it is recorded on that one only. A refund of the whole payment, started from **Commerce** → **Payments** or at the gateway, names no transaction: it is split back over the transactions in the order they were listed, each up to its share.
- **Debts paid through a page of their own.** A transaction an `ITransactionPayUrlProvider` sends elsewhere (an order's invoice, paid through the order's checkout) is refused by the ledger's checkout, on its own or with others, so it is never settled without the page that owns its payment hearing of it.
- **One checkout's payment shared by debts of the same checkout.** A purchase checkout's payment can also be applied, by shares, to other transactions that record the same checkout (`CheckoutSessionId`), such as an invoice and the debit note for the shipping its order's checkout added. A refund found by the checkout and obligation goes to that transaction up to its share of the payment, and the rest to the others that took a share (`ITransactionStore.GetByCheckoutSessionAsync`), so none gives back more than it was paid.

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

**Staged reminders.** `TransactionReminderSettings.Steps` lists the days, relative to the due date, a reminder is sent
on, such as `[-3, 0, 7, 14, 30]`. When it is set it replaces the first delay, interval, maximum and upcoming reminder:
`TransactionReminderSchedule.GetDueStep` gives the latest step whose day has come and that was not sent
(`Transaction.ReminderStepsSent`), passing over earlier steps missed meanwhile, so an owner never gets several at once.
A step before the due date is sent as the upcoming notice, the others as reminders. Either way, the sweep:

- skips a transaction whose reminders are paused (`Transaction.RemindersPaused`, set by
  `ITransactionCommandService.SetRemindersPausedAsync` and the **Pause reminders** button, noted on the timeline);
- skips every transaction of a party kept out of reminders: the user who owns it or the party that pays it
  (`PartyCollectionSettings.RemindersPaused`, read once per sweep through `IPartyCollectionSettingsService.GetExclusionsAsync`);
- sends no owed reminder while a charge of a saved card is scheduled (`Transaction.AutoCharge`);
- reads the transaction again in a unit of work of its own right before each reminder, and skips it when it was paid,
  paused or closed meanwhile, or a payment for it is being taken (a checkout `AwaitingProvider` or `PaymentPending`);
- sends only between `SendFromHour` and `SendUntilHour` (8 and 20 by default) in the site's time zone. The task now runs
  every hour, so the next run in those hours sends what is due; overdue is still raised at any hour.

#### Charging on a schedule, with retries

`ISavedPaymentMethodChargeService` (`DefaultSavedPaymentMethodChargeService`, in the module) is the one
charge-and-retry path for every debt collected with a card: invoices charged automatically, installment plan payments
and booking balances. It charges a transaction on a day and retries a declined card. The charge is
`Transaction.AutoCharge` (`TransactionAutoCharge`): the card (`SavedPaymentMethodId`, one of the owner's saved cards, or
`SourceSessionId`, the checkout that kept it, which also reaches a guest's card), the next attempt, the declined
attempts so far, and the debt's own retry days (`RetryDays`, `null` for the reminder settings' ones, empty for none). A
module that owns a schedule sets it as the charge's inputs: installment plans their payments' due dates and
`InstallmentPlanSettings.RetryDays`, Bookings the balance's due date and `[1, 1]`.

| Member | What it does |
| --- | --- |
| `ScheduleAsync(id, savedPaymentMethodId, chargeOnUtc)` | Sets `Transaction.AutoCharge` (the card, the next attempt) and `AutoCollection` (the card's description, so the upcoming notice names it). `savedPaymentMethodId` `null` takes the owner's default card (`ISavedPaymentMethodStore.GetDefaultAsync`); refused when the owner kept none that can be charged, or without the Checkout feature. |
| `ScheduleAsync(SavedPaymentMethodChargeRequest)` | The same with every input: `SavedPaymentMethodId` or `SourceSessionId` (the checkout's owner must be the transaction's, a guest's for a guest's transaction), `ChargeOnUtc` and `RetryDays`. It replaces any charge before, so its declined attempts count from zero. A module that creates the transaction can instead set `Transaction.AutoCharge` before saving it, as installment plans and booking balances do. |
| `ChargeAsync(id)` | Charges now through `ChargeSavedPaymentMethodAsync`, or `ChargeKeptPaymentMethodAsync` for a card a checkout kept. `Succeeded` or `Pending` removes the charge. `Declined` counts an attempt and moves the next one by the charge's `RetryDays`, or else `ChargeRetryDays` (`[1, 3, 5]` by default, **Retry a declined card after (days)** in the settings), and raises `ITransactionEventHandler.AutoChargeDeclinedAsync` with the next attempt; the reason kept is the provider's own, from the failed charge on the timeline. `Failed` (the provider couldn't be reached) tries again in an hour without counting. `RequiresAuthentication`, a refusal before the provider was asked, or a decline with no retry left hands the transaction to its owner: the charge and `AutoCollection` are removed, a note is added, a `PaymentIssueKinds.AutoChargeFailed` payment issue is recorded, and `ITransactionEventHandler.AutoChargeAbandonedAsync` is raised so the module that owns the debt sends its own notice. |
| `ChargeDueAsync(utcNow)` | Charges up to 100 transactions whose next attempt is due (`TransactionIndex.AutoChargeNextAttemptUtc`), each in a unit of work of its own. The **Saved Card Charges** background task calls it every hour, at half past. |
| `CancelAsync(id)` | Removes the scheduled charge; the owner pays themselves. The transaction page's **Cancel the charge** button calls it. |

Each command takes the lock `Transactions:AutoCharge:{id}`. The ledger's charge commands
(`ChargeSavedPaymentMethodAsync`, `ChargeKeptPaymentMethodAsync`) first apply any succeeded payment attempt for the
transaction that is not on it yet (`ITransactionSettlementService`), so money a provider took whose recording was
interrupted is never charged a second time, and they finish a charge still settling instead of starting another.
`CheckoutKeptPaymentMethods.ReadAsync` (Checkout.Abstractions) reads the card a checkout kept from its settled attempts.

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

- **The printed invoice.** The invoice a customer opens (the transaction's payment request) is a [business document](business-documents.md) of the kind `PaymentRequest`, built by `ITransactionInvoiceBuilder` (a `BusinessDocument`, the seam an invoicing module replaces): the site's business details from the receipt settings, the charge, each adjustment still in force (one taken back is left off), a tax line per rate from `Transaction.TaxLines` (one *Tax* line when the transaction only has a tax amount), the total still owed, and the payer (`PayerContact`) as the recipient, else the owner. It is printed in the look of the document template for **Payment requests**, read once Business Documents is recorded as enabled, and rendered with the `BusinessDocument` shape.
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
  and the notice asks the owner to sign in instead. `ITransactionPayLinkService` creates and reads them. A transaction paid through a page of
  its own, such as the checkout of the order an invoice bills, is linked there instead: the first `ITransactionPayUrlProvider` that names
  a page wins, for the links, the reminders and the ledger's pay page alike.
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
| `RecordPaymentAsync(id, TransactionPaymentRequest)` | Records money received outside the site with its payment method and received time. Money above what is owed is returned in `UnappliedAmount`, or, with `CreditExcess`, added to the payer's [customer credit](#customer-credit) (`TransactionEvent.CreditedAmount`, `TransactionCommandResult.CreditedAmount`). |
| `ApplyCreditAsync(id, amount, note)` | Pays toward the transaction with the payer's [customer credit](#customer-credit): spends the amount, or what is owed when that is less, and records it as a payment with the settlement method `customer-credit`. Refused when the credit is less than the amount. |
| `MarkPaidAsync(id, note)` | Records what is still owed as an offline payment and marks the transaction `Paid`. |
| `CancelAsync(id, reason)` | Cancels a collectable transaction; the reason goes to `TransactionEvent.Reason` and `ITransactionEventHandler.CanceledAsync`. |
| `AdjustAsync(id, amount, kind, reason)` | Lowers what is owed without money being received. `kind` is a `TransactionAdjustmentKind`: `Discount`, `Waiver`, `Credit` or `WriteOff`. Adds to `Transaction.AmountAdjusted` and records a `TransactionEventType.Adjusted` event (`Amount`, `AdjustmentKind`, `Reason`). An adjustment can never exceed `OutstandingAmount`. When nothing is left to pay, the transaction becomes `Paid` with `SettlementMethod = "adjusted"`, so it is never reminded. No payment handler runs, because no money moved. Adjustments are printed as negative lines on the invoice and noted on each receipt. |
| `SendInvoiceAsync(id)` | Issues the invoice number when there is none and sends an *Invoice INV-1001* notice (not a reminder) with the pay link, through `ITransactionReminderService.SendInvoiceAsync`. It records a `TransactionEventType.InvoiceSent` event and counts as the first notice (`ReminderCount` and `LastReminderSentUtc`). Needs the **Transaction Reminders** feature; refused for a transaction collected automatically (`AutoCollection`). |
| `RefundAsync(id, amount?, reason)` | Finds the confirmed payment attempts made for the transaction (from its timeline and `IPaymentAttemptStore.GetByReferenceAsync("Transaction", id)`), and refunds them through `ICheckoutRefundService`, newest first, each up to what it still has to give back, until `amount` (or everything refundable when `null`) is refunded. Attempts from a provider that defers payment, or with a refund waiting for manual settlement, are skipped. From a payment that settled several transactions at once, only this transaction's share can be refunded, and the refund names this transaction. An amount above what can be refunded is refused before anything is refunded. `IPaymentRefundHandler` then records each refund on the transaction. Needs the **Checkout** feature. |
| `ChargeSavedPaymentMethodAsync(id, savedPaymentMethodId)` | Charges what is still owed to a card the transaction's owner kept (`ISavedPaymentMethodStore`, see [Kept payment methods](checkout.md#kept-payment-methods)), without the owner present. See [Charging a kept card](#charging-a-kept-card). Needs the **Checkout** feature and a provider that can charge saved payment methods. |

Each command returns a `TransactionCommandResult`: `Succeeded`, `Errors` (messages ready to show), the `Transaction` as saved (`null` when no transaction has that id), the timeline `Event` it recorded, the `Refunds` it requested, and, for a charge of a kept card, its `ChargeStatus`. A command takes a distributed lock on the transaction (`IDistributedLock`), loads it, checks the transaction accepts the command (`TransactionStateMachine`), saves it through `ITransactionManager` (which raises the lifecycle events) and commits the session before releasing the lock, so concurrent commands never overwrite each other; a conflict comes back as an error. Payment handlers run after that commit. The service does not check permissions; the caller decides who may run a command. The signed-in user is recorded as the event's actor.

### Customer credit

No money is left unaccounted. What a payer pays above a balance can be kept as their **customer credit**, and their credit pays toward a balance like any other payment. The credit is the payer's [store credit](customers.md#store-credit): the Customers **Store Credit** feature registers `ICustomerCreditService` (Customers.Abstractions, `StoreCreditCustomerCreditService`), and Transactions offers credit only when it is registered.

- **Who can hold it.** Every payer a debt can have except a guest: a user (`PartyKinds.User`, on their customer record, made the first time they are given credit), a customer record (`PartyKinds.Customer`), and any other party, such as a business account (`PartyKinds.Account`), on store credit of its own keyed by the party. `ICustomerCreditService.CanHold(party)` says so, and the transaction page shows the payer's credit for an account payer too. Staff apply credit through **Manage transactions**; a customer may spend a party's credit themselves only with billing access to it (`ICustomerCreditService.CanSpendAsync`: their own, or an account they are an admin of).
- **Excess to credit.** `RecordPaymentAsync` with `TransactionPaymentRequest.CreditExcess` keeps what was received above the balance as the payer's credit, and refuses the payment at once when the payer can't hold credit or the feature is off. The payment keeps `CreditedAmount`. On **Record payment**, the checkbox **Keep anything above what is owed as customer credit** sets it; without it, the console warns that the rest was not recorded.
- **Applying credit.** `ApplyCreditAsync` spends the credit, never more than the payer has, and records a payment with `TransactionsConstants.SettlementMethods.CustomerCredit`. The transaction page shows **Apply customer credit** when the payer has credit in the transaction's currency.
- **Taking back.** Taking back a payment made with credit gives the credit back; taking back a payment whose excess was kept as credit takes that credit back, and is refused when it was spent already.
- **The entry first, the credit after it.** A command saves its timeline entry first, as the durable intent: the entry carries the change in `TransactionEvent.CreditChange` with `CreditState = Pending` (and the transaction index flags it in `PendingCredit`). The credit is then changed with the entry's reference (`Transaction:{transactionId}:{entryId}`, stored as `StoreCreditActivity.Reference`), so repeating it changes nothing, and the entry is marked `Settled`. Nothing is undone and the transaction is saved once.
- **Finishing what a stop left.** An entry a stopped process left `Pending` is completed the next time a command loads the transaction, and by the **Transaction Payment Reminders** background task, which completes up to 100 such transactions each run. `ITransactionCommandService.ReconcileCreditAsync(id)` completes them on demand. A spend that finds too little credit (it was spent elsewhere meanwhile) takes its payment back with a reversing entry. A change that can't be made (the feature was turned off, or the payer can't hold credit) marks the entry `Failed` and records a payment issue of kind `PaymentIssueKinds.CreditNotReconciled` through `IPaymentIssueRecorder` for someone to handle. What did not finish with the command comes back in `TransactionCommandResult.Warnings`, which the transaction page shows.

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
| `RefundedAsync(transaction, refund)` | Once per refund, after the refund was recorded on the timeline (`refund` is that `Refunded` entry, with the amount this transaction gave back). A refund reported twice by the provider raises it once. |
| `ReversedAsync(transaction, reversal, reversed)` | Once, after a payment recorded by hand or an adjustment was taken back with a reversing entry (`reversal`, with the reason; `reversed` is the entry taken back), so what it paid or took off is owed again. |

`RefundedAsync` and `ReversedAsync` have default implementations that do nothing, so a handler that implements `ITransactionEventHandler` directly and was written before them keeps compiling.

Every place a transaction changes goes through the manager, so the events cover the console (**Cancel**, **Mark paid**, **Record payment**), an online settlement, a refund that brings what was kept to zero (`Refunded`), an installment charge that was declined (`Failed`, then `Outstanding` once no retries are left, or `Paid` when a retry goes through), and a canceled installment plan. The manager diffs the status itself, so a consumer no longer needs a catalog handler that re-reads every save.

Handlers run after the save, and they are resolved when the event is raised (not injected into the manager), so a handler may depend on `ITransactionManager`. A handler that throws is logged and does not undo the change or stop the other handlers, the same as `ITransactionPaymentHandler`. The overdue sweep is part of the **Transaction Reminders** feature's background task (`TransactionReminderBackgroundTask`, every hour). It runs even when **Enabled** is turned off in the reminder settings, which only stops the reminders themselves.

`AutoChargeDeclinedAsync(transaction, reason, nextAttemptUtc)` is raised when a scheduled charge was declined and a retry
is set, so the module that owns the debt records it and tells the customer (installment plans, booking balances); a
charge that could not be made at all is retried within the hour without it. `AutoChargeAbandonedAsync(transaction, reason)` is raised once when `ISavedPaymentMethodChargeService` stops charging a saved card and hands the transaction to its owner. It does nothing by default.

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

### Message templates

Every message about a transaction has a kind, so a store writes its own template of each one under **Settings** → **Commerce** → **Emails** without changing the others. Each template reads the transaction as `{{ Transaction }}` and these values:

| Kind | Sent when | Feature |
| --- | --- | --- |
| `payment-request` | The first request to pay a debt: **Send invoice** on the transaction, or `ITransactionCommandService.SendInvoiceAsync`. | Transaction Reminders |
| `payment-reminder` | A reminder before, on or after the due date, from the sweep or sent by hand. | Transaction Reminders |
| `payment-receipt` | After every payment. | Send Payment Receipts |
| `payment-refund` | After every refund. | Send Payment Receipts |

The request and the reminders pass `{{ PayUrl }}` (the link that pays it without signing in, when one could be made), `{{ DocumentUrl }}` (the printable invoice, when the payer can open one), `{{ DocumentNumber }}`, `{{ AmountDue }}` (formatted with its currency) and `{{ DueDate }}` (a short date in the site's time zone). The names are constants on `TransactionEmailKinds`. Before this release the request used the reminder template.

### Workflows

With **Workflows** on, the ledger starts these events, all in the **Transactions** category: **Transaction Created**, **Transaction Payment Recorded**, **Transaction Settled** (nothing more is owed), **Transaction Overdue**, **Transaction Canceled**, **Transaction Refunded** and **Transaction Entry Taken Back** (a payment recorded by hand or an adjustment was taken back; its input adds `Reason`, `ReversedEventId` and `ReversedEventType`). Each can be limited to some sources and some reference types (comma-separated, without regard to case). The workflow input holds `TransactionId`, `Title`, `Source`, `Status`, `Currency`, `TotalAmount`, `AmountPaid`, `AmountAdjusted`, `AmountRefunded`, `OutstandingAmount`, `OwnerId`, `PayerKind`, `PayerId`, `ReferenceType`, `ReferenceId`, `InvoiceNumber` and `DueUtc`; a payment or a refund also gives `EventId`, `Amount`, `Method` and `Reference`, and a cancellation `Reason`. The events start after the request's work is saved, so a workflow that runs a command on the same transaction never waits on the lock of the command that raised the event.

Three tasks run ledger commands: **Record Transaction Payment** (amount, payment method, reference, note), **Cancel Transaction** (reason) and **Adjust Transaction** (amount, kind `Discount`, `Waiver`, `Credit` or `WriteOff`, reason). Their fields are Liquid. Each ends with `Done`, `NotFound` or `Failed`, and writes the transaction and the command's `Errors` to the output named `Transaction` (or the name you give). The tasks have schemas for the CrestApps Recipes feature.

### Webhooks

With **Commerce Webhooks** on, the ledger publishes `transaction.created`, `transaction.payment-recorded`, `transaction.settled`, `transaction.overdue`, `transaction.canceled`, `transaction.refunded` and `transaction.entry-reversed` (`TransactionsConstants.WebhookEvents`). The payload holds `transaction` (its id, title, source, status, currency, amounts, payer, reference, invoice number and dates) and, for a payment, a refund or a reversal, `entry` (the timeline entry, with its reason and document number); a reversal also has `reversedEntry`, the entry taken back, so a system that mirrored the payment learns it was withdrawn. Each delivery has a stable event id built from the transaction and the entry, so a receiver can drop a repeat.

### Adding a card to the transaction page

The administration page of a transaction is built by the display drivers of `Transaction` for the display type `TransactionsConstants.DisplayTypes.DetailAdmin` (`DetailAdmin`). The Transactions module places the summary (`Content:5`), the timeline (`Content:20`) and the actions (`Actions:5`). Another module adds a card with its own driver, such as the document a transaction was posted for:

```csharp
public sealed class InvoiceTransactionDisplayDriver : DisplayDriver<Transaction>
{
    public override IDisplayResult Display(Transaction transaction, BuildDisplayContext context)
    {
        if (context.DisplayType != TransactionsConstants.DisplayTypes.DetailAdmin || transaction.ReferenceType != "Invoice")
        {
            return null;
        }

        return View("InvoiceTransactionCard_DetailAdmin", transaction).Location(TransactionsConstants.DisplayTypes.DetailAdmin, "Content:10");
    }
}
```

## Using the module

### Customer statement — "My Transactions"

Authenticated users with the **View own transactions** permission get a **My Transactions** entry in the admin navigation. Consistent with the administrator report, it offers a search bar, a status filter in the list header (including an *outstanding* view), and a pager, and lets them open a transaction and **Pay** an outstanding balance online. Paying opens the checkout straight on the payment step: the balance being paid is shown in the order summary, so there is no separate step for it.

The same statement, and the customer's invoices and receipts, are also available in the site theme; see [Customer pages in the site theme](#customer-pages-in-the-site-theme).

### Administrator report and console

Users with the **Manage transactions** permission get a **Commerce** → **Transactions** report. It searches by title, and its list header filters by status (including an *outstanding* view) and by **source**, from the sources registered by the enabled features. Each row shows the owner, source, total, outstanding balance and due date as badges. Opening a transaction reveals its full timeline and the management actions, each of which asks for confirmation before it changes anything:

- **Send reminder** — deliver a payment reminder now. Available only when the **Transaction Reminders** feature is enabled.
- **Record payment** — record a full or partial payment received offline, with an optional reference, how it arrived and when. The payment method field suggests the common methods; one picked or typed by its label or key is stored as its stable key from `TransactionsConstants.OfflinePaymentMethods` (`cash`, `check`, `bank-transfer`, `card-terminal`, `other`), so reports and webhooks group payments the same in every language, and any other text is kept as typed. The timeline shows the localized label.
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
- The statement needs **View own transactions** and lists the transactions the viewer may see as their own, decided by `ITransactionVisibilityService` (Transactions.Abstractions): those their account owns (`OwnerKind` `Authenticated` and `OwnerId` equal to the user's identifier) and those whose payer is a party they have billing access to (`IPartyMembershipService` with `PartyAccess.Billing`): their customer record, or a business account they are an **admin** of. An account admin sees, opens and pays all of the account's debts, whoever placed the order; a plain member (a buyer) sees only the transactions they own, also those billed to the account. A guest payer is never matched. See [Access levels](parties.md#access-levels-member-and-billing).
- An invoice or a receipt needs **View own transactions** or **Manage transactions**; neither answers `403`. A customer sees only a transaction `ITransactionVisibilityService` lets them see: one their account owns or one owed by a party they have billing access to. A guest transaction is never shown to an account, even one that shares its identifier: a guest reaches their invoice through the signed [pay link](#invoices-and-pay-links). A user with **Manage transactions** sees any invoice or receipt, and the model's `IsAdministrator` is `true`.
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

## Payment terms

The **Payment Terms** feature (`CrestApps.OrchardCore.Transactions.PaymentTerms`) keeps the store's payment terms, a small catalog that invoices, account terms, pay-later purchases and work orders read instead of storing a bare number of days. Staff keep it under **Commerce** → **Payment Terms** (permission **Manage payment terms**).

A `PaymentTerm` (`CrestApps.OrchardCore.Transactions.Abstractions`, namespace `CrestApps.OrchardCore.Transactions.Models`) has a `Name`, a `Kind` and its numbers, an optional early-payment discount and `Enabled`:

| `Kind` | Due on |
| --- | --- |
| `DueOnReceipt` | The issue date. |
| `NetDays` | The issue date plus `Days`. |
| `EndOfMonth` | The last day of the issue month plus `Days`. |
| `DayOfNextMonth` | Day `DayOfMonth` of the month after the issue month, or its last day when the month is shorter. |

`DiscountPercent` with `DiscountDays` offers a discount for paying within that many days of the issue date, the last day included.

Read the terms through `IPaymentTermManager` (a catalog manager with `GetEnabledAsync`) and turn one into dates with `IPaymentTermCalculator`:

```csharp
var term = await paymentTermManager.FindByIdAsync(termId);
var dueDate = paymentTermCalculator.GetDueDate(term, issueDate);
var discount = paymentTermCalculator.GetEarlyPaymentDiscount(term, issueDate, total, currency);
```

Both work on `DateOnly` local dates in the site's time zone; convert the due date to a UTC time only when storing it on the ledger (`Transaction.DueUtc` is the end of the due date in the site's zone). The discount is rounded to the currency with `CurrencyScale`. A due date is never before the issue date.

The terms travel with recipes and deployment plans: the step is `PaymentTerms`, with the terms under `Terms`, matched by `ItemId`:

```json
{
  "name": "PaymentTerms",
  "Terms": [
    { "ItemId": "4paymentterm00000000000001", "Name": "Net 30", "Kind": "NetDays", "Days": 30, "Enabled": true },
    { "ItemId": "4paymentterm00000000000002", "Name": "2/10 net 30", "Kind": "NetDays", "Days": 30, "DiscountPercent": 2, "DiscountDays": 10, "Enabled": true }
  ]
}
```

The recipe step and the deployment step have schemas for the CrestApps Recipes feature.

## Keeping a party out of reminders and late fees

`PartyCollectionSettings` (Transactions.Abstractions; `PartyCollectionSettingsIndex` in the `Transactions` collection, migration step 6) is kept once per party: **reminders paused** and **late fees exempt**. `IPartyCollectionSettingsService` gets and saves it, and `GetExclusionsAsync` reads the excluded parties for a sweep (`PartyCollectionExclusions.AreRemindersPaused(transaction)` for the owner user or the payer, `IsLateFeeExempt(party)`). `PartyCollectionSettingsBillingProfileSource` puts both into the party's billing profile (`PartyBillingProfile.RemindersPaused` and `LateFeesExempt`) for any kind of party: a billing profile source of kind `PartyKinds.Any` (`*`) contributes to every party. Nothing is copied onto debts or invoices; the reminder sweep and Invoicing's late fees read it when they run. An account's page (Overview section, with Accounts) and a customer's page (with Customers) have a **Reminders and late fees** card (**Manage transactions**) to change it.

## Moving the ledger between sites

With **Deployment** enabled, the **Transactions** deployment step exports the whole ledger in one `Transactions` recipe step: every transaction with its timeline (`Events`), and, while Checkout keeps them, the payment attempts and refunds, and the payment issues, under `Transactions`, `PaymentAttempts`, `PaymentRefunds` and `PaymentIssues`. Each record carries everything but the storing site's document id, including when it was made and last changed.

`TransactionLedgerImporter` (Transactions.Core) imports the step. Records are written to their stores as they are, never through the ledger's commands: nothing is posted, numbered, e-mailed or announced to the event handlers, workflows or webhooks. A record the site already has (by `ItemId`) is kept as it is, so a recipe never rewrites a ledger. Payment attempts and refunds need the Checkout feature on the importing site; without it the step reports an error and imports the rest. `TransactionLedgerExporter.ExportAsync(ids)` writes the same step for a set of transactions with the attempts made toward them and their refunds: the Invoicing deployment step uses it to carry the transactions of the invoices it exports. With CrestApps Recipes, the recipe and deployment steps have schemas.

Receipt numbers on imported payments are kept; the importing site's receipt series does not know them.

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

Reminder settings appear only when the **Transaction Reminders** feature (`CrestApps.OrchardCore.Transactions.Notification`) is enabled. Under **Settings** → **Commerce** → **Transactions** (requires the **Manage transaction settings** permission) you can configure the scheduled reminder sweep:

| Setting | Purpose | Default |
| --- | --- | --- |
| **Enabled** | Turns the scheduled reminder sweep on or off. | `true` |
| **First reminder delay (days)** | Days to wait after a transaction becomes due before the first reminder. | `0` |
| **Reminder interval (days)** | Days to wait between reminders. | `7` |
| **Maximum reminders** | Maximum reminders per transaction (`0` = no limit). | `3` |
| **Remind before the due date (days)** | How many days before a payment falls due the owner is told it is coming due, once per payment (`0` = off). | `3` |

The background task runs the sweep on a schedule and sends a reminder only when a transaction is due for one under this cadence, up to the maximum.

## Payments and refunds

With [Checkout](checkout) enabled, **Commerce** → **Payments** lists every payment the suite has attempted, filtered by state and payment method, showing what was expected, what actually settled, and the gateway's own transaction reference. It answers "did this customer actually pay?" without reading the database.

It is also the only place a refund starts. **Refund** isn't offered on a payment from a provider that defers payment (`PaymentProviderCapabilities.DefersPayment`, such as Pay Later), since nothing was collected to give back. A refund the service refuses, because another refund for the same payment holds the lock, an earlier one still needs manual settlement, or the checkout is gone, comes back as a message rather than an error page. The form shows what was collected and what has already been given back, refuses an amount above what is still refundable, and issues the refund through the refund service so the durable ledger, the original payment's tax allocation, and the over-refund protection all apply. Counting an in-flight refund against the remaining amount is deliberate: ignoring one would let an operator start a second refund for money already on its way back.

**Commerce** → **Refunds** lists what came of them. A refund against a payment method that has no executable refund operation is recorded as **needing manual settlement** rather than silently dropped, and this screen is where an operator records the reference once they have moved the money by hand. Only a refund in that state can be closed this way; marking a failed gateway refund as done would tell the ledger the customer was paid when they were not.

**Commerce** → **Payment Issues** (`ManageRefunds`) lists the money operations that failed and must be finished by hand, open ones first: a refund the checkout could not issue while undoing a checkout it could not complete, a cancel, pause or resume the payment provider refused for a subscription (so the customer may still be billed), and a promotion code whose use was not counted after a settled payment. Each row says what to do, why it failed, the provider and its reference; **Mark resolved** records what the operator did. The code that hits the failure still logs it.

A module records its own issue through `IPaymentIssueRecorder.RecordAsync(PaymentIssue)` (`Transactions.Abstractions`; `services.AddPaymentIssues()` in `Transactions.Core`, registered by Checkout). The kind is an open string; the built-in kinds are in `PaymentIssueKinds`. Recording never throws, so it is safe inside a failure handler.

Both screens require the **Manage payments and refunds** permission.

## Report Builder data source

When **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) is enabled, as it is with the [Report Builder](report-builder/index.md), the **Transactions** feature registers the **Transactions** data source (`Transactions`), so reports and data pipelines can read the ledger, the outstanding balances, and the payments and refunds. The payments and refunds data sets are registered only while [Checkout](checkout), which keeps those ledgers, is enabled too. Each data set reads its newest records first, a page at a time, through its index. Money is in each record's own currency, which the **Currency** field next to it names. Identifier fields reference the data sets they point to, so checkout sessions, orders, users and payments join automatically.

| Data set | One row per | Feature | Permission |
| --- | --- | --- | --- |
| **Transactions** (`Transactions`) | Transaction of the ledger | Transactions | **Manage transactions** (`ManageTransactions`) |
| **Payments** (`PaymentAttempts`) | Payment the checkout tried to take | Transactions with Checkout | **Manage payments and refunds** (`ManageRefunds`) |
| **Refunds** (`PaymentRefunds`) | Refund of a payment | Transactions with Checkout | **Manage payments and refunds** (`ManageRefunds`) |

The outstanding transactions are those with the status `Outstanding` or `PartiallyPaid`; a status filter narrows the read in the database, as do the date range a report puts on the main date of transactions and payments and the keys of a join. The refunds index holds no creation date, so refunds have no main date: they are read newest first, and a date range a report puts on them is applied to the rows read. A transaction's history and notes, its settlement reference, and the keys a payment or refund is made with are never exposed.

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
| `CreatedUtc` | Asked for | |
| `CompletedUtc`, `UpdatedUtc` | Completed, Last changed | |

## Permissions

| Permission | Grants |
| --- | --- |
| **Manage transactions** | View and manage every tenant transaction: send reminders and invoices, record payments, mark paid, adjust or waive, cancel, and add notes from the administration report. |
| **Manage transaction settings** | Configure the transaction reminder settings. |
| **View own transactions** | View and pay your own transactions. |
| **Manage payments and refunds** | View the payment and refund ledgers and issue a refund. It is deliberately separate from managing transactions, because giving money back is the one action in the suite that cannot be undone and should not come free with the ability to read the outstanding report. |
| **Manage payment terms** | Create, change and delete the payment terms (Payment Terms feature). |

## Enabling the feature

Add the package to your Orchard Core project:

```bash
dotnet add package CrestApps.OrchardCore.Transactions
```

Then, in the **Orchard Core Admin Dashboard** under **Tools** → **Features**, enable **Transactions**. Enabling it also enables the **[Commerce Foundation](commerce#the-commerce-foundation-and-commerce-features)** feature it depends on, which owns the shared Commerce admin menu and icon; it does not turn on the store's setup guide.

Enable **[Checkout](checkout)** as well if you want customers to settle outstanding balances online. To send manual and scheduled payment reminders, enable the **Transaction Reminders** feature (`CrestApps.OrchardCore.Transactions.Notification`); it depends on the OrchardCore **[Notifications](https://docs.orchardcore.net/en/latest/reference/modules/Notifications/)** feature, which is enabled automatically as a dependency.

## Related modules

- [Commerce](commerce) — owns the shared Commerce admin menu and icon this module contributes to.
- [Pay Later](pay-later) — records offline commitments as outstanding transactions in this ledger.
- [Checkout](checkout) — the provider-agnostic checkout framework used to settle outstanding transactions online.
- [Subscriptions](subscriptions) — a consumer of checkout that can leave balances a transaction tracks.
- [Payments](payments) — the lower-level payment contracts and the Stripe provider.

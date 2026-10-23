# CrestApps OrchardCore Transactions

The Transactions module is the provider-agnostic ledger of what customers owe, what they have paid, and what
has been given back. Any module that creates a financial obligation records it here, so a site owner has one
report to read instead of one per payment method.

## Why this module exists

A balance that only exists inside the feature that created it cannot be chased, reported on, or paid off
anywhere else. Pay Later commitments, subscription invoices, and one-off purchases all produce the same kind
of fact — somebody owes an amount by a date — and the useful things to do with that fact are the same too:
show it to the customer, remind them, let them settle it online, and report on what is outstanding.

## Features

- **Transactions** (`CrestApps.OrchardCore.Transactions`) — the ledger, the administration report, the
  customer statement, and the payments and refunds screens.
- **Transaction Reminders** (`...Notification`) — chases unpaid balances on a configured cadence through
  each user's preferred notification channel.

## What a transaction records

Who owes it (an account, or a guest and their contact details), what it is for, how much including tax, how
much has been paid, when it is due, its lifecycle state, and an event trail of everything that happened to
it. A recurring transaction also carries the billing cycle it covers, so an offline agreement can be
invoiced again when the period ends.

The state transitions are guarded by a shared state machine rather than by each screen, so an operator
cannot record a payment against a cancelled balance or settle one twice.

## Payments and refunds

With Checkout enabled, **Commerce → Payments** lists every payment the suite has attempted and is the only
place a refund starts. The form refuses an amount above what is still refundable, counting refunds already
in flight, and issues the refund through the refund service so the ledger, the original payment's tax
allocation, and the over-refund protection all apply.

**Commerce → Refunds** surfaces refunds that a payment method cannot settle automatically, so an operator
can record the reference once they have moved the money by hand. Only a refund in that state can be closed
that way: marking a failed gateway refund as done would tell the ledger a customer was paid when they were
not.

Giving money back is the one action in the suite that cannot be undone, so it needs its own **Manage
payments and refunds** permission rather than coming free with the ability to read the outstanding report.

## Customer pages in the site theme

Besides the customer pages in the admin, a signed-in customer can open their statement at `my-transactions`, an invoice at `my-transactions/invoices/{itemId}` and a payment's receipt at `my-transactions/receipts/{itemId}/{paymentId}`, in the site theme. Each page renders a shape a theme can override: `TransactionStatement`, `TransactionInvoice` and `TransactionReceipt`. `ITransactionCustomerPageService` (Transactions.Abstractions) builds their models with the same ownership checks as the admin pages, so another module can show them in its own pages; `ITransactionInvoiceBuilder` and `ITransactionReceiptBuilder` live in the abstractions too.

Set `CrestApps:Transactions:CustomerPages:LinkTarget` to `Site` to make the links in receipt and reminder messages open these pages instead of the admin ones.

## Recording a transaction from your own module

Inject `ITransactionManager`, create the entry with your own reference type and id, and set a source key so
the report can group and label it. Register an `ITransactionSource` to give that key a display name.

Make creation idempotent on something stable — the checkout session and obligation, for example — because
the pipelines that create obligations can legitimately run more than once.

## Acting on a transaction from your own module

Inject `ITransactionCommandService` to record an offline payment, mark a transaction paid, cancel it, adjust or
waive what is owed (`AdjustAsync` with a discount, waiver, credit or write-off), send its invoice now, refund
it, or charge it to a card its owner kept (`ChargeSavedPaymentMethodAsync`, which reports whether the charge
succeeded, was declined or needs the cardholder's approval). The Commerce console uses the same service, so the timeline, receipts and handler calls are identical. Each
command returns a result with errors and runs under a lock on the transaction.

## Reacting to a transaction from your own module

Implement `ITransactionEventHandler` (or derive from `TransactionEventHandlerBase`) to hear about every change:
`CreatedAsync`, `StatusChangedAsync` with the previous and current status, `CanceledAsync` with the reason, and
`BecameOverdueAsync`, raised once per transaction by the reminder sweep. They run after the save, and a handler
that fails is logged without undoing the change. `ITransactionPaymentHandler` and `IPaymentRefundHandler` report
each payment and refund.

## Documentation

Full documentation: <https://docs.crestapps.com/docs/modules/transactions>

## License

This project is licensed under the MIT License.

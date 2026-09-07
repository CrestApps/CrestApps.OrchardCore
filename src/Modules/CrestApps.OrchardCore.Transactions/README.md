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

## Recording a transaction from your own module

Inject `ITransactionManager`, create the entry with your own reference type and id, and set a source key so
the report can group and label it. Register an `ITransactionSource` to give that key a display name.

Make creation idempotent on something stable — the checkout session and obligation, for example — because
the pipelines that create obligations can legitimately run more than once.

## Documentation

Full documentation: <https://docs.crestapps.com/docs/modules/transactions>

## License

This project is licensed under the MIT License.

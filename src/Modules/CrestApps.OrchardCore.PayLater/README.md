# CrestApps OrchardCore Pay Later

The Pay Later module adds an offline payment option to the checkout: the customer commits to pay, gets what
they bought, and the balance is tracked until it is settled. It is how invoiced business customers, purchase
orders, and "bill me" arrangements work alongside card payments.

## Why this module exists

Not every sale is a card transaction, and the ones that are not still have to be tracked. A commitment that
is only recorded as "checkout completed" is money nobody chases. So Pay Later is a real payment provider as
far as the checkout is concerned, and every commitment it records becomes an outstanding entry in the
provider-agnostic [Transactions](../CrestApps.OrchardCore.Transactions/README.md) ledger.

Being a real provider is also what makes it honest. It never moves money at a gateway, so it reports that it
is **not** the authoritative source of a charged amount. The checkout therefore records the commitment on
the strength of its reference alone, without pretending a gateway confirmed a total. That keeps the same
reconciliation guarantees as a card payment without ever fabricating a settlement.

## Features

- **Pay Later** (`CrestApps.OrchardCore.PayLater`) — the payment provider, the checkout panel, the net-term
  setting, and the renewal sweep for recurring commitments.

## What happens after checkout

Each succeeded Pay Later payment leaves a balance the customer still owes, recorded with a due date derived
from the configured net term. From there it behaves like any other obligation: the customer sees it on their
statement and can pay it online later, an administrator sees it in the outstanding report, and the reminder
pipeline chases it.

Recording is idempotent per obligation, so a checkout that completes more than once never duplicates a debt,
and settling an existing balance never creates a new one.

## Recurring commitments

Pay Later can back a subscription as well as a one-off purchase. There is no gateway keeping the schedule,
so the ledger carries it, and a sweep invoices the next period once the current one ends. Without that, a
recurring commitment would be invoiced exactly once and then quietly stop, leaving the customer with what
they subscribed to and the site owner with nothing to chase.

Each cycle is created exactly once: the sweep locks the commitment it advances, marks the cycle it came from
as spawned before committing, and gives the new period its own obligation id, so a retry, a second node, or
a restart cannot invoice the same period twice.

## Configuration

Under **Settings → Commerce → Pay Later**, set the net term in days. It becomes the due date on each
recorded balance and drives the reminder cadence. A value of `0` records the balance with no due date.

## Documentation

Full documentation: <https://docs.crestapps.com/docs/modules/pay-later>

## License

This project is licensed under the MIT License.

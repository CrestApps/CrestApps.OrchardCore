# CrestApps OrchardCore Taxation

The Taxation module works out what tax applies to a sale, line by line, and captures the answer as an
immutable snapshot on the transaction.

## Why this module exists

It is built on one principle:

> Tax is a determination, not a property.

Storing a tax rate on a product is the intuitive design and it is wrong in every interesting case. The tax
owed depends on *what* is sold, *who* is buying, *where* it is sourced from and shipped to, *when* the sale
happens, and *how* the rule calculates. A stored rate cannot express a customer with an exemption
certificate, a jurisdiction whose rate changed last quarter, a threshold that only applies once a seller has
enough sales in a state, or a product that is taxable in one place and not another.

Worse, a stored rate quietly rewrites history. Refunding a sale from last year with this year's rate gives
the customer back the wrong amount, and the difference is the site owner's to explain. So a determination is
snapshotted onto the transaction, and every later calculation — a partial refund especially — reads that
snapshot rather than re-rating with today's rules.

## Features

- **Taxation** (`CrestApps.OrchardCore.Taxation`) — the determination engine, the jurisdiction, rule, table,
  exemption, and nexus catalogs, the `TaxationPart`, and the reports.

## Assemblies

| Assembly | Purpose |
| --- | --- |
| `...Taxation.Abstractions` | Contracts, models, and constants. No infrastructure dependencies. |
| `...Taxation.Core` | The determination engine, calculation methods, sourcing strategies, resolvers, and catalog stores. |
| `...Taxation` | The Orchard Core module: the content part, its editors, migrations, and admin screens. |

## Plugging in

Any content type or module becomes taxable by supplying an `ITaxableItem`. Ask `ITaxService` for a
determination, then persist the resulting `TaxSnapshot` alongside whatever recorded the money. Refunds go
through `ITaxRefundCalculator`, which allocates back from that snapshot rather than recalculating.

## Documentation

Full documentation: <https://docs.crestapps.com/docs/modules/taxation>

## License

This project is licensed under the MIT License.

# CrestApps OrchardCore Taxation

The Taxation module works out what tax applies to a sale, line by line, and captures the answer as an
immutable snapshot on the transaction. Its Income Tax features work out the taxes on a payment of income (income
tax withholding and the contributions on earnings) for payroll or any other module that pays income.

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
- **Income Tax** (`CrestApps.OrchardCore.Taxation.IncomeTax`) — income tax rules for any jurisdiction of any
  country (a rate, a fixed amount, a table, progressive brackets or a country's withholding method, paid by the
  earner or the payer, with wage bases, yearly exemptions and maximums, and the payer's own registration rate), the
  built-in `IIncomeTaxProvider`, and **Commerce > Taxation > Income tax rules**, behind the Administrator-only
  `ManageIncomeTaxRules` permission (security critical: the rules decide every payer's withholding).
- **United States Income Tax** — Form W-4 withholding (Publication 15-T) and the 2026 federal rules as a recipe.
- **Canada Income Tax** — the CRA's T4127 federal and provincial formulas and Revenu Québec's TP-1015.F, with
  CPP, CPP2, EI, QPP, QPP2, QPIP and the Quebec health services fund, and the 2026 rules as a recipe.

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

A module that pays income builds an `IncomeTaxRequest` (the payer's registrations, the earner's jurisdictions and
elections, the income of each tax category, what was taxed earlier in the year) and asks the `IIncomeTaxProvider`
for the tax lines. The request names no module's records. A tax engine plugs in as another `IIncomeTaxProvider`; a
country adds its withholding formulas as `IIncomeTaxWithholdingMethod`s and its election forms through
`IncomeTaxElectionOptions`.

## Documentation

Full documentation: <https://docs.crestapps.com/docs/modules/taxation>, and for people setting up the rules, the
Income Tax page of the user manual (`src/CrestApps.Docs/docs/user-manual/finance/income-tax.md`).

## License

This project is licensed under the MIT License.

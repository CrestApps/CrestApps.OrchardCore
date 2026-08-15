# CrestApps.OrchardCore.Taxation

A provider-agnostic, extensible taxation framework for OrchardCore. It models tax as a **determination**
(from *what*, *who*, *where*, *when*, and *how*) rather than a static property on a product.

## Features

- `TaxationPart` that lets any content type participate in taxation by classification only (no stored rate).
- A deterministic taxation engine (`ITaxService`) that produces an auditable, line-by-line breakdown.
- Extensible tax types, jurisdictions, categories, rules, tax tables, calculation methods, sourcing
  strategies, exemptions, and merchant nexus.
- Historical tax snapshots (`ITaxSnapshotFactory`) so transactions are never recalculated with new rules.
- Clean extension points for third-party modules and external tax providers.

## Getting started

Enable the **Taxation** feature. Attach the **Taxation** part to any content type and classify it with a
tax category and classification. During checkout, resolve the content item into an `ITaxableItem` and call
`ITaxService.CalculateAsync`.

See the full documentation on the CrestApps documentation site under **Taxation**.

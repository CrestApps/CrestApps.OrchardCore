# CrestApps Orchard Core Invoicing

Invoices and credit notes posted to the [Transactions](../CrestApps.OrchardCore.Transactions/README.md) ledger.

| Feature | ID | Depends on |
| --- | --- | --- |
| Invoicing | `CrestApps.OrchardCore.Invoicing` | Transactions, Payment Terms, Business Documents, Resources |

- **Documents.** `Invoice` and `CreditNote` (Invoicing.Abstractions) are catalog records in the `Invoicing` collection with `InvoiceIndex` and `CreditNoteIndex`. Drafts go through `IInvoiceManager`; `InvoiceHandler` refuses lines out of range and any change after issue.
- **Commands.** `IInvoiceCommandService` issues (number, transaction, one unit of work), voids (refused when a credit note exists or anything was paid, decided by the ledger under its lock), writes off and reinstates. Each takes a lock on the invoice and is idempotent. `IInvoiceObserver` hears of each after the save.
- **Tax.** `IDocumentCalculator` with the `IDocumentTaxProvider`s; with Taxation enabled, `TaxationDocumentTaxProvider` taxes lines for the bill-to's party tax profile.
- **Admin.** Commerce > Invoices: the list (`AdminList`, List and Grid), the draft editor with line rows and a bill-to selector over every party kind, and the invoice's page built by display drivers for `InvoicingConstants.DisplayTypes.DetailAdmin`, with issue, void, write off, reinstate and delete draft. `InvoiceCheckoutReferenceDescriber` names the invoice on its transaction.
- **Settings.** `InvoicingSettings` under Settings > Commerce > Invoicing.
- **Permissions.** `InvoicingPermissions`: View, Manage, Issue, Void, Credit notes, Record payments, Settings, View own.

See the [Invoicing documentation](https://orchardcore.crestapps.com/docs/modules/invoicing).

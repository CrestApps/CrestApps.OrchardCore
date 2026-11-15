# CrestApps Orchard Core Invoicing

Invoices and credit notes posted to the [Transactions](../CrestApps.OrchardCore.Transactions/README.md) ledger.

| Feature | ID | Depends on |
| --- | --- | --- |
| Invoicing | `CrestApps.OrchardCore.Invoicing` | Transactions, Payment Terms, Business Documents, Resources |
| Invoicing Estimates | `CrestApps.OrchardCore.Invoicing.Estimates` | Invoicing |
| Invoicing Recurring | `CrestApps.OrchardCore.Invoicing.Recurring` | Invoicing |

- **Documents.** `Invoice` and `CreditNote` (Invoicing.Abstractions) are catalog records in the `Invoicing` collection with `InvoiceIndex` and `CreditNoteIndex`. Drafts go through `IInvoiceManager`; `InvoiceHandler` refuses lines out of range and any change after issue.
- **Commands.** `IInvoiceCommandService` issues (number, transaction, one unit of work), voids (refused when a credit note exists or anything was paid, decided by the ledger under its lock), writes off and reinstates. Each takes a lock on the invoice and is idempotent. `IInvoiceObserver` hears of each after the save.
- **Payments and credit notes.** An issued invoice mirrors its transaction (`InvoiceLedgerSync`, from the ledger's payment and event handlers and when its page opens). `ICreditNoteCommandService` drafts and issues credit notes that lower the balance, refund or keep customer credit.
- **Delivery.** `IInvoiceDocumentBuilder` (business document kinds `Invoice` and `CreditNote`), `IInvoicePdfService` (PDF archived at issue in `InvoiceArchiveFileStore`), `IInvoiceSender` (the `invoice-issued` e-mail with the PDF), `IInvoiceShareLinkService` and the public page `invoices/view/{token}` with viewed tracking.
- **Customer pages.** `IInvoiceVisibilityService`, **My invoices** (`my-invoices`, pay one or several), and the account page's Invoices section (with Accounts).
- **Work orders.** With Work Orders on, `InvoicingWorkOrderBilling` bills completed work orders as issued invoices with their lines.
- **Workflows and webhooks.** `IInvoiceObserver` hears a draft created, issued, sent, viewed, paid in part and in full, overdue, voided, written off, reinstated and credited. With Workflows on, nine invoice events and the tasks Create Invoice, Issue Invoice, Send Invoice, Record Invoice Payment, Void Invoice and Create Credit Note (with recipe schemas); with Commerce Webhooks on, the same events as `invoice.*` and `credit-note.issued` webhooks.
- **Recipes and deployment.** The Invoices and Credit notes deployment steps, and the `Invoices` and `CreditNotes` recipe steps (`InvoicingDocumentImporter`): issued documents keep their numbers and post nothing, issued ones the site has are kept, drafts are replaced, a held number is refused; issue passes over imported numbers.
- **Reports.** With Data Pipelines Core, the **Invoicing** data source (Invoices, Invoice lines, Invoice taxes, Credit notes); with Reports, the Receivables aging, Invoice register, Payments collected, Invoiced sales by customer, Invoiced sales by item and Invoiced tax summary reports.
- **Personal data.** `InvoicingPersonalDataContributor` exports a person's invoices and credit notes (by their user, customer records and e-mails) and on erasure clears the contact details, keeping numbers, amounts, dates, tax ids and archived PDFs.
- **Estimates.** `IEstimateCommandService` (send, accept, decline, revise, cancel, convert to an invoice or a deposit invoice, expire), Commerce > Estimates, the public page `estimates/view/{token}` to accept or decline, the `estimate-sent` e-mail, workflow events, webhooks, the recipe step and the data set.
- **Recurring plans.** `IRecurringPlanService` (activate, pause, resume, cancel, run the due occurrences once each, catch up within a limit, price rises, end by date or runs), the 30-minute run task, Commerce > Recurring invoices, workflow events, webhooks, the `RecurringPlans` recipe step and the data set.
- **Tax.** `IDocumentCalculator` with the `IDocumentTaxProvider`s; with Taxation enabled, `TaxationDocumentTaxProvider` taxes lines for the bill-to's party tax profile.
- **Admin.** Commerce > Invoices: the list (`AdminList`, List and Grid), the draft editor with line rows and a bill-to selector over every party kind, and the invoice's page built by display drivers for `InvoicingConstants.DisplayTypes.DetailAdmin`, with issue, void, write off, reinstate and delete draft. `InvoiceCheckoutReferenceDescriber` names the invoice on its transaction.
- **Settings.** `InvoicingSettings` under Settings > Commerce > Invoicing.
- **Permissions.** `InvoicingPermissions`: View, Manage, Issue, Void, Credit notes, Record payments, Settings, View own, Manage estimates, Manage recurring invoices.

See the [Invoicing documentation](https://orchardcore.crestapps.com/docs/modules/invoicing).

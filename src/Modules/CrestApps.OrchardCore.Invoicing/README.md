# CrestApps Orchard Core Invoicing

Invoices and credit notes posted to the [Transactions](../CrestApps.OrchardCore.Transactions/README.md) ledger.

| Feature | ID | Depends on |
| --- | --- | --- |
| Invoicing | `CrestApps.OrchardCore.Invoicing` | Transactions, Payment Terms, Business Documents, Resources |
| Invoicing Estimates | `CrestApps.OrchardCore.Invoicing.Estimates` | Invoicing |
| Invoicing API | `CrestApps.OrchardCore.Invoicing.Api` | Invoicing |
| Invoicing Billing | `CrestApps.OrchardCore.Invoicing.Billing` | Invoicing |
| Invoicing Collections | `CrestApps.OrchardCore.Invoicing.Collections` | Invoicing |
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
- **Estimates.** `IEstimateCommandService` (send, accept, decline, revise, cancel, convert to an invoice or a deposit invoice, invoice in part by percent of each line, expire), Commerce > Estimates, the public page `estimates/view/{token}` to accept or decline, the `estimate-sent` e-mail, workflow events, webhooks, the recipe step and the data set.
- **Billing.** `IBillableSource` and `IBillingService`: charges kept to bill later (`BillableItem`, Commerce > Unbilled charges, with markup) and Time Tracker time at an hourly rate, added to drafts from the Unbilled work card, released when the line goes or the invoice is voided; recurring plans can bill the charges.
- **API.** `InvoicingApiEndpoints` on the commerce API: list, get, draft (and issue), issue, send, void, record payments, PDF; `me/invoices` for customers.
- **Orders on account.** With Orders, `OrderInvoiceBuilder` invoices an order whose whole balance is a ledger debt (account terms, pay later), issuing the invoice against that debt (a draft with `TransactionId` set takes it over); the mirror finds such invoices by `TransactionId`.
- **Statements.** `IInvoiceStatementService`: open-item (with aging) and activity statements per customer and currency, printed as the `InvoiceStatement` business document, e-mailed with the PDF (`invoice-statement`), Commerce > Statements.
- **Deposits.** `IInvoicePrepaymentService`: a deposit invoice asked from a draft, listed once paid and deducted from the final invoice as a negative deposit line, once; an estimate's paid deposit is deducted when the work is invoiced.
- **Saved card charges.** `InvoiceAutoCharge` charges an issued invoice marked `AutoCharge` to the payer's default saved card through the ledger's `ISavedPaymentMethodChargeService`, and sends the invoice when there is no card or the retries run out.
- **Late fees.** `LateFeePolicy` (Commerce > Late fees) and `ILateFeeService`: each fee a debit note linked to the overdue invoice, once per policy step, the balance re-read first; the hourly task, the invoice page's late fees and **Charge the late fee due now**, the `LateFeePolicies` recipe step.
- **Recurring plans.** `IRecurringPlanService` (activate, pause, resume, cancel, run the due occurrences once each, catch up within a limit, price rises, end by date or runs), the 30-minute run task, Commerce > Recurring invoices, workflow events, webhooks, the `RecurringPlans` recipe step and the data set.
- **Tax.** `IDocumentCalculator` with the `IDocumentTaxProvider`s; with Taxation enabled, `TaxationDocumentTaxProvider` taxes lines for the bill-to's party tax profile.
- **Admin.** Commerce > Invoices: the list (`AdminList`, List and Grid), the draft editor with line rows and a bill-to selector over every party kind, and the invoice's page built by display drivers for `InvoicingConstants.DisplayTypes.DetailAdmin`, with issue, void, write off, reinstate and delete draft. `InvoiceCheckoutReferenceDescriber` names the invoice on its transaction.
- **Settings.** `InvoicingSettings` under Settings > Commerce > Invoicing.
- **Permissions.** `InvoicingPermissions`: View, Manage, Issue, Void, Credit notes, Record payments, Settings, View own, Manage estimates, Manage recurring invoices.

See the [Invoicing documentation](https://orchardcore.crestapps.com/docs/modules/invoicing).

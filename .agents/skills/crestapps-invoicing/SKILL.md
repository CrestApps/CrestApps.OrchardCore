---
name: crestapps-invoicing
description: How to build on the CrestApps Invoicing building block (CrestApps.OrchardCore.Invoicing) instead of keeping another invoice, bill or statement record. Use when a module needs to bill its work (an order, a work order, time, usage, a booking, a subscription period) as an invoice; react when an invoice is created, issued, sent, viewed, paid, overdue, voided or credited; add something to the printed invoice; tax invoice lines; show a customer their invoices; or move invoices between sites. Covers Invoice, CreditNote, IInvoiceManager, IInvoiceCommandService, ICreditNoteCommandService, IInvoiceObserver, IInvoiceDocumentContributor, IDocumentTaxProvider and AddDocumentTaxProvider, IInvoiceVisibilityService, InvoiceQuery with SourceType and SourceId, the Transactions ledger rules, the workflow events and tasks, the invoice.* webhooks, the Invoicing data source, recipes and personal data.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Building on Invoicing

Invoicing owns invoices and credit notes: legal documents with a number, posted to the Transactions ledger as what a party owes. Never post a debt to the ledger and call it an invoice, never number or render an invoice yourself, and never change an issued invoice outside the commands. Reference `CrestApps.OrchardCore.Invoicing.Abstractions` (models and contracts) and `CrestApps.OrchardCore.Invoicing.Core` (services) only, never the Invoicing module, and put your integration in a `[RequireFeatures(InvoicingConstants.Features.Area)]` startup unless your feature depends on Invoicing.

The authoritative description is `src/CrestApps.Docs/docs/modules/invoicing.md`; the plan and its progress log are `docs/engineering/invoicing/invoicing-plan.md`.

## The life of an invoice

1. **Draft.** `IInvoiceManager.NewAsync()`, fill it, `ValidateAsync`, `CreateAsync`. A draft is a catalog record and changes through the manager. `InvoiceHandler` refuses lines out of range and any change once issued.
2. **Issue.** `IInvoiceCommandService.IssueAsync(id)` validates and calculates it a last time, copies the issuer and the bill-to's billing profile (`IPartyBillingProfileService`), takes its number (`IFinancialDocumentNumberGenerator`, passing over numbers an imported document holds), posts one transaction (`ReferenceType = "Invoice"`, `ReferenceId` the invoice) and opens it, in one unit of work. A refused or failed issue takes no number and posts nothing.
3. **Paid.** Payments are recorded on the transaction (online through checkout, or by staff); `InvoiceLedgerSync` mirrors the ledger onto the invoice after the ledger saves. Never set `AmountPaid`, `AmountDue` or `Settlement` yourself.
4. **Corrected.** `IInvoiceCommandService.VoidAsync` (nothing paid, no credit note), `WriteOffAsync`, `ReinstateAsync`; or `ICreditNoteCommandService.CreateDraftAsync(invoiceId)` then `IssueAsync(creditNoteId)` with a `CreditNoteDisposition` of `ReduceBalance`, `Refund` or `CustomerCredit`.

Every command takes the invoice's distributed lock, is idempotent (`AlreadyDone`), returns its reasons in `Errors` rather than throwing, and checks no permission: the caller does (`InvoicingPermissions`).

## Bill your module's work

Model it on `InvoicingWorkOrderBilling` (Invoicing module, `Services/`):

- Set `SourceType` (your reference type constant) and `SourceId` (your record's id), and `Reference` (a number people recognise).
- Before creating, look for the invoice you made already: `IInvoiceManager.PageAsync(1, 1, new InvoiceQuery { SourceType = ..., SourceId = ... })`, so billing twice returns the same invoice.
- Bill a party (`DocumentParty.Party` is a `PartyRef`: `User`, `Customer`, `Account`...), not a copied name; the billing profile fills the contact at issue.
- Lines are `DocumentLine` with a `Kind` (`Item` with a `ProductId`, `Service`, `Time`, `Expense`, `Usage`, `Fee`, `Deposit`, `Text`, `Section`), `Quantity`, `UnitPrice`, an optional `DiscountPercent` or `DiscountAmount` and `TaxCategoryCode`. Leave `Net`, `TaxAmount` and `Total` to the calculator.
- Issue it with the command, or leave it a draft for staff to finish. Log and keep the draft when issue is refused.

## React to what happens

Implement `IInvoiceObserver` (every member has a default that does nothing): `CreatedAsync`, `IssuedAsync`, `SentAsync`, `ViewedAsync`, `PaymentReceivedAsync(invoice, amount)`, `PaidAsync`, `OverdueAsync`, `VoidedAsync`, `WrittenOffAsync`, `ReinstatedAsync`, `CreditNoteIssuedAsync(creditNote, invoice)`. Register it with `services.AddScoped<IInvoiceObserver, YourObserver>()`.

- You hear it after the change is saved. A failing observer is logged and the others still hear it.
- You may be called while the command still holds the invoice's lock. Defer anything slow or anything that runs another invoice command with `DeferredWork.Add(...)` (when `ShellScope.Current` is not null), as `InvoiceWorkflowObserver` does.
- A credit that settles an invoice is not a payment: you get `CreditNoteIssuedAsync`, not `PaidAsync`.

Workflows (events Invoice Created, Issued, Sent, Viewed, Payment Received, Paid, Overdue, Voided, Credit Note Issued; tasks Create Invoice, Issue Invoice, Send Invoice, Record Invoice Payment, Void Invoice, Create Credit Note) and webhooks (`InvoicingConstants.WebhookEvents`) are observers already: don't add your own for the same moments.

## Add to the printed invoice

Implement `IInvoiceDocumentContributor.ContributeAsync(InvoicingDocument source, BusinessDocument document, ...)` to add a note, a field or a section to the `BusinessDocument` the PDF, the public page and the e-mail render. Never render your own invoice view or PDF.

## Tax invoice lines

Implement `IDocumentTaxProvider` (`Order`, `ApplyTaxesAsync(DocumentCalculationContext)` returning whether it taxed the lines) and register it with `services.AddDocumentTaxProvider<T>()`. Providers are asked in order; the first that applies wins; without one, the taxes typed on the lines are used. The Taxation module's `TaxationDocumentTaxProvider` is the reference.

## Show a customer their invoices

Use `IInvoiceVisibilityService`: `CanViewAsync(invoice, userId)` and `CreateQueryAsync(userId)` (issued invoices billed to a party the user has billing access to). Ask `InvoiceLedgerAvailability.CanPayAsync(invoice)` before offering to pay: an invoice brought from another site without its transaction can't be paid there.

## Data, recipes and privacy

- **Reports:** the `Invoicing` data source (`CommerceReportDataSets.InvoicingSource`: Invoices, InvoiceLines, InvoiceTaxes, CreditNotes). Refer to an invoice from your data set with `CommerceReportDataSets.Reference(InvoicingSource.Name, InvoicingSource.Invoices)`.
- **Recipes:** `Invoices` and `CreditNotes` steps through `InvoicingDocumentImporter`; the Invoices deployment step can carry the invoices' transactions in a `Transactions` step first. Issued documents keep their numbers and never post again.
- **Personal data:** `InvoicingPersonalDataContributor` already exports and scrubs invoices billed to a person; don't add another for invoices.

## Tests

Use `tests/CrestApps.OrchardCore.Tests/Invoicing/InvoicingTestHarness.cs` (SQLite, the real stores, ledger and commands): `CreateDraftAsync`, `RunAsync(commands => ...)`, `OnLedgerAsync(ledger => ...)`, `InScopeAsync`, and `Observed` for what the observers heard. Read records back from a new scope.

---
sidebar_label: Invoicing
sidebar_position: 17
title: Invoicing
description: Invoices and credit notes posted to the Transactions ledger, with line items, tax, payment terms, numbering and printable business documents.
user_manual:
  - user-manual/commerce/invoicing
---

| Feature | Feature ID | What it adds |
| --- | --- | --- |
| **Invoicing** | `CrestApps.OrchardCore.Invoicing` | Invoices and credit notes, their stores and indexes, the invoice commands, the permissions and the invoicing settings. Depends on Transactions, Payment Terms and Business Documents. |
| **Invoicing Estimates** | `CrestApps.OrchardCore.Invoicing.Estimates` | Estimates (quotes): drafted, sent with a number, accepted or declined by the customer online, revised, expired, and turned into a draft invoice or a deposit invoice. Depends on Invoicing. |
| **Invoicing Recurring** | `CrestApps.OrchardCore.Invoicing.Recurring` | Recurring plans that make an invoice on a schedule (a draft, issued, issued and sent, or issued and charged), with the period written into their lines, catch-up within a limit, pause and resume, price rises, and an end by date or number of runs. Depends on Invoicing. |

Invoicing owns the documents: an invoice is drafted with lines, issued with a number, and posted to [Transactions](transactions) as what the customer owes. Everything about the money (payments, refunds, adjustments, reminders, pay links) stays in the ledger; the invoice mirrors what the ledger says. Prices, discounts and tax are worked out by one calculator (EN 16931 arithmetic) shared with credit notes, estimates and recurring plans.

## Projects

- **`CrestApps.OrchardCore.Invoicing.Abstractions`** holds the model (`Invoice` and `CreditNote` on the shared `InvoicingDocument` header, `DocumentLine`, `DocumentCharge`, `DocumentTax`, `DocumentTotals`, `DocumentParty`, `DocumentIssuer`, `DocumentEvent`), `InvoicingSettings`, the calculator contract (`IDocumentCalculator`, `IDocumentTaxProvider`), `IInvoiceManager` with `InvoiceQuery`, `IInvoiceCommandService` with `InvoiceCommandResult`, `IInvoiceObserver` and `InvoicingConstants`.
- **`CrestApps.OrchardCore.Invoicing.Core`** holds `DocumentCalculator`, the life cycle rules (`InvoiceLifecycle`, `CreditNoteLifecycle`, `CreditNoteRules`), the stores (`IInvoiceStore`, `ICreditNoteStore`) and their indexes (`InvoiceIndex`, `CreditNoteIndex`), `InvoiceManager`, `InvoiceHandler`, `InvoiceIssueRules`, `DefaultInvoiceCommandService`, `InvoicingPermissions` and `AddInvoicingCore()`.

## Records and indexes

Invoices and credit notes are catalog records in the `Invoicing` collection (`InvoicingConstants.CollectionName`, declared in `StoreCollectionOptions` by the feature's startup so its document table is created), saved with concurrency checks. `InvoiceIndex` holds the number, status, settlement, bill-to (kind, id, name), reference and purchase order number, currency, total, amount due, due date, issue time, created time, transaction id and source; `CreditNoteIndex` the number, invoice id, status, currency, total and issue time. `IInvoiceManager.PageAsync(page, pageSize, InvoiceQuery)` filters by status, settlement, bill-to parties, overdue date, source and a search over number, bill-to name and reference, newest first. `FindByTransactionIdAsync` and `FindByNumberAsync` find one.

Drafts are created and changed through `IInvoiceManager` (a `CatalogManager<Invoice>`). `InvoiceHandler` audits them and refuses lines out of range: a quantity of zero or less, a discount given both as a percentage and an amount, a percentage outside 0 to 100, an amount below zero or above the line, and negative charges. It refuses any change to an invoice that is no longer a draft: after issue only the commands change it.

`InvoiceCalculation` (Invoicing.Core) builds the calculator's context from an invoice and the settings (`CreateContext`) and stores the tax breakdown and totals on it (`ApplyAsync`). Issue and the draft editor both use it, so a saved draft shows the totals it will be issued with.

## Commands

`IInvoiceCommandService` changes an invoice after it is drafted. Each command takes the distributed lock `Invoicing:Invoice:{id}`, loads the invoice and its transaction fresh, decides from what the ledger holds (never from the amounts mirrored on the invoice), and is idempotent: repeating a completed command returns the invoice with `AlreadyDone`. Refusals come back in `Errors`. The service checks no permission; the caller does.

| Command | What it does |
| --- | --- |
| `IssueAsync(id)` | Fills what the draft leaves empty from the bill-to's [billing profile](parties) (contact, shipping address, tax id, exemption, copies, currency, culture, payment term), then a user's name and e-mail from the user store when no profile knows them (`ICustomerContactResolver`, so it works without the CrestApps Users module), then the party's name (`IPartyDirectory`), then the settings. It validates (someone to bill, a currency, at least one line, valid lines), checks the issue date (the one chosen on the draft, else today in the site's time zone; never after today unless `InvoicingSettings.AllowFutureIssueDate`, and with `ChronologicalIssueDates` never before the date of the last invoice issued, found by `IInvoiceStore.FindLastIssuedAsync`), calculates a final time for that date, sets the due date from the payment term and that date (`IPaymentTermCalculator`; a date typed on the draft wins; with neither it is due on issue), copies the issuer from the store profile and the default notes, terms and footer, takes the number (`IFinancialDocumentNumberGenerator`, kind `Invoice`), posts the transaction and opens the invoice, in one unit of work: a refused or failed issue takes no number and posts nothing. A command that fails, or throws, after taking a number cancels the session (`ISession.CancelAsync`), so a request that ends by saving its session doesn't keep the number: the next issue gets it. A zero total posts nothing and is paid at once. The invoice keeps a SHA-256 of what it says (`ContentHash`). |
| `VoidAsync(id, reason)` | Refused when an issued credit note credits the invoice. Otherwise cancels the transaction through `ITransactionCommandService.CancelUnpaidAsync`, which refuses under the transaction's lock when anything was paid, so a payment recorded a moment earlier is never canceled. Keeps the number. |
| `WriteOffAsync(id, reason)` | Writes off what the transaction still owes with a `WriteOff` adjustment naming the invoice; the adjustment refuses more than is owed under the transaction's lock. The invoice becomes `Uncollectible`. |
| `ReinstateAsync(id, reason)` | Takes back the write-off adjustment, so what it wrote off is owed again, and reopens the invoice. |

**The transaction an invoice posts** has `ReferenceType` `Invoice` and `ReferenceId` the invoice id, source `Invoicing`, the invoice number, the bill-to as payer (`SetPayer`) and its contact as `PayerContact`, the amounts and the tax lines (the rate as a fraction, as Taxation keeps it), the exchange rate, part payments when the invoice allows them, and is due at the end of the due date in the site's time zone. A user bill-to owns it; anyone else is a guest owner (the invoice id) reached at the bill-to's e-mail. An issue that finds a transaction already posted for the invoice reuses it.

After a command is saved, every `IInvoiceObserver` hears of it (`IssuedAsync`, `VoidedAsync`, `WrittenOffAsync`, `ReinstatedAsync`; each has a default that does nothing). An observer that throws is logged and does not undo the command.

## Admin pages

**Commerce > Invoices** (`InvoicesAdminController`, routes `Admin/invoices/{action}/{itemId?}`, named `Invoices{action}`) lists invoices through the `AdminList` shape (list name `Invoices`), in the List or Grid layout: number (or *Draft invoice*), status and payment badges, who it is billed to, the total and what is still due, and the due date, with search and the **Status** and **Payment** filters in the toolbar. **View invoices** opens it.

The draft editor (**New invoice**, **Edit draft**; **Create invoices and change drafts**) posts a plain form, mapped by `InvoiceDraftEditor`: each row keeps its line id (and what the editor does not show, such as a line's source or manual taxes), a row ticked **Remove** goes, and the blank row offered at the end is kept only when something was typed in it. **Update** saves and stays, **Save draft** saves and opens the invoice, **Save and issue** saves and runs `IssueAsync` (also needs **Issue invoices**). The bill-to is one `ItemSelector` over `Admin/api/crestapps/invoicing/parties/search` (route `CrestApps.Invoicing.PartySearch`, **Create invoices and change drafts**), which searches every party kind of `IPartyDirectory` but guests and posts the party as `Kind:Id` (`PartyRef.ToString()`); the feature depends on Resources for the selector. An issued invoice never opens in the editor.

The invoice's page is built by the display drivers of `Invoice` for `InvoicingConstants.DisplayTypes.DetailAdmin` (`Content` is the main column, `Actions` the side), so another module adds a card with a driver of its own. `InvoiceDisplayDriver` adds the summary (bill-to, dates, purchase order, reference, total, paid, credited, still due, void reason, a link to the transaction for **Manage transactions**), the lines with the tax breakdown and totals, the history (times in the site's time zone), and the actions each viewer may take: **Edit draft**, **Issue** and **Delete draft** on a draft; **Write off**, **Reinstate** and **Void** on an issued invoice, each with its reason. The same driver builds the list rows (`SummaryAdmin` and `SummaryAdminGrid`).

`InvoiceCheckoutReferenceDescriber` names the invoice a transaction was posted for (*Invoice INV-1001*, linked to its page) wherever a transaction shows its reference.

## Payments and credit notes

**An issued invoice follows its transaction.** `InvoiceLedgerMirror` (Invoicing.Core) copies onto the invoice what its transaction holds: `AmountPaid` (paid less refunded), `AmountCredited` (the totals of the issued credit notes), `AmountDue` (what the transaction still owes; none once void or canceled) and `Settlement` (`Paid` when what was paid and credited covers the total, `PartiallyPaid` when anything was, else `Unpaid`; a write-off is never counted as paid). It adds a history entry per payment, payment taken back and refund, keyed by the ledger entry's id (`InvoicingConstants.TransactionEventReferenceType`), so each is added once. `InvoiceLedgerSync` runs it under the invoice's lock, once the ledger's change is saved, and only queues the invoice's update: it never saves the session itself, which may carry the ledger command's own work. `InvoiceTransactionHandler` (an `ITransactionPaymentHandler` and `ITransactionEventHandler`, registered by `AddInvoicingCore()`) mirrors at once after a payment is recorded or taken back, which the ledger reports after its save. A status change, a cancel and a refund are reported while the ledger command still has the transaction open, so they are handed to `IInvoiceLedgerSyncScheduler`: `DeferredInvoiceLedgerSyncScheduler` runs them as `DeferredWork` after the request, in a scope of its own that saves only the invoices it changed. A request whose invoice command holds the invoice's lock (`InvoiceCommandLocks`, set by the invoice and credit note commands) skips the mirror without waiting: the command mirrors what it changes itself. Anything missed is caught up when the invoice's page opens. Payments are recorded, and customer credit applied, on the transaction's page; the invoice links to it.

**Credit notes** (`ICreditNoteCommandService`, `DefaultCreditNoteCommandService`, under the invoice's lock):

| Command | What it does |
| --- | --- |
| `CreateDraftAsync(invoiceId)` | Drafts a credit note for an issued invoice that is not void, with a line for each invoice line still creditable (`CreditNoteRules.GetCreditable`): a line credited in full is copied as it is, one partly credited already is credited by what is left as one amount. The line keeps `CreditedLineId`. The disposition starts as `ReduceBalance` while something is owed, else `CustomerCredit` when the payer can hold credit, else `Refund`. One draft at a time: a second request returns the first with `AlreadyDone`. |
| `IssueAsync(creditNoteId)` | Calculates the credit note (`InvoiceCalculation`), checks its lines and `CreditNoteRules.Validate` (never more than is creditable per line and on the invoice), then checks the disposition before anything is numbered: `ReduceBalance` needs what the transaction still owes to cover the total, `Refund` what was paid and not refunded, `CustomerCredit` a payer who can hold credit (`ICustomerCreditService.CanHold`). It then takes the number (`FinancialDocumentKind.CreditNote`) and moves the money: a `Credit` adjustment naming the credit note (`DocumentKind` `CreditNote`, its id and number), `ITransactionCommandService.RefundAsync`, or `ICustomerCreditService.AddAsync` with the reference `CreditNote:{id}`, so a retry never moves it twice. A refusal from the ledger or the credit service cancels the session, so no number is used up. The credit note is then `Issued`, and the invoice gets a `credited` history entry and its mirrored amounts. |

Draft credit notes are edited (`CreditNotesAdminController`, routes `Admin/credit-notes/{action}/{itemId?}`, permission **Issue credit notes**): the reason, how the credit is given, notes, and each line's quantity, price and discount, or leaving a line out; a credit note adds no line of its own. **Save and issue** issues it. An issued credit note is never deleted. The invoice's page lists its credit notes and offers **New credit note** while something is creditable.

## Delivery

- **The document.** `IInvoiceDocumentBuilder` (`InvoiceDocumentBuilder`, Invoicing.Core) turns an invoice or credit note into a `BusinessDocument` of the kind `Invoice` or `CreditNote` (`InvoicingConstants.BusinessDocumentKinds`, registered as *Invoices* and *Credit notes*, so each has its own document template): the issuer and bill-to as the document keeps them (addresses through `IAddressFormatter` when the Addresses feature is on), the date, due date with the term, purchase order and reference (a credit note names its invoice), a row per line (sections and text lines as such, the discount under the line), the tax breakdown (rates in percent) and totals, and for an issued invoice what was paid, credited and is due. A draft carries a *Draft* watermark and a void one *Void*. Every `IInvoiceDocumentContributor` adds to it, then the template of its kind (or the one the document names) is applied.
- **The PDF.** `IInvoicePdfService` renders it with `IBusinessDocumentPdfRenderer`. When an invoice is issued, `InvoiceArchiveObserver` (an `IInvoiceObserver`) stores its PDF in `InvoiceArchiveFileStore`, a private tenant file store under `App_Data/Sites/{tenant}/InvoiceArchives` (`AddTenantFileStore`), and records the path in `ArchiveFile`; issuing a credit note does the same. Downloads and e-mails use the archived PDF from then on, so they are what was issued whatever the template becomes; a document without one is rendered from what it keeps. A PDF is never replaced.
- **Sending.** `IInvoiceSender` (`DefaultInvoiceSender`) sends the commerce e-mail kind `invoice-issued` to the bill-to's e-mail (or the address given), copying the bill-to's copy addresses unless others are given, with the PDF attached, `{{ Document }}` as its model and the values `Number`, `Total`, `AmountDue`, `DueDate`, `PayUrl` (the transaction's pay link while something is owed), `ViewUrl` (the share link), `CustomerName` and `StoreName`. A draft is issued first when **Sending a draft issues it** is on, and refused otherwise; a void invoice is never sent. The invoice records `SentUtc`, the delivery status `Sent` and a history entry naming the recipients.
- **Share link and viewed tracking.** `IInvoiceShareLinkService` signs the invoice id with `SignedTokenService` for the days the settings say (**Share links last (days)**). The link opens `invoices/view/{token}` (route `InvoicingView`, the `Invoice` controller, no sign-in), which shows the issued invoice in the site's theme with **Pay this invoice** (the pay link), **Download PDF** (`invoices/view/{token}/pdf`) and **Print**; an expired or altered link shows that it no longer works, and a draft is never shown. The first open by someone without **View invoices** sets `ViewedUtc`, the delivery status `Viewed` and a history entry (`InvoiceViewTracker`).
- **Admin.** The invoice's page has **Send and share**: **Send** (the send dialog: to, copy, blind copy; needs **Issue invoices** too for a draft), **Print** (`Admin/invoices/Document/{id}`), **PDF** (`Admin/invoices/Pdf/{id}`) and the share link with **Copy**. A credit note's page has **Print** and **PDF**.

## Customer pages and the account page

- **Who sees an invoice.** `IInvoiceVisibilityService` (`DefaultInvoiceVisibilityService`) lets a signed-in user see the issued invoices billed to a party they have billing access to (`IPartyMembershipService` with `PartyAccess.Billing`): themselves, their customer record, or an account they are an admin of; a plain member of an account doesn't see its invoices, and a draft is never shown. `CreateQueryAsync` returns an `InvoiceQuery` with `IssuedOnly` and `OnlyBillTo`, so a user with no party sees nothing rather than every invoice.
- **My invoices** (`my-invoices`, route `InvoicingMyInvoices`, permission **View own invoices**, in the customer's account menu and the `CommerceAccountPage` layout): the customer's invoices, newest first, 25 a page, with the date, due date, total, what is still due, status and PDF; each opens on `my-invoices/{id}` with **Pay this invoice**, **Download PDF** and **Print**. Ticking several and **Pay selected** starts one checkout over their transactions (`StartCheckoutRequest.References`); one invoice starts the single checkout. Only invoices the customer may see and that are still owed are paid. Paying several needs them owed by the same payer and in one currency: the ledger now groups several transactions by the payer they name (an account's invoices placed by different buyers pay together) and by the owner otherwise.
- **The account page.** With Accounts enabled, Invoicing's `AccountsStartup` registers the account page's **Invoices** section (`AccountPageTabs.Invoices`) and `AccountInvoicesDisplayDriver` fills it: what the account owes and is overdue on per currency (from its open invoices, up to 500), its 20 newest invoices, and **New invoice**, which opens the editor billed to the account (`Admin/invoices/Create?billTo=Account:{id}`). Accounts no longer registers the section, and Accounts B2B no longer lists its terms transactions there.

## Work orders

With Work Orders enabled, Invoicing's `WorkOrdersStartup` registers `InvoicingWorkOrderBilling` (`IWorkOrderBilling`, `Priority` 10, so it bills instead of the plain debt Transactions posts). It turns the `WorkOrderBillingRequest` into an invoice: parts as `Item` lines (with the product), labor and other lines as `Service` lines, each with the line's or the request's tax category; billed to the request's payer with the work order's contact; `SourceType` `WorkOrder`, `SourceId` the work order and `Reference` its number; the request's payment term. It issues the invoice at once, which posts what is owed; an invoice that can't be issued stays a draft for someone to finish. Billing a work order again returns its invoice.

## Charging a saved card

An invoice with `AutoCharge` (the editor's **Charge the customer's saved card**, or a recurring plan in `IssueAndCharge` mode) is charged by `InvoiceAutoCharge` (Invoicing.Core, an `IInvoiceObserver` and an `ITransactionEventHandler`) when it is issued: it schedules the charge of its transaction to the payer's default saved card through `ISavedPaymentMethodChargeService` ([Transactions](transactions#charging-on-a-schedule-with-retries)) on the morning of the due day in the site's time zone, or charges it at once when it is due on issue. A declined card is retried on the ledger's retry schedule. When the payer kept no card that can be charged (a guest payer never has one), or the retries run out (`AutoChargeAbandonedAsync`), the invoice is sent to the payer with `IInvoiceSender` instead.

## Estimates

With **Invoicing Estimates** enabled, `Estimate` (Invoicing.Abstractions) is a catalog record with the invoice's header and lines, `EstimateIndex`, and the commands of `IEstimateCommandService` (`DefaultEstimateCommandService`, each under the estimate's lock, idempotent, refusing with reasons):

- **Send** numbers a draft from the `Invoicing.Estimate` number series (`EST-{seq}` from 1001, passing over held numbers), works out its totals, sets its date and, when it has none, an expiry `EstimateValidDays` days later (**Settings > Commerce > Invoicing**, 30 by default), and locks it: `EstimateHandler` refuses any change to a sent estimate.
- **Accept** records the customer's typed name, the time, the address and the terms they were shown (`EstimateAcceptance`) while it is valid on the site's date, and invoices what the estimate's `OnAcceptance` says: nothing, the whole of it, or its deposit. **Decline** records an optional reason.
- **Revise** starts a draft copy numbered after it when sent (`EST-1001-2`); the estimate it revises is canceled and points at it. **Cancel** withdraws one that was not invoiced.
- **Convert** makes a draft invoice: `Full` copies the lines (`SourceType` `Estimate`, `Reference` the estimate's number) and marks it `Invoiced`; `Deposit` makes a `Prepayment` invoice with one `Deposit` line of the deposit percent or amount. Converting again returns the invoice made the first time.
- **Expire**: `EstimateExpiryBackgroundTask` (hourly) expires the sent estimates whose expiry date has passed in the site's time zone.

`IEstimateObserver` hears sent, accepted, declined, expired and converted. The admin pages are **Commerce > Estimates** (an `AdminList`, the draft editor with the invoice's line rows, and the estimate's page with the document, its history, the actions and the customer's link). The customer's page is `estimates/view/{token}` (`EstimateShareLinkService`, signed), where they read it, download its PDF, and accept or decline it while it is valid. `EstimateSender` e-mails it (the `estimate-sent` kind, with the PDF and the link); without e-mail, **Mark sent** numbers it so its link can be shared. The PDF and pages render through `IInvoiceDocumentBuilder` (business document kind `Estimate`, with **Valid until**, the deposit asked and the acceptance). Workflows get **Estimate Sent**, **Accepted**, **Declined** and **Expired**; webhooks get `estimate.sent`, `estimate.accepted`, `estimate.declined`, `estimate.expired` and `estimate.converted`; recipes and deployment plans the `Estimates` step; the Invoicing data source the **Estimates** data set. Permission: **Manage estimates** (create, send, revise, cancel, record an answer, invoice; making the invoice also needs **Create invoices and change drafts**).

## Recurring plans

With **Invoicing Recurring** enabled, `RecurringPlan` (Invoicing.Abstractions) is a catalog record with the invoice's header and lines and a schedule: a recurrence rule (RFC 5545, expanded by the [Calendars](calendars) recurrence engine), a start date, an optional end date and number of runs, what each run does (`Mode`: `Draft`, `Issue`, `IssueAndSend` or `IssueAndCharge`), and an optional price rise (a percent every so many runs). `RecurringPlanIndex` keeps its status and next run; `RecurringPlanHandler` refuses a plan with no name, a rule that does not parse, an end before its start, or lines out of range.

`IRecurringPlanService` (`DefaultRecurringPlanService`, each command under the plan's lock):

- **Activate** starts a draft plan on its first occurrence; **Pause** stops an active one; **Resume** carries on from today, skipping what was missed while paused; **Cancel** stops it for good.
- **Run** makes the invoice of each occurrence due by the site's date, up to 12 per plan at a time, so a plan started in the past catches up a year at most per sweep. Each invoice has `SourceType` `RecurringPlan` and `SourceId` `{planId}:{yyyy-MM-dd}`: an occurrence that already has one is recorded as skipped, never invoiced twice. The invoice copies the plan's bill-to, lines and terms, bills the period from the occurrence to the day before the next one (`ServiceStart`, `ServiceEnd`, and on each line), and writes the period into the line names and descriptions and the notes: `{{ period.start }}`, `{{ period.end }}`, `{{ period.month }}`, `{{ period.year }}`, or `{{ period.start | date: "MMMM yyyy" }}` with a .NET date format.
- The draft is saved first; issuing and sending run in a unit of work of their own, so a run that can't issue keeps its draft and is recorded as **Failed** with the reason, and the plan moves to its next occurrence. `IssueAndCharge` marks the invoice `AutoCharge` for the saved-method charge.
- The plan completes after its last run (end date or number of runs). `GetUpcoming` lists the next occurrences.

`RecurringInvoiceBackgroundTask` runs the due plans every 30 minutes on the site's date. `IRecurringPlanObserver` hears an invoice generated and a run failed. The admin pages are **Commerce > Recurring invoices**: an `AdminList` with a status filter, the editor (the schedule, the bill-to and the invoice's line rows), and the plan's page with its next runs, the runs it made with links to their invoices, and **Start**, **Run what is due now**, **Pause**, **Resume**, **Cancel** and **Delete** (a draft only). A change to a running plan applies to the runs after it. Workflows get **Recurring Invoice Generated** and **Recurring Run Failed**; webhooks get `recurring-plan.invoice-generated` and `recurring-plan.run-failed`; recipes and deployment plans the `RecurringPlans` step; the Invoicing data source the **Recurring plans** data set. Permission: **Manage recurring invoices** (security critical, since its plans issue and charge on their own).

## Workflows and webhooks

`IInvoiceObserver` hears what happens to an invoice once it is saved: a draft created (`InvoiceHandler`), issued, voided, written off and reinstated (the commands), sent (`IInvoiceSender`), opened by the customer for the first time (`InvoiceViewTracker`), a payment reaching it and its being paid in full (`InvoiceLedgerSync`, when the mirror raises `AmountPaid` or settles it), overdue (when the ledger finds its transaction overdue and the invoice is still open) and credited (`ICreditNoteCommandService`). Every member has a default that does nothing. `InvoiceObserverNotifier` calls them, logging an observer that fails; a change queued in the session (created, payment, paid, overdue) is told after the request saves it, through `DeferredWork` (`invoicing_observers`), with the invoice read again. A credit is not a payment: crediting an invoice in full raises Credit Note Issued, not Paid.

With **Workflows** enabled, `InvoiceWorkflowObserver` starts the events **Invoice Created**, **Invoice Issued**, **Invoice Sent**, **Invoice Viewed**, **Invoice Payment Received**, **Invoice Paid**, **Invoice Overdue**, **Invoice Voided** and **Credit Note Issued** (category **Invoicing**), after the request's work is saved and correlated by the invoice's id. The input holds the invoice: `InvoiceId`, `Number`, `Kind`, `Status`, `Settlement`, `Delivery`, `Currency`, `Total`, `TaxTotal`, `AmountPaid`, `AmountCredited`, `AmountDue`, `BillToKind`, `BillToId`, `BillToName`, `BillToEmail`, `IssueDate` and `DueDate` (ISO dates), `TransactionId`, `SourceType`, `SourceId` and `OwnerId`; a payment adds `Amount`, a void `Reason`, a credit `CreditNoteId`, `CreditNoteNumber`, `CreditNoteTotal`, `Disposition` and `Reason`.

The tasks run the commands staff run. Their fields are Liquid, amounts are written with a dot, and each writes the invoice and the command's `Errors` to the workflow output under **Output name** (`Invoice` when empty):

| Task | Fields | Outcomes |
| --- | --- | --- |
| **Create Invoice** | Bill to (`Kind:Id`), Currency, Description, Amount, Reference | Done, Failed |
| **Issue Invoice** | Invoice id | Done, NotFound, Failed |
| **Send Invoice** | Invoice id, To | Done, NotFound, Failed |
| **Record Invoice Payment** | Invoice id, Amount (what is due when empty), Payment method, Reference, Note | Done, NotFound, Failed |
| **Void Invoice** | Invoice id, Reason | Done, NotFound, Failed |
| **Create Credit Note** | Invoice id, Reason, Disposition (`ReduceBalance`, `Refund` or `CustomerCredit`) | Done, NotFound, Failed |

Create Invoice drafts one line; Issue Invoice issues it. Create Credit Note credits everything still creditable and issues the credit note. With CrestApps Recipes, each activity has its schema for the `WorkflowType` step.

With **Commerce Webhooks** enabled, `InvoiceWebhookObserver` publishes `invoice.created`, `invoice.issued`, `invoice.sent`, `invoice.viewed`, `invoice.payment-received`, `invoice.paid`, `invoice.overdue`, `invoice.voided` and `credit-note.issued` (`InvoicingConstants.WebhookEvents`), with the invoice in `data.invoice` and the event's own values (the payment's `amount`, the void's `reason`, the `creditNote`) in `data.details`. Each event id is stable (such as `evt_invoice-issued_{invoiceId}`), so a receiver drops a repeat.

## Recipes and deployment

With **Deployment** enabled, the **Invoices** and **Credit notes** deployment steps export every invoice and credit note, drafts and issued, with everything they hold (number, lines, totals, history, when they were created and by whom) but the storing site's document id. The `Invoices` and `CreditNotes` recipe steps import them through `InvoicingDocumentImporter`:

- an issued document keeps its number and never posts to the ledger: its transaction travels with the ledger's own step;
- an issued document the site already has is kept as it is, since after issue only the document commands change it;
- a draft the site has is replaced, in the same stored document;
- a document whose number another document of the site holds is refused, as is an issued one without a number, and the step reports a recipe error.

The **Invoices** step's **Also export the transactions the invoices posted** option (`IncludeTransactions`, on by default) writes a Transactions step (see [Transactions](transactions#moving-the-ledger-between-sites)) with those transactions, their payment attempts and refunds, before the invoices, so an open invoice can be paid where it is imported. The two steps import in either order: neither needs the other to be there first.

An open invoice whose transaction is not on the site (`InvoiceLedgerAvailability`) can't be paid, reminded or mirrored there. Its admin page says so, with the transaction's id, and offers no void, write-off or credit note, which change the transaction; My invoices, its page and its share link show no way to pay, and paying several leaves it out; the e-mail has no pay link. It is logged once per transaction while the site runs, and nothing throws. Importing the transaction makes it payable.

Issuing passes over a number an imported document already holds, so the site's series never gives it twice (`UnusedDocumentNumbers`; the issue fails after 1,000 held numbers in a row). Import the invoices before their credit notes. Payment terms, document templates, number series settings and invoicing settings have their own steps (Payment Terms, Business Document Templates, and the Settings step with the `NumberSeriesSettings` and `InvoicingSettings` schemas). With CrestApps Recipes, each step has its schema.

## Reports and the data source

With **Data Pipelines Core** enabled (Report Builder, data pipelines), the **Invoicing** data source (`CommerceReportDataSets.InvoicingSource`) has four data sets, each needing **View invoices**:

| Data set | One row per |
| --- | --- |
| **Invoices** | Invoice: number, kind, status, payment status, delivery, reference, purchase order, term, totals, paid, credited and still due, bill-to, issue and due dates, and when it was created, issued, sent, viewed and voided. `TransactionId` refers to the Transactions data set. |
| **Invoice lines** | Line of an issued invoice, with the invoice's number, status, currency, issue date and bill-to. |
| **Invoice taxes** | Tax of an issued invoice at one rate: amount taxed and tax. |
| **Credit notes** | Credit note: number, the invoice it credits, how it was given back, totals and the amounts applied, refunded and kept as credit. |

With **Reports** enabled, six coded reports under **Reports > Commerce**, each needing **View invoices** and summing per currency:

| Report | Shows |
| --- | --- |
| **Receivables aging** | What open invoices still owe today (in the site's time zone), per customer: not due yet, 1 to 30, 31 to 60, 61 to 90 and over 90 days past the due date (the issue date when there is none). |
| **Invoice register** | Every invoice issued in the period, by issue date and number, with its status, tax, total, paid, credited and still due. |
| **Payments collected** | The payments recorded in the period on the transactions invoices posted, by method, and each payment and refund; a payment taken back doesn't count. |
| **Invoiced sales by customer** | Invoiced before and after tax per customer, less the credit notes issued in the period. |
| **Invoiced sales by item** | Quantity and amount before tax per product, SKU or line name, after discounts and less credit note lines. |
| **Invoiced tax summary** | Amount taxed and tax per tax and rate, less what credit notes gave back; untaxed lines appear at 0%. |

The period reports default to the last 30 days. Voided invoices count for nothing in the sales and tax reports. The documents are read 500 at a time through the index columns (`IssuedUtc`, `Status` and `AmountDue`, and the transactions' `ReferenceType` and `UpdatedUtc`). The name of an offline payment method comes from `OfflinePaymentMethodText` (Transactions.Core), shared with the Transactions pages.

## Personal data

`InvoicingPersonalDataContributor` (an `ICommercePersonalDataContributor`) takes part in privacy requests, such as erasing or exporting a customer under **Commerce > Customers**. A person's documents are the invoices billed to them as a user, to their customer records (`IPartyMembershipService`) or to them as a guest, and those billed to one of their e-mails (the `BillToEmail` index column, lower case) without a business account; invoices billed to a business account are the business's records and are left alone. Each invoice's credit notes come with it.

- **Export** adds an `Invoice` or `CreditNote` record per document: number, currency, total, issue date, bill-to name, e-mail, phone, address and tax id, and the status, paid and due amounts and due date of an invoice.
- **Erasure** clears the bill-to's contact (name, e-mail, phone, address), shipping address and copy addresses, and scrubs the person from the notes, reference and history. It keeps numbers, amounts, dates, tax ids and the archived PDFs of issued documents, which the business must keep, and adds a warning to the result that says so. The invoice's page then shows **No name kept** for the bill-to.

The contributor does not take part in the guest retention expiry. Invoices saved before the `BillToEmail` column was added are found by e-mail once they are saved again.

## Tax

The calculator asks the `IDocumentTaxProvider`s in order for the lines' taxes; without one, the taxes typed on the lines are used. With **Taxation** enabled, a `[RequireFeatures]` startup registers `TaxationDocumentTaxProvider`: it builds a `TaxCalculationContext` from the lines (the line discount as an amount, services as `Service` items), the bill-to's party tax profile (`IPartyTaxProfileService`) and its shipping address, and maps the tax lines back, the rate in percent. Charges keep the taxes typed on them. Invoicing resolves nothing of Taxation's when it is off.

## Settings

`InvoicingSettings` (**Settings > Commerce > Invoicing**, group `invoicing`, permission **Manage invoicing settings**): the default payment term (none means due on issue), the default currency, whether prices include tax, whether customers may pay part of an invoice, whether sending a draft issues it, whether an issue date after today is allowed (`AllowFutureIssueDate`, off), whether invoices must be dated in the order they are numbered (`ChronologicalIssueDates`, off), cash rounding, how long share links last, and the default notes, terms and footer. With CrestApps Recipes, `InvoicingSettingsSchema` describes them for the Settings step.

## Permissions

| Permission | Lets a user |
| --- | --- |
| **View invoices** | See every invoice and credit note. Implied by every permission below that changes them. |
| **Create invoices and change drafts** | Create invoices and change and delete drafts. |
| **Issue invoices** | Issue drafts (security critical). |
| **Void and write off invoices** | Void, write off and reinstate (security critical). |
| **Issue credit notes** | Issue credit notes (security critical). |
| **Record invoice payments** | Record payments received outside the site (security critical). |
| **Manage invoicing settings** | Change the settings. |
| **Manage estimates** | Create, send, revise and cancel estimates, record the customer's answer, and invoice them (with Invoicing Estimates). |
| **Manage recurring invoices** | Create, start, pause, resume, cancel and run recurring plans (security critical; with Invoicing Recurring). |
| **View own invoices** | See the invoices billed to them and to the parties they have billing access to. |

Administrators and store managers get every permission; authenticated users get **View own invoices**. None of the changing permissions implies another.

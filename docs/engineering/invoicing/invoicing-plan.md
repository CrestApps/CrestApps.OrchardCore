# Invoicing and billing plan

Status: design finalized 2026-11-13, nothing built yet. Progress is tracked in the **Progress** section at the end.

The goal is an invoicing and billing module that covers what a business expects from a current invoicing product, for any business type, size, industry and billing model: invoices, estimates, credit notes, recurring invoices, payment terms, partial payments, deposits, installments, late fees, reminders and collection, statements, a customer portal, branded PDF and e-mail delivery, multiple currencies, and time, milestone and usage billing.

It is built as building blocks. Invoicing owns the **documents** (invoice, estimate, credit note, recurring plan). Everything else is reused from the module that already owns it, and where a concept is missing or exists in a second copy, it is added to its owner or extracted into a shared building block in this plan: the ledger of money owed (Transactions), payments (Checkout and the payment providers), taxes (Taxation), numbering, document rendering and PDF, e-mail, parties and accounts.

The plan uses generic industry terms only (AGENTS.md forbids product names in code and public docs).

## Contents

1. Industry baseline
2. Decisions
3. What exists and how it is reused
4. Duplicates this plan removes
5. Module map
6. Shared building blocks added or changed
7. Transactions: the complete ledger
8. Invoicing domain model
9. Life cycles and invariants
10. Totals, tax and rounding
11. Payments, credit and refunds
12. Delivery: templates, PDF, e-mail, links
13. Customer portal and the account page
14. Estimates
15. Recurring invoices
16. Terms, reminders, late fees and collection
17. Deposits and installments
18. Billing sources: time, milestones, usage, subscriptions, work orders, orders
19. Multiple currencies
20. Cross-cutting: workflows, recipes and deployment, data sources and reports, API, webhooks, AI tools, permissions
21. Changes to existing modules
22. Non-functional requirements
23. Phases and delivery
24. Testing strategy
25. Out of scope
26. Risks
27. Glossary
28. Progress

---

## 1. Industry baseline

This is the capability map of current invoicing and billing products, measured across seven widely used ones plus two subscription-billing references and the European e-invoicing standards. "Tier" says how common a capability is: **T** table stakes (all), **C** common (most), **D** differentiator (one or two).

| Area | Capability | Tier | Where this plan covers it |
|---|---|---|---|
| Documents | Draft that does not touch the ledger; issue; void that keeps the number | T | §9 |
| Documents | Issued invoices locked; corrections by credit note or by a revision that voids and replaces | C | §9 |
| Documents | Estimates: send, accept or decline, expire, convert to invoice | T | §14 |
| Documents | Credit notes tied to invoice lines; refund, apply, or keep as customer credit | C | §11 |
| Documents | Statements; write-off (uncollectible); approval before issue | C/D | §13, §11, §9 |
| Data | Line items with quantity, unit, price, per-line discount, several taxes, service period | C | §8 |
| Data | Document-level discount, shipping, surcharge; custom fields; attachments; notes, terms, footer | C | §8 |
| Data | Customer snapshot frozen at issue; several billing contacts; tax ID and treatment | C/D | §8 |
| Tax | Exclusive and inclusive prices, compound taxes, exemptions, reverse charge, automatic tax | C | §10 |
| Recurring | Schedules with draft, issue, send or charge modes; end by date or count; pause | T | §15 |
| Recurring | Proration, trials, per-seat, usage and thresholds | D | §18 |
| Billing | Time and expenses to invoice, with markup; progress invoicing from an estimate; retainers | C/D | §18 |
| Payments | Pay link and hosted pay page; saved methods and autopay; offline payments | T | §11 |
| Payments | Partial payments; one payment across several invoices; overpayment to credit; refunds | C/T | §11 |
| Payments | Deposits and an installment schedule on one invoice | C/D | §17 |
| Collection | Terms: net days, due on receipt, end of month; early-payment discount | T/D | §16 |
| Collection | Reminders before, on and after the due date; late fees; retry schedules for failed charges | T/C/D | §16 |
| Delivery | Branded templates, PDF, e-mail with placeholders and attachment, share link, viewed tracking | T/C | §12 |
| Delivery | Numbering: prefix, padding, yearly reset, series per type; number assigned at issue | C/D | §6.1 |
| Delivery | Documents in the customer's language; customer portal | C | §12, §13 |
| Automation | API, webhooks, workflows, roles, audit history, import and export, reports | T/C | §20 |
| Compliance | Sequential unique numbers per series, immutable issued documents, credit notes that cite the original, tax breakdown per rate, structured e-invoices | — | §9, §10, §25 |

Lessons taken from the products' own documentation and user reports:

- Never change an issued invoice. Lock amounts and party data at issue.
- Assign the number at issue, not at draft, or deleted drafts leave gaps.
- Keep a payment separate from where it is applied, and release (never delete) payments when an invoice is voided.
- Do not overload one status. Life cycle, settlement and delivery are separate; overdue is derived.
- Make every money handler idempotent and independent of event order.
- Store the rounding policy on the document. Compute tax per rate on the summed net.
- Due dates are local dates in the issuer's time zone.
- Re-check the balance right before a reminder or a late fee, and give late fees a grace period and a cap.
- A deposit is not revenue until the work is invoiced.

## 2. Decisions

ID1 to ID5 were made with the product owner on 2026-11-13. ID6 onward follow from them.

| # | Decision | Why |
|---|---|---|
| ID1 | **Invoice on the ledger.** An invoice is a new document record with lines, taxes and a life cycle. Issuing it posts one `Transaction`. Payments, pay links, refunds, installments and reminders stay in Transactions and Checkout. | `Transaction` is a single-amount ledger entry that eight modules post to and that Checkout settles as one billing item. Keeping it as the balance owner reuses all the payment plumbing and changes no consumer. |
| ID2 | **Invoicing becomes the one owner of invoices and estimates.** The Transactions invoice page, the B2B account "Invoices" list and Work Orders billing move onto it. Orders keeps working alone; when Invoicing is on, a sent draft order or quote creates a real invoice or estimate through an optional integration. | One concept, one owner. Orders must still work on a site without Invoicing. |
| ID3 | **E-invoicing: model-ready now, file output later.** Lines, tax categories, the tax breakdown per rate, rounding and credit-note references follow EN 16931 from day one. Generating UBL or hybrid PDF files and network delivery is a later phase behind a provider seam. | The mandates arrive in stages from 2026 to 2030. A model that fits the standard costs little now; retrofitting it costs a migration. |
| ID4 | **Three pull requests, one commit per slice.** PR A: shared building blocks, the ledger changes, invoices, payments, credit notes, PDF, e-mail, portal, account section. PR B: estimates, recurring, terms, reminders, late fees, deposits, installments, statements. PR C: time, milestone and usage billing, exchange rates, approvals. | The scope is too large for one review. Each PR ships something usable. |
| ID5 | **Transactions is the complete ledger.** Anything about money owed, paid, credited or refunded belongs to it, and it is brought up to that standard in this plan (§7). | Product owner, 2026-11-13: "Transaction module should be able to handle any transactions related." |
| ID6 | **Documents are indexed YesSql records, not content items.** | Issued documents must be immutable, numbered in sequence and never pruned or unpublished. Content versioning, deletion and pruning work against that. Each integration a content item would give for free (workflow events, data source, deployment, API) is added explicitly. |
| ID7 | **Three status dimensions.** Life cycle (`Draft`, `PendingApproval`, `Open`, `Void`, `Uncollectible`), settlement (`Unpaid`, `PartiallyPaid`, `Paid`, mirrored from the transaction) and delivery (`NotSent`, `Sent`, `Viewed`). Overdue is derived from the due date and is never stored. | One overloaded status field is the most reported modeling mistake in the products studied. |
| ID8 | **The number is assigned at issue**, inside the same unit of work as the status change, from a locked series. A draft has no number and can be deleted. An issued document can only be voided. | Gap-free, unique numbering per series is a legal requirement in many places. |
| ID9 | **Issued documents are immutable.** After issue only internal notes, custom tags, delivery data and the payment schedule may change. A correction is a credit note, or a revision that issues a replacement and voids the original in one step. | Tax compliance, and the audit trail. |
| ID10 | **One calculator, EN 16931 arithmetic.** Line net is rounded to the currency's precision; tax is computed per (category, rate) on the summed net and spread back over the lines; payable rounding is an explicit amount. Tax comes from Taxation when it is on, and from tax lines typed on the document when it is not. | One place computes totals for invoices, estimates, credit notes and recurring plans. Taxation stays the owner of tax rules. |
| ID11 | **One renderer for business documents.** A new Documents building block owns the printable document model, templates, HTML and PDF. Receipts, transaction payment requests, invoices, estimates and credit notes all render through it. The PDF primitives are extracted from the Reports PDF add-on into a shared library. | Invoice PDFs are the second PDF consumer and the third document renderer; the second copy is the trigger to extract. |
| ID12 | **One numbering owner.** `INumberSequence` gains series formats (prefix, padding, date tokens, yearly reset). The financial document number generator becomes an adapter over it and keeps its issued numbers. | Two locked sequence stores exist today. |
| ID13 | **The bill-to is a party.** A document stores a `PartyRef` (account, customer, user or contact) and a frozen copy of the party's billing profile. A new party billing profile seam supplies contact, addresses, tax ID, currency, language and terms. | Transactions, Work Orders and B2B each encode "who owes" differently today. |
| ID14 | **Payment terms are a ledger concept** and become a small catalog in Transactions (net days, due on receipt, end of month, day of next month, early-payment discount). Invoicing, B2B, Pay Later and Work Orders read it. | Terms exist today as five scattered day counts. |
| ID15 | **Recurring plans use the Calendars recurrence rule.** | The catalog names `RecurrenceRule` as the one model for repeats; four other cadence models already exist. |
| ID16 | **A late fee is its own document** (a debit note linked to the original invoice), never a line added to an issued invoice. | ID9. |
| ID17 | **Customer credit is the existing store credit balance.** Overpayments and credit notes on paid invoices post to it, and it can be applied to an open invoice. | A balance ledger per customer already exists in Customers. |
| ID18 | **Staff discounts are stored on the document** using the Checkout discount line shape. Coupons keep working on the pay link through Checkout. | Coupons need a checkout; a discount a person types on a line does not. |

## 3. What exists and how it is reused

| Existing piece | Path or contract | Reused for |
|---|---|---|
| Transactions | `Transaction`, `ITransactionManager`, `ITransactionCommandService` (offline payment, mark paid, cancel, adjust, refund, charge saved method), `ITransactionSettlementService`, `ITransactionEventHandler`, `ITransactionPaymentHandler`, `IPaymentRefundHandler` | The receivable behind every issued invoice; every payment, adjustment and refund |
| Pay links | `ITransactionPayLinkService` (signed token), `TransactionSettlementCheckoutHandler` | "Pay this invoice" without signing in |
| Checkout and providers | `ICheckoutEngine`, `ICheckoutPaymentProvider`, `ICheckoutRefundService`, `ISavedPaymentMethodStore`, off-session charge, `IPaymentEvent`, the provider webhook inbox | Online payment, saved methods, autopay, refunds, provider webhooks |
| Payment records | `PaymentAttempt`, `PaymentRefund`, `PaymentIssue`, `IPaymentIssueRecorder` | The payment ledger and the "money failures someone must handle" list |
| Reminders | `ITransactionReminderService`, `TransactionReminderBackgroundTask`, `BecameOverdueAsync` | Reminder sweep and the overdue signal |
| Installment plans | `IInstallmentPlanService.CreateFromTransactionAsync` | Installment schedules on an invoice |
| Store credit | Customers store credit accounts | Customer credit balance (ID17) |
| Taxation | `ITaxService.CalculateAsync` on any lines and addresses, `TaxLine`, `TaxSnapshot`, `ITaxRefundCalculator`, `CustomerTaxProfile` | Automatic tax, tax breakdown, credit-note tax |
| Money | `CurrencyScale`, `Money`, `BalanceLedger` | Precision and rounding |
| Numbering | `INumberSequence` (locked, gap-free) | Document numbers |
| Parties | `PartyRef`, `IPartySource`, `IPartyDirectory` | The bill-to picker and names |
| Accounts | `AccountPart`, page sections (`AddPageSection`), `IAccountMemberService` | Invoices on the account page; account members in the portal |
| Addresses | `Address`, `ContactInfo`, `IAddressFormatter` | Bill-to and ship-to snapshots |
| Store profile | `StoreProfileSettings`, `IStoreLocationService` | The issuer block |
| Products | `ISellableProduct`, `IPriceResolver`, `IBuyerPriceProvider` | Item picker and prices on lines |
| Commerce e-mail | `ICommerceEmailSender`, `AddCommerceEmailKind`, Liquid template per kind | Invoice, estimate, credit note and reminder e-mails |
| Calendars | `RecurrenceRule`, `RecurrenceExpander`, `CalendarTime` | Recurring schedules, local due dates |
| Reports | `IndexedReportDataSet`, `AddReport`, export formats | Data sets and the receivables reports |
| Reports PDF add-on | PdfPig-based layout primitives (internal today) | The shared PDF library (ID11) |
| Receipts | `ReceiptDocument`, `IReceiptService`, HTML renderer | Becomes a consumer of the Documents building block |
| Commerce API and webhooks | `MapCommerceApi`, idempotency filter, `AddWebhookEvent`, `IWebhookPublisher` | Invoicing API and outbound events |
| Platform | Catalogs, `CatalogAdminController`, recipe and deployment bases, `TransitionTable`, `SignedTokenService`, `SiteUrlGenerator`, tenant file storage, leased work queue, retention, telemetry | Storage, admin, portability, links, stored PDFs, background work |
| Orchard Core | Workflows, Data Pipelines data sources, Recipes, Deployment, Email (attachments), Notifications, Liquid, Background tasks, Audit Trail, OpenApi | As named |

**Gaps this plan fills:** stored documents with lines; draft, issue and void; estimates; credit notes as documents; document templates and PDF; payment terms; partial payment through the link; payer as a party; tax breakdown and exchange rate on the ledger; late fees; staged reminders; recurring plans defined by staff; billing from time, milestones and usage; an account-level portal; recipe, deployment, workflow and webhook coverage for the ledger.

## 4. Duplicates this plan removes

| Concept | Copies today | After this plan |
|---|---|---|
| Invoice | Transactions invoice page; Orders `OrderInvoice`; B2B list of account-terms transactions; Work Orders single-amount billing | Invoicing documents. Transactions keeps a payment request for sites without Invoicing, rendered by the same Documents block |
| Quote | Orders `OrderQuote` | Invoicing estimates; Orders bridges to them when Invoicing is on |
| Number generators | `DocumentNumberSequence`; `SequentialFinancialDocumentNumberGenerator` | `INumberSequence` only |
| Document rendering | Receipt view and HTML renderer; transaction invoice builder; Reports PDF writer | Documents building block and the shared PDF library |
| Money formatting | Eight private formatters | `IMoneyFormatter` |
| Payment terms | B2B net days; Pay Later and bank transfer net days; Work Orders hard-coded 30 days; order invoice due date | Payment terms catalog |
| Who owes | `OwnerId` holding a user, a guest id, or an account id; B2B parent reference | `Transaction.Payer` |
| Charge a saved method and retry | Bookings, installment plans, Transactions | One saved-method charge service (PR B) |
| Reminder schedules | Transactions reminders; installment notices; subscription grace days | Staged reminder policy in Transactions (PR B) |

The other cadence models (`Subscription`, `TransactionRecurrence`, checkout `RecurringPlan`, installment frequency) are not migrated here; recurring invoices use `RecurrenceRule` and add no new one.

## 5. Module map

Every module follows the Abstractions/Core/module split. Optional integrations are `[RequireFeatures]` startups in the consumer.

| Project(s) | Feature id(s) | Depends on | Holds |
|---|---|---|---|
| `CrestApps.OrchardCore.Pdf.Core` (library) | — | — | PdfPig layout primitives: page flow, fonts, text measuring and wrapping, tables, colors. Extracted from the Reports PDF add-on |
| `CrestApps.OrchardCore.Documents` (+ `.Abstractions`, `.Core`) | `CrestApps.OrchardCore.Documents` | — | Printable business document model, document templates (catalog), HTML renderer, themeable shape |
| | `CrestApps.OrchardCore.Documents.Pdf` | Documents | PDF renderer on `Pdf.Core` |
| `CrestApps.OrchardCore.Transactions` (existing) | existing features | existing | Ledger changes in §7 |
| | `CrestApps.OrchardCore.Transactions.PaymentTerms` | Transactions | Payment terms catalog |
| `CrestApps.OrchardCore.Invoicing` (+ `.Abstractions`, `.Core`) | `CrestApps.OrchardCore.Invoicing` | Transactions, Documents, Payment terms | Invoices, credit notes, settings, calculator, issue, send, payment actions, portal pages, account section |
| | `CrestApps.OrchardCore.Invoicing.Estimates` (PR B) | Invoicing | Estimates and conversion |
| | `CrestApps.OrchardCore.Invoicing.Recurring` (PR B) | Invoicing | Recurring plans and the generation task |
| | `CrestApps.OrchardCore.Invoicing.Collections` (PR B) | Invoicing | Late-fee policies, debit notes, statements |
| | `CrestApps.OrchardCore.Invoicing.Billing` (PR C) | Invoicing | Billable sources, progress invoicing, usage meters, approvals |
| | `CrestApps.OrchardCore.Invoicing.Api` (PR B) | Invoicing | `/api/commerce/v1/invoicing` |
| `CrestApps.OrchardCore.ExchangeRates` (+ `.Abstractions`, `.Core`) (PR C) | `CrestApps.OrchardCore.ExchangeRates` | — | Dated exchange rates, manual entry, provider seam |

Dependency rules:

- Transactions, Documents, Taxation, Accounts, Orders and Work Orders never reference Invoicing. Invoicing references their Abstractions.
- Integrations with Accounts, Orders, Work Orders, Subscriptions, Taxation, Products, Workflows, Data Pipelines, Reports, Recipes, Deployment, Webhooks and AI are `[RequireFeatures]` startups inside Invoicing.
- Documents names no consumer: document kinds are open strings that consumers register.

## 6. Shared building blocks added or changed

### 6.1 Numbering (`CrestApps.OrchardCore.Abstractions`, `YesSql.Core`)

- `NumberSeriesFormat`: a pattern with the tokens `{seq}`, `{seq:0000}`, `{yyyy}`, `{yy}`, `{MM}`, and a reset policy (`Never`, `Yearly`, `Monthly`). The date tokens use the issuer's local date.
- `services.AddNumberSeries(name, title, defaultFormat)` registers a series so a settings page can list it. `NumberSeriesSettings` (site settings) stores the format and the first number for each registered series. The settings page lives in the Documents module, under Settings.
- `INumberSequence.NextAsync` keeps its contract: the series key is the name plus the reset period, so a yearly series restarts without a second store.
- `SequentialFinancialDocumentNumberGenerator` becomes an adapter: it calls `INumberSequence` and seeds each series from the old `FinancialDocumentSequences` document, so no issued number repeats. `IFinancialDocumentNumberGenerator` keeps its interface.
- Default series: `Invoicing.Invoice` (`INV-{seq:0000}`), `Invoicing.CreditNote` (`CN-{seq:0000}`), `Invoicing.Estimate` (`EST-{seq:0000}`), `Invoicing.DebitNote` (`DN-{seq:0000}`), and the existing receipt series.

### 6.2 Money formatting (`Payments.Abstractions`)

- `IMoneyFormatter.Format(amount, currency, culture?)`: culture-aware, with the currency symbol or code, using `CurrencyScale` for precision. A Liquid filter `money` and a Razor helper use it.
- The eight private formatters named in §4 move onto it.

### 6.3 Party billing profile (`Parties.Abstractions`)

- `PartyBillingProfile { ContactInfo Contact; Address ShippingAddress; string TaxId; bool TaxExempt; string Currency; string Culture; string PaymentTermId; IList<string> CopyTo }`.
- `IPartyBillingProfileSource { string Kind; Task ContributeAsync(PartyRef party, PartyBillingProfile profile) }` and `IPartyBillingProfileService.GetAsync(PartyRef)`. Several sources may contribute to one kind (Accounts supplies the name and the billing location; B2B adds the tax ID, the billing e-mail and the terms).
- Sources: Accounts, Accounts.B2B, Customers, Users, CRM contacts.
- `IPartyMemberSource`: the user ids that may act for a party (an account's members). The portal and the ledger's visibility checks use it.

### 6.4 Tax profile for a party (`Taxation.Abstractions`)

- `IPartyTaxProfileProvider.BuildAsync(CustomerTaxProfile profile, PartyRef party)`. The checkout tax profile handlers become adapters over it, so an invoice billed to an account with no checkout session still gets its exemption, tax number and business status.

### 6.5 E-mail attachments (`Commerce.Abstractions`, `Commerce.Core`)

- `CommerceEmailContent.Attachments` (`{ FileName, ContentType, Func<Stream> Open }`) and `CommerceEmail.Cc`/`Bcc`. The sender passes them to Orchard's `MailMessage`.
- `ICommerceEmailSender.SendAsync` returns a result with a failure reason, so a document's delivery log can record why a send failed. The boolean overload stays.

### 6.6 PDF library and Documents

- `Pdf.Core`: `PdfFlow` (pages, margins, cursor, page breaks, header and footer callbacks), `PdfFonts` and the font resolver, `PdfText` (measure, wrap), `PdfTable` (columns, alignment, repeated header row), `PdfColor`. No report or invoice types. The Reports PDF add-on keeps its charts and report layout and consumes the library.
- `BusinessDocument` (Documents.Abstractions): `Kind` (open string), `Title`, `Number`, `StatusLabel`, `Issuer` and `Recipient` (`DocumentPartyBlock`: name, address lines, tax ID, e-mail, phone), `ShipTo`, `Fields` (label and value pairs: issue date, due date, reference, terms), `Columns` and `Rows` (the line table, with section and subtotal rows), `Totals` (label, amount, emphasis), `TaxSummary`, `PaymentSchedule`, `Notes`, `Terms`, `Footer`, `Currency`, `Culture`, `Watermark` (for example draft, void, paid), `LogoUrl`, `AccentColor`.
- `DocumentTemplate` (catalog record): name, the kinds it applies to, logo, accent color, paper size, which columns and blocks show, default notes, terms and footer (Liquid), and an optional Liquid body for the HTML form.
- `IBusinessDocumentRenderer`: `RenderHtmlAsync(document, template)`; `IBusinessDocumentPdfRenderer.RenderAsync(document, template)` returns a stream.
- The `BusinessDocument` shape is themeable. Receipts builds its `ReceiptDocument` as before and maps it to a `BusinessDocument` to render.

## 7. Transactions: the complete ledger

Additive changes; no existing field changes meaning.

| # | Change | Why |
|---|---|---|
| TX1 | `Transaction.Payer` (`PartyRef`) and `PayerContact` (`ContactInfo`), indexed as `PayerKind`/`PayerId`. `OwnerId` keeps meaning "the user or guest who may pay". A migration fills `Payer` from `OwnerId`, and from the account parent reference for B2B rows. | One way to say who owes. Account members can see their account's invoices. Work Orders no longer stores an account id as a user |
| TX2 | Visibility through parties: a signed-in user may see a transaction they own or whose payer they are a member of (`IPartyMemberSource`). | Account-level portal |
| TX3 | `Transaction.TaxLines` (the breakdown) and `ExchangeRate` with `BaseCurrency` | Tax reporting per rate; base-currency reporting |
| TX4 | `Document` on the transaction (`{ Kind, Id, Number }`) and on timeline events (a credit note's number on its adjustment) | The ledger names the document behind each entry |
| TX5 | Pay part of a balance: `StartSettlementRequest.Amount` on the pay link and the customer page, with a minimum set by the payer's settings | Partial payment online |
| TX6 | Overpayment goes to customer credit through `ICustomerCreditService` (implemented by Customers store credit) instead of being logged; `ApplyCreditAsync` applies credit to a transaction | No money is left unaccounted |
| TX7 | `ReopenAsync` (a canceled or written-off transaction that is paid after all) and `IncreaseAsync` is **not** added: a debt never grows, a new entry is posted | Ledger entries are corrected by new entries |
| TX8 | Every timeline event gets an id, and a payment recorded in error is undone by a reversing event (`ReversesEventId`), never edited or removed | Append-only ledger |
| TX9 | Payment terms catalog (ID14) with `IPaymentTermCalculator.GetDueDate(term, issueDate)` and early-payment discount fields | Terms |
| TX10 | E-mail kinds `invoice` and `payment-request`; the pay link and document link passed as template values | Changing the reminder template no longer changes the invoice e-mail |
| TX11 | The payment request (the old "invoice" page) renders through Documents and `ITransactionInvoiceBuilder` stays the seam Invoicing replaces | ID2, ID11 |
| TX12 | Workflow events (created, payment recorded, settled, became overdue, canceled, refunded) and tasks (record payment, cancel, adjust); outbound webhook events; recipe and deployment steps for payment terms and for transactions; richer data set fields (payer, tax, document) | Portability and automation, which the ledger has none of today |
| TX13 | Clean-up: wire or remove `IFinancialDocumentPolicy`; remove the never-set `Abandoned` status from pickers; fix the two stale doc pages; the transaction detail page becomes a driver-built page so other modules can add sections | Findings from the survey |
| TX14 (PR B) | Staged reminder policy (offsets before, on and after the due date; per-transaction pause; balance re-check before send) and one saved-method charge service with a retry schedule | §16 |

## 8. Invoicing domain model

All records are catalog records in the `Invoicing` collection, each with its own index table.

**Shared value types (Invoicing.Abstractions)**

- `DocumentParty`: `PartyRef Party`, `ContactInfo Contact`, `Address ShippingAddress`, `TaxId`, `TaxTreatment` (`Taxable`, `Exempt`, `ReverseCharge`, `OutOfScope`), `ExemptionReason`, `CopyTo`.
- `DocumentIssuer`: name, address, tax ID, e-mail, phone, website, logo (a copy of the store profile at issue).
- `DocumentLine`: `Id`, `Kind` (`Item`, `Service`, `Time`, `Expense`, `Usage`, `Fee`, `Deposit`, `Text`, `Section`), `ProductId` and `Sku`, `Name`, `Description`, `Quantity` (6 decimals), `Unit`, `UnitPrice` (up to 6 decimals), `DiscountPercent` or `DiscountAmount`, `TaxCategoryCode`, `Taxes` (manual tax lines), `ServiceStart`/`ServiceEnd`, `SourceType`/`SourceId`, `CreditedLineId`, and the computed `Net`, `TaxAmount`, `Total`.
- `DocumentCharge`: a document-level discount, shipping or surcharge with its own tax category.
- `DocumentTax`: one row of the breakdown: name, category, treatment, rate, taxable amount, tax amount, compound flag, exemption reason.
- `DocumentTotals`: `Subtotal`, `DiscountTotal`, `ChargeTotal`, `TaxTotal`, `RoundingAmount`, `Total`.
- `DocumentEvent`: an append-only history entry (created, issued, sent, viewed, payment, credited, voided, note) with actor and time.

**`Invoice`**

- Identity: `ItemId`, `Number` (empty until issued), `Kind` (`Standard`, `DebitNote`, `Prepayment`), `RevisionOfId`, `ReplacedById`.
- State: `Status`, `Settlement`, `Delivery`, `IssuedUtc`, `VoidedUtc`, `SentUtc`, `ViewedUtc`, `VoidReason`.
- Parties: `BillTo` (`DocumentParty`), `Issuer` (`DocumentIssuer`).
- Money: `Currency`, `ExchangeRate`, `PricesIncludeTax`, `Lines`, `Charges`, `Taxes`, `Totals`, `AmountPaid`, `AmountCredited`, `AmountDue` (mirrored from the transaction after issue).
- Dates: `IssueDate` and `DueDate` (local dates), `ServiceStart`/`ServiceEnd`, `PaymentTermId` and a copy of the term's name.
- References: `PurchaseOrderNumber`, `Reference`, `SourceType`/`SourceId` (estimate, recurring plan, work order, order), `TransactionId`.
- Text: `Notes`, `Terms`, `Footer` (public), `InternalNote`, `CustomFields`, `TemplateId`, `Culture`.
- Collection: `AllowPartialPayment`, `AutoCharge`, `RemindersPaused`, `Schedule` (PR B).
- Audit: `History`, `ContentHash` and `ArchiveFile` (the PDF stored at issue), plus the audited model members.

**`CreditNote`**: the same header and lines; `InvoiceId` and `InvoiceNumber` (the original); `Reason`; `Disposition` (`ReduceBalance`, `Refund`, `CustomerCredit`); `Status` (`Draft`, `Issued`, `Void`); the amounts applied, refunded and credited.

**`Estimate`** (PR B), **`RecurringPlan`** (PR B), **`LateFeePolicy`** (PR B), **`UsageMeter`** and **`BillableItem`** (PR C) are described in their sections.

**Settings (`InvoicingSettings`, site settings):** default template, default term, default notes, terms and footer, prices include tax, default tax treatment, whether partial payment is allowed, whether sending issues a draft, archive PDFs at issue, portal options.

**Settings cascade:** site settings, then the party's billing profile (currency, language, terms, tax treatment), then the document. Every effective value is copied onto the document at issue.

## 9. Life cycles and invariants

**Invoice life cycle** (`TransitionTable<InvoiceStatus>`)

- `Draft → Open` (issue), `Draft → PendingApproval → Open` when an approval rule applies (PR C), `PendingApproval → Draft` (rejected), `Draft →` deleted.
- `Open → Void`: allowed only when no payment is applied; otherwise a credit note is required. Voiding cancels the transaction and keeps the number.
- `Open → Uncollectible` (write-off through a ledger adjustment), `Uncollectible → Open` (reinstated).
- `Void` is terminal.

**Settlement** follows the transaction through `ITransactionEventHandler` and `ITransactionPaymentHandler`: `Unpaid`, `PartiallyPaid`, `Paid`. A refund without a credit note moves it back.

**Delivery:** `NotSent → Sent → Viewed`. Opening the public link records the first view.

**What issue does, in one unit of work**

1. Validates the document (a bill-to, at least one line, a currency, totals that are not negative).
2. Recalculates totals and tax a final time.
3. Copies the issuer, the bill-to profile, the term and the effective settings onto the document.
4. Takes the next number from the series.
5. Posts the transaction (`Payer`, amounts, tax lines, due time, document link) and stores its id.
6. Sets `Open`, the issue time and the content hash, and writes the history entry.
7. After the commit: renders and stores the archive PDF, raises `IInvoiceObserver.IssuedAsync`.

**Invariants**

- A draft has no number, no transaction and no effect on any balance or report.
- After issue, lines, charges, taxes, totals, parties, dates, currency and number never change. The model enforces it in the manager, not only in the UI.
- An issued document is never deleted.
- A credit note cites its invoice by id and number, and can never credit more than what remains creditable on each line and on the invoice.
- A zero-total invoice is issued, posts no transaction and is `Paid` at once.
- Issue, void, credit and send take a distributed lock on the document, and are idempotent: repeating a completed command returns the same result.
- Due dates and issue dates are local dates in the site's time zone. The transaction's `DueUtc` is the end of the due date in that zone.

## 10. Totals, tax and rounding

`IDocumentCalculator.CalculateAsync(DocumentCalculationContext)` returns lines with their computed amounts, the tax breakdown and the totals. Invoices, estimates, credit notes and recurring plans all use it.

- Line net = quantity × unit price, less the line discount, rounded to the currency's precision (`CurrencyScale`).
- Document-level percentage discounts are spread over the discountable lines in proportion, so the tax of each rate is right.
- **Tax providers** (`IDocumentTaxProvider`, ordered): the Taxation provider (in a `[RequireFeatures]` startup) builds a `TaxCalculationContext` from the lines, the bill-to's tax profile (§6.4), the issuer's address and the ship-to, and maps the `TaxLine`s back. The built-in manual provider uses the tax lines typed on each line.
- Tax is computed per (category, rate) on the summed net and then spread over the lines, largest remainder first, so line taxes add up to the breakdown.
- Prices that include tax keep the gross: net = gross ÷ (1 + rate), and the remainder is tax.
- Tax treatment `Exempt`, `ReverseCharge` and `OutOfScope` produce a zero-tax breakdown row with the reason, which the document prints.
- `RoundingAmount` holds cash rounding where a currency needs it.
- The calculator is pure given its inputs. It runs on every draft save (for preview) and once more at issue.

## 11. Payments, credit and refunds

- **Online:** the invoice's pay link is the transaction's pay link. The public invoice page shows the document, its status and a pay button. Coupons, saved methods and every enabled provider work as they do for any transaction.
- **Partial payment** (TX5): when the invoice allows it, the payer may enter an amount between the minimum and what is due.
- **Offline:** "Record payment" on the invoice (amount, date, method, reference, note) calls `RecordOfflinePaymentAsync`. A payment above what is due asks whether the rest becomes customer credit.
- **Autopay:** an invoice marked `AutoCharge` whose payer has a default saved method is charged at issue (or on the due date) through `ChargeSavedPaymentMethodAsync`; a failed charge falls back to sending the invoice.
- **Receipts:** each payment gets its numbered receipt as today.
- **Credit note on an open invoice:** issuing it posts `AdjustAsync(Credit)` with the credit note as the document. If nothing is due afterwards, the invoice is settled.
- **Credit note on a paid invoice:** the disposition decides: refund through `RefundAsync` (with the tax recalculated by `ITaxRefundCalculator`), or add to customer credit.
- **Customer credit:** shown on the invoice when the payer has any, with "Apply credit".
- **Write-off:** `AdjustAsync(WriteOff)` and status `Uncollectible`.
- **Void:** cancels the transaction; payments must be refunded or moved to credit first.
- **Refund without a credit note** stays possible from the ledger for sites that do not need a tax document, and is recorded on the invoice history.

## 12. Delivery: templates, PDF, e-mail, links

- **Template:** a `DocumentTemplate` per invoice (default from settings). The invoice maps to a `BusinessDocument` through `IInvoiceDocumentBuilder`, which other modules extend with `IInvoiceDocumentContributor` (for example a work order adds its job address).
- **PDF:** generated on demand for drafts (watermarked) and stored at issue in the tenant file store (private), with the content hash. Download checks the same access rules as the page.
- **E-mail:** kinds `invoice-issued`, `invoice-reminder`, `credit-note-issued`, `estimate-sent`, each with a Liquid template the site edits, values (`Number`, `Total`, `DueDate`, `PayUrl`, `ViewUrl`, `CustomerName`, `StoreName`) and the PDF attached. To, copy and blind-copy come from the bill-to and can be changed on the send dialog, which also previews the message.
- **Share link:** a signed, expiring link to the public page (`SignedTokenService`), with "Copy link".
- **Viewed tracking:** the first open of the public page by someone who is not staff sets `Viewed`.
- **Delivery log:** every send is a history entry with recipients and result.
- **Language:** the document and its e-mail render in the bill-to's culture when one is set.

## 13. Customer portal and the account page

- **My invoices** (storefront and the customer admin area): invoices the signed-in user can see (their own, or their account's through TX2), with status, amount due, PDF, pay, and "pay several".
- **Public invoice page:** by signed link, no sign-in.
- **Account page:** Invoicing registers its own `Invoices` page section and lists the account's documents with totals due and overdue, and "New invoice". The B2B list of account-terms transactions is removed, and `AccountPageTabs.Invoices` is no longer registered by Accounts.
- **Statements** (PR B): open-item and activity statements per party and period, as a page and a PDF.

## 14. Estimates (PR B)

- `Estimate`: the shared header and lines, `ExpiryDate`, `Status` (`Draft`, `Sent`, `Accepted`, `Declined`, `Expired`, `Canceled`, `Invoiced`), a requested deposit, `Acceptance` (who, when, IP address, typed name, the terms shown), and the percentage invoiced per line.
- Sent estimates are locked; a change makes a new revision (`EST-0001-2`).
- The customer accepts or declines on the public page. Acceptance can create the invoice at once (full, deposit only, or none).
- Conversion copies lines to a draft invoice and links both ways. Progress invoicing (PR C) bills a percentage or an amount per line, never more than 100%.
- An expiry sweep marks estimates expired and raises the event.

## 15. Recurring invoices (PR B)

- `RecurringPlan`: bill-to, lines, charges, template, term, `Rule` (`RecurrenceRule`), start, end by date or count, `Mode` (`Draft`, `Issue`, `IssueAndSend`, `IssueAndCharge`), `Status` (`Draft`, `Active`, `Paused`, `Completed`, `Canceled`), next run, runs so far, and price increase rules.
- Period placeholders in line text (`{{ period.month }}`, `{{ period.year }}`, `{{ period.start | date }}`) through Liquid.
- The generation task runs on a schedule, takes a lock per plan, and is idempotent per plan and occurrence (the invoice's source id is `plan:occurrence-date`).
- Each run pulls unbilled billable items for the plan's party when asked to (PR C).
- A skipped or failed run is shown on the plan and raised as an event.

## 16. Terms, reminders, late fees and collection

- **Terms** (TX9): net days, due on receipt, end of month plus days, day of the following month; early-payment discount (percent, days). The discount is offered on the pay page inside its window and recorded as a ledger discount.
- **Reminders** (TX14): a policy of steps relative to the due date (before, on, after), each with a template. The sweep re-reads the balance before sending, skips paused documents and those with a payment in flight, and sends within the site's daytime hours.
- **Late fees** (PR B): `LateFeePolicy` (flat or percent of the amount due, one time or repeating, grace days, cap, minimum balance). The sweep issues a debit note linked to the invoice (ID16), once per policy step, after re-reading the balance.
- **Failed automatic charges:** the retry schedule of TX14, then the invoice is sent for manual payment and a payment issue is recorded.
- **Per-party overrides:** a party can be excluded from reminders or late fees.

## 17. Deposits and installments (PR B)

- **Deposit on an invoice or estimate:** a percentage or an amount due first; the rest is due on the due date.
- **Installment schedule:** amounts or percentages with their due dates, created on the ledger through the installment plan service, so each installment has its own due date, reminders and autopay.
- **Prepayment invoice** (`Kind = Prepayment`): a deposit invoiced before the work; the final invoice deducts it as a `Deposit` line.

## 18. Billing sources

- **Billable items** (PR C): `IBillableSource` supplies unbilled items for a party (time entries, expenses, work order lines, usage totals). Each item is marked billed with the invoice line that billed it and is released when the invoice is voided or the line is credited.
- **Time** (PR C): Time Tracker gains an optional party and a bill rate on an entry's source reference; no second time ledger.
- **Progress invoicing** (PR C): from an estimate, §14.
- **Usage** (PR C): `UsageMeter` (name, unit, aggregation: sum, maximum, last) and idempotent `UsageRecord`s (event id, party, quantity, time). A recurring plan line can read a meter for its period; price tiers (graduated or volume) are on the line.
- **Subscriptions** (PR C): a "bill this cycle" seam in Subscriptions lets a renewal produce an invoice.
- **Work Orders** (PR A): `IWorkOrderBilling` is widened to pass lines, the party, the tax category and the term. Invoicing implements it and creates a draft or issued invoice with labor and parts lines. The Transactions implementation stays for sites without Invoicing.
- **Orders** (PR B): when Invoicing is on, sending a draft order as an invoice creates an invoice from the order's lines and tax, and an order quote creates an estimate. `OrderInvoice` and `OrderQuote` keep working without Invoicing.

## 19. Multiple currencies

- PR A: a document has one currency, taken from the party's profile or chosen on the draft. Payments must be in that currency (the ledger's rule today).
- PR C: Exchange Rates building block (dated rates, manual entry, a provider seam). The rate to the store currency is copied onto the document and its transaction at issue. Reports can total in the store currency. Markets becomes a consumer of the same rates.

## 20. Cross-cutting

**Workflows** (`[RequireFeatures]` Workflows): events Invoice Created, Issued, Sent, Viewed, Payment Received, Paid, Overdue, Voided, Credit Note Issued, Estimate Sent, Accepted, Declined, Expired, Recurring Invoice Generated, Recurring Run Failed. Tasks: Create Invoice, Issue Invoice, Send Invoice, Record Payment, Void Invoice, Create Credit Note. Events come from `IInvoiceObserver`, so workflows stay optional. Each activity has its schema for the Recipes feature.

**Recipes and deployment:** a recipe step and a deployment step for every record: document templates, payment terms, invoices, credit notes, estimates, recurring plans, late-fee policies, usage meters, number series settings and invoicing settings, each with its schema definition. Issued documents import with their numbers and never take a new one; an import never posts a second transaction for a document that already has one.

**Data sources and reports** (`[RequireFeatures]` Data Pipelines Core): data source `Invoicing` with data sets Invoices, Invoice lines, Invoice taxes, Credit notes, Estimates, Recurring plans. Coded reports under the Reports menu: receivables aging, invoice register, payments collected, sales by customer, sales by item, tax summary.

**API** (PR B): invoices, credit notes, estimates, recurring plans and usage records under the commerce API, with idempotency keys and Problem Details.

**Webhooks:** the workflow events are also outbound webhook events.

**AI tools** (PR C): find invoices, summarize receivables, draft an invoice, send a reminder.

**Permissions:** `ViewInvoices`, `ManageInvoices` (create, edit drafts), `IssueInvoices`, `VoidInvoices`, `ManageCreditNotes`, `RecordInvoicePayments`, `ManageEstimates`, `ManageRecurringInvoices`, `ManageInvoicingSettings`, `ViewOwnInvoices`. Issue, void, credit and payment are security-critical.

**Audit:** the document's own append-only history is the record. Audit Trail events are added for staff actions but are not relied on, because the audit trail is trimmed.

**Personal data:** a contributor exports a person's documents and, on erasure, scrubs the contact fields while keeping numbers and amounts, reporting what was kept.

## 21. Changes to existing modules

| Module | Change |
|---|---|
| Abstractions, YesSql.Core | Number series formats and registration (§6.1) |
| Payments.Abstractions | `IMoneyFormatter` (§6.2) |
| Parties.Abstractions | Billing profile and member seams (§6.3) |
| Taxation | Party tax profile provider (§6.4) |
| Commerce.Abstractions, Commerce.Core | E-mail attachments, copy recipients, send result (§6.5) |
| Reports PDF add-on | Consumes `Pdf.Core`; no behavior change |
| Receipts | Renders through Documents; `ReceiptDocument` unchanged |
| Transactions | §7 |
| Customers | `ICustomerCreditService` over store credit; billing profile source |
| Accounts | Billing profile and member sources; stops registering the Invoices section |
| Accounts.B2B | Billing profile source; terms read the payment terms catalog; its invoice list is removed |
| Work Orders | Wider billing seam (§18) |
| Orders | Bridge startup (PR B) |
| Subscriptions, Time Tracker | Seams in PR C |
| Docs | `building-blocks.md` catalog rows for Documents, Pdf.Core, payment terms, number series, billing profile, Invoicing |

## 22. Non-functional requirements

- **Concurrency:** document commands take a distributed lock and use YesSql concurrency checks. Numbering holds its series lock until commit.
- **Idempotency:** issue, void, credit, send, recurring generation, late fees and usage records carry stable keys built from business ids.
- **Performance:** list pages query index columns only; the account section and portal page are paged; totals for a party come from the transaction index.
- **Time:** every time shown is in the site or user zone through `ILocalClock`; dates entered are local dates.
- **Security:** every endpoint checks a permission; public pages need a valid signed token and are rate limited; PDFs are stored in a private file store.
- **Telemetry:** counters and spans for our own operations only (issue, send, generate, charge), with no ids on metric tags.
- **Retention:** documents are never purged by a retention policy; delivery logs may be.
- **Localization:** all text through localizers; documents render in the bill-to's culture.

## 23. Phases and delivery

Each slice is test-first, then a browser pass, docs, a Release build with warnings as errors, the architecture tests, and one commit.

### PR A: foundations and invoices

| Slice | Content |
|---|---|
| A1 | Number series formats, registration and settings; the financial generator as an adapter |
| A2 | `IMoneyFormatter` and the move of the private formatters |
| A3 | `Pdf.Core` extracted; Reports PDF add-on on it |
| A4 | Documents building block: model, templates, HTML renderer, PDF renderer, recipe and deployment; Receipts renders through it |
| A5 | Party billing profile and member seams with their sources; party tax profile provider |
| A6 | Transactions: payer, visibility, tax lines, document link, partial payment, customer credit, event ids, e-mail kinds, payment request on Documents |
| A7 | Payment terms catalog |
| A8 | E-mail attachments |
| A9 | Invoicing core: model, calculator, store, manager, issue, void, settings, permissions |
| A10 | Invoice admin: list, editor with line items, detail page, actions |
| A11 | Payments and credit notes |
| A12 | Delivery: document builder, PDF archive, send dialog, share link, viewed tracking |
| A13 | Portal pages and the account section; B2B list removed |
| A14 | Work Orders billing through Invoicing |
| A15 | Workflows, webhooks, recipes and deployment, data source and reports, personal data |
| A16 | Docs, catalog, skill, changelog |

### PR B: estimates, recurring and collection

Estimates; recurring plans; staged reminders and the saved-method charge service; late fees and debit notes; deposits, installments and prepayment invoices; statements; the Orders bridge; the API; transactions recipe and deployment.

### PR C: advanced billing

Billable sources, time and expense billing, progress invoicing, usage meters and tiers, the Subscriptions seam, Exchange Rates, approvals, AI tools, revision flow.

## 24. Testing strategy

- **Unit:** the calculator (rounding, inclusive prices, compound tax, spread of discounts, treatments), number formats and resets, term due dates, the status tables, credit limits per line.
- **Integration** (the test host with SQLite): issue posts one transaction and one number; a failed issue consumes no number; concurrent issues give distinct consecutive numbers; settlement mirrors onto the invoice; void and credit rules; recipe export and import round trip; the financial generator continues after the old sequence.
- **Architecture:** the layering, feature id, recipe schema, workflow view, editor root view and settings permission guards must pass with the new modules.
- **Browser:** every new admin and customer page, in list and grid layouts, with a full invoice flow: draft, issue, send, pay by link, partial payment, credit note, void.
- **PDF:** a rendered invoice is parsed back and checked for number, totals and line count.

## 25. Out of scope

- Structured e-invoice files and network delivery (ID3): later phase.
- Purchase-side documents (bills, purchase orders, vendor credits).
- A general ledger, chart of accounts, revenue recognition and bank reconciliation. Exports to accounting systems can be built on the data source.
- Tips and payment surcharges.
- Electronic signature beyond a typed-name acceptance record.

## 26. Risks

| Risk | Mitigation |
|---|---|
| The ledger changes touch a module eight others use | Additive fields only; a migration that fills `Payer`; the existing tests of every consumer run in each slice |
| Moving receipts onto a new renderer changes their look | Keep the receipt layout as the default template for the receipt kind; compare before and after in the browser pass |
| Numbering merge could repeat a number | The adapter seeds from the old store; an integration test proves the next number continues |
| PDF fonts cover Latin-1 only when no installed font is found | The shared library keeps the installed-font resolver and reports the fallback; documented |
| Scope | Three PRs; each slice is shippable |

## 27. Glossary

- **Issue:** the moment a draft becomes a legal document: it gets its number, is locked and posts to the ledger.
- **Ledger:** the Transactions module's record of money owed, paid, adjusted and refunded.
- **Payment request:** what Transactions shows for a debt on a site without Invoicing: the amount due and a pay button.
- **Credit note:** a document that reduces or reverses an issued invoice.
- **Debit note:** a document that adds a charge related to an earlier invoice, such as a late fee.
- **Bill-to:** the party that owes, with the copy of its details taken at issue.
- **Series:** a named sequence of document numbers with its own format.

## 28. Progress

Each entry says what was built, what differs from the plan above, and what is still open. Keep it current: it is the handover between sessions.

### Naming and design changes made while building

- The block §5 and §6.6 call **Documents** is named **BusinessDocuments** in code (`CrestApps.OrchardCore.BusinessDocuments`, `.Abstractions`, `.Core`; features `CrestApps.OrchardCore.BusinessDocuments` and `.Pdf`), so it is not confused with the AI Documents modules. Its model is `BusinessDocument`, its renderers `IBusinessDocumentHtmlRenderer` and `IBusinessDocumentPdfRenderer`, its template record `BusinessDocumentTemplate` with `IBusinessDocumentTemplateManager`, and consumers register kinds with `AddBusinessDocumentKind`.
- §6.1: series are registered with `AddNumberSeries(name, title, NumberSeriesFormat)` and issued by `INumberSeries.NextAsync(series, NumberSeriesRequest)`, which returns a `SeriesNumber(Sequence, Text)`. Financial documents use the series `Financial.Receipt`, `Financial.Invoice`, `Financial.CreditNote` and `Financial.RefundDocument` (`FinancialDocumentSeries`), default formats `R-{seq}`, `INV-{seq}`, `CN-{seq}`, `RF-{seq}` from 1001, so the numbers a site already issued continue. **Invoices and credit notes take their numbers from `IFinancialDocumentNumberGenerator`** (kinds `Invoice` and `CreditNote`), not from separate `Invoicing.*` series; estimates and debit notes register their own series. The first number of a series only applies when the series is new.
- §6.2: the shared format is the static `CurrencyFormat` (`WithCode`, `WithSymbol`, `GetSymbol`) in `CrestApps.OrchardCore.Abstractions`, with `IMoneyFormatter` (`AddMoneyFormatter()`) in Payments.Abstractions for views and documents. Left on their own formatters: the Stripe currency helper (provider precision) and `SubscriptionReportBase.FormatCurrency` (grouped, always two decimals).
- §7 TX1: the payer is stored as `Transaction.PayerKind` and `Transaction.PayerId`, read and set through `GetPayer()` and `SetPayer(PartyRef)`. The index derives the payer of older rows (the account in the parent reference, else the owner), and the migration re-maps existing rows in a deferred task, so no stored document is rewritten.
- §7 TX4: there is no `Transaction.Document`. An invoice's transaction has `ReferenceType = "Invoice"` and `ReferenceId` = the invoice id, and `InvoiceNumber` holds its number. Timeline entries carry `DocumentKind`, `DocumentId` and `DocumentNumber` (a credit note behind its adjustment).
- §7 TX5: part payments are `Transaction.AllowPartialPayment` and `MinimumPayment`, `StartCheckoutRequest.Amount` (kept on the session as `CheckoutRequestedAmount`), and `TransactionStateMachine.TryGetPaymentAmount`. A part payment takes no promotion code.
- §7 TX7 and TX8: there is no `ReopenAsync`. `ITransactionCommandService.ReverseAsync(transactionId, eventId, reason)` takes back a payment recorded by hand or an adjustment with a `Reversed` entry (`ReversesEventId`), and a transaction it had settled becomes collectable again; that is how a write-off is undone. `RecordPaymentAsync(id, TransactionPaymentRequest)` records the payment method and received date and returns `UnappliedAmount`; `AdjustAsync(id, TransactionAdjustmentRequest)` names the document and records it once. `TransactionManager` gives every timeline entry an id when it saves.

### Done

| Slice | Commit | Notes |
|---|---|---|
| Plan | `5d1000444` | |
| A2 Money format | `9762c82b7` | |
| A1 Number series | `660bcd1c5` | Contracts, service and the financial generator adapter. The **settings page** for number series is part of A4 (BusinessDocuments module) |
| A3 PDF library | `5b825ee62` | `CrestApps.OrchardCore.Pdf.Core`: `PdfFlow`, `PdfPage`, `PdfFont`, `PdfFonts`, `PdfFontResolver`, `PdfText`, `PdfColor`. Report charts, tables and theme stay in the Reports PDF add-on |
| A6 Ledger (part) | `1260e979b` | TX1 (payer, index, migration, query by payers), TX3, TX4 (as changed above), TX5, TX7 and TX8 (as changed above). 254 Transactions tests pass |
| A6 Ledger (TX10, TX12, TX13) | see `git log` ("Give the ledger its own payment request template, workflows, webhooks and a driver-built page") | TX10: kind `payment-request` for `SendInvoiceAsync`; every request and reminder passes `PayUrl`, `DocumentUrl`, `DocumentNumber`, `AmountDue`, `DueDate` as template values (`TransactionEmailKinds`). TX12: six workflow events and three tasks (record payment, cancel, adjust) with views and activity schemas; six `transaction.*` webhook events with stable event ids; `ITransactionEventHandler.RefundedAsync` (raised by `TransactionRefundHandler`); data set fields payer kind/id/name/e-mail, tax rates, exchange rate, base currency, part payments. TX13: `IFinancialDocumentPolicy` and its receipts-only default removed (never called); *Abandoned* no longer offered in the status filter; the admin detail page is built by `TransactionDetailDisplayDriver` for display type `DetailAdmin` (zones `Content`, `Actions`); `commerce.md` numbering section rewritten. 269 Transactions and 293 Commerce tests pass; workflow view and recipe schema guards pass |

| A7 Payment terms | see `git log` ("Add the payment terms catalog") | Feature `CrestApps.OrchardCore.Transactions.PaymentTerms`. `PaymentTerm` (`Kind` = `DueOnReceipt`, `NetDays`, `EndOfMonth`, `DayOfNextMonth`; `Days`, `DayOfMonth`, `DiscountPercent`, `DiscountDays`, `Enabled`), `EarlyPaymentDiscount`, `IPaymentTermCalculator` (works on `DateOnly`), `IPaymentTermManager` with `GetEnabledAsync`, all in Transactions.Abstractions; store, manager, calculator, index (`PaymentTermIndex` in the Transaction collection) and index provider in Transactions.Core (`AddPaymentTermsCore`). Admin CRUD on `CatalogAdminController` at `Admin/commerce/payment-terms` (menu Commerce > Payment terms, permission `ManagePaymentTerms`), recipe step and deployment step `PaymentTerms` (entries under `Terms`) with schemas. Tests: calculator (all kinds, month ends, leap year, rounding per currency), handler validation, recipe import/export/replay. Browser: created terms (net days with discount, day of next month), validation messages, List and Grid layouts, Buttons and Menu actions. **Bug found in the browser and fixed with a regression test:** the hidden "day of the next month" field (0, `min=1`) stopped the form from being sent; hidden rows now disable their inputs. Not checked in the browser: the deployment plan export and recipe import (covered by the round-trip test) |

| A4 BusinessDocuments (agent branch `worktree-agent-ad38834e38cf8137d`) | cherry-picked as `d1f3f251f`, `dd9b4d738`, `48b9d34f7`, `2ee6ccd4a` | Only `sidebars.js` conflicted (additive). |
| A5 and A8 parties and e-mail (agent branch `worktree-agent-acf4e4e8455d10bc2`) | cherry-picked as `40f80dbb9`, `8d49a4b47`, `61f73258c` | No conflicts. |
| Merge review fixes | `43ff987b3` | `ReceiptsBusinessDocumentsFeatureMigrations` records Business Documents as enabled on a site that had Receipts before (on the test site it shows Enabled after the upgrade). `IReceiptHtmlRenderer.Render` is `[Obsolete]`; no caller in `src` remained, the two tests use `RenderAsync`. A receipt has a **Currency** field with the ISO code (US and Canadian dollars both print "$"). User and customer billing phones that are not E.164 are normalized through `IPhoneNumberService` (a customer's with the billing address's country) instead of dropped; a phone that still cannot be read is left off. A CRM contact with several address fields is billed at the first one that holds an address in editor order (part position, field position, stored order). Test showing checkout leaves the B2B account address off its tax profile while the party tax profile carries it. The number series page already labels its sample "Example". 3015 tests of the merged areas, 69 public API tests and the JS lint tests (7 files, 21 tests) pass. |

- **Browser pass of the merged pages:** document templates list in List/Buttons and Grid/Menu, template created (default, receipts, accent color, A4, footer); number series page: the example follows the pattern as it is typed, a yearly invoice pattern saved, a pattern without `{seq}` refused with its message; account editor: the Billing location checkbox and hint, the Billing badge on the account page, and "Only one location can be the billing location." when two are marked. **Not checked:** printing a receipt (the site has no paid transaction yet; checked once an invoice is paid in A11), the PDF of a business document.
- **Left for Invoicing (A9), from the A5 review:** resolve `IPartyTaxProfileService` only in a `[RequireFeatures]` Taxation startup inside Invoicing (Invoicing works without Taxation through the manual tax provider); a document billed to a user must still get a name and e-mail when the CrestApps Users module is off (fallback through `IPartyDirectory` and Orchard's user store).
- **A9 pure parts:** committed with the subject "Add the invoicing document model, life cycle rules and calculator". Projects `CrestApps.OrchardCore.Invoicing.Abstractions` (feature ids in `InvoicingConstants.Features`, collection `Invoicing`, the model of §8: `InvoicingDocument` as the shared header of `Invoice` and `CreditNote`, `DocumentLine`, `DocumentLineTax`, `DocumentCharge`, `DocumentTax`, `DocumentTotals`, `DocumentParty`, `DocumentIssuer`, `DocumentEvent`/`DocumentEventKinds`, the status enums, `InvoicingSettings`, `IDocumentCalculator`, `IDocumentTaxProvider`) and `CrestApps.OrchardCore.Invoicing.Core` (`DocumentCalculator`, `InvoiceLifecycle`, `CreditNoteLifecycle`, `CreditNoteRules` with `CreditableAmounts`/`CreditNoteError`, `AddInvoicingCore`, `AddDocumentTaxProvider<T>`). 74 tests (calculator, life cycles, credit limits per line).
- **Deviation from §8 and §10 (review 1, point 1):** tax treatment is per line, not only per document. `DocumentLine` and `DocumentCharge` have `TaxTreatment` (nullable: null takes the document's) and `TaxTreatmentReason`; `DocumentLineTax` has `Treatment` and `TreatmentReason` so the Taxation provider maps `TaxLine.Treatment`/`TreatmentReason` straight across. The breakdown has one row per (treatment, code, rate, compound); a zero-rated, exempt, reverse-charged or out-of-scope line, or a taxable line with no tax, adds its net to a zero-tax row of its treatment with its reason. Tax treatment reuses Taxation's `TaxTreatment` (which also has `ZeroRated`) instead of a second enum. Prices that include tax keep the gross as the net when no tax applies (a business rule, stated in the calculator's remarks; the Invoicing docs page in A16 states it too). Line discounts are clamped in the calculator (0 to the line amount, percent 0 to 100); the manager's validation (A9 manager) refuses them. `Allocate` floors every share and hands the remaining units back largest remainder first, which is correct for weights of mixed sign (a negative deposit line in a tax group).
- **Review 1, ledger points 4 to 8:** committed with the subject "Let staff take back ledger entries and record how payments arrived". Admin transaction page: payer (name from `PayerContact` or `IPartyDirectory`, kind, e-mail), tax lines, exchange rate, part payments; **Record payment** takes a payment method and a received time entered in the site's time zone (converted with `ToUtcLenientAsync`) and calls `RecordPaymentAsync`; the timeline shows document numbers, payment method and received time, marks entries taken back, describes what a reversal took back, and offers **Take back** (`AdminController.Reverse`, `ManageTransactions`, required reason, confirmation) on a payment recorded by hand or an adjustment; `EventTypeName` has `Reversed` ("Taken back"). `ITransactionEventHandler.ReversedAsync` (raised after the save by `ReverseAsync`), workflow event **Transaction Entry Taken Back** with views and schema, webhook `transaction.entry-reversed` with `reversedEntry` and the stable id `evt_transaction-reversed_{transaction}_{reversal}`. `RefundedAsync` and `ReversedAsync` have default interface implementations. `PaymentTerm` is `IAuditedModel` and `PaymentTermHandler` derives from `AuditedCatalogEntryHandlerBase`. Receipts calls `AddBusinessDocumentsCore()` itself, and `DefaultReceiptHtmlRenderer` renders with the receipt's own look (logged warning) when the template store cannot be read. 982 tests of the touched areas pass. **Not yet opened in the browser:** the new transaction page parts (no transaction on the test site yet; checked with the first issued invoice).
- **Open from review 1 for the manager (A9 manager):** decide void, write-off and credit limits from the transaction loaded under the document lock, not from the mirrored amounts; refuse to void an invoice with an issued credit note; integration tests for a payment recorded a moment before a void, and for void with a credit note.
- The test site `App_Data_Invoicing` is set up (admin / `Password1!`). Start it with the scratchpad script `start-server.sh` and open it as `http://invoicing.localhost:5482`: another local site on 127.0.0.1 overwrites its sign-in cookie.
- **TX2 (party visibility, review 2 point 1):** committed with the subject "Let account admins see and pay what their account owes". Membership has levels in the owner: `PartyAccess` (`Member`, `Billing`) in Parties.Abstractions, overloads of `IPartyMembershipService` and `IPartyMemberSource` that take one (the old ones answer for `Member`), and source defaults that grant `Billing` to no one. The user and customer sources give their one member every level; `AccountPartyMemberSource` gives `Billing` only to `AccountMemberRole.Admin` (new `IAccountMemberService.GetMembersAsync`), the same line `BusinessAccountController` draws for orders and approvals. `ITransactionVisibilityService` (Transactions.Abstractions; `DefaultTransactionVisibilityService` in Transactions.Core, registered by `AddTransactionsCore()` with `AddPartyMembership()`) asks for `Billing`: `CanViewAsync` (owned as a signed-in owner, or billing access to a non-guest payer) and `CreateQueryAsync` (owner or payers, which `TransactionStore` turns into an OR). `TransactionCustomerPageService` (statement, invoice and receipt in the admin and the site theme) and `TransactionController` (admin statement, detail, pay) use it; `MyTransactionsController` goes through the page service. `FakeTransactionStore` matches owner OR payer like the real store. Tests (with the real membership service and account source over an admin and two buyers): an admin lists and opens the account's transaction, invoice and receipt; a plain member gets "not found" and does not list it; a member still sees their own transaction billed to the account; a plain member's statement query carries no account payer; a source that does not override the levels grants no billing access. The rule is written in the Parties (`#access-levels-member-and-billing`), Accounts and Transactions pages. Other money uses checked: saved payment methods are the owner's (`ChargeSavedPaymentMethodAsync`), not a party's; the "My invoices" portal and paying several invoices (A12) must use `ITransactionVisibilityService`, and pay-several still groups by owner (`TransactionSettlementCheckoutHandler`), so an admin paying invoices posted by several buyers needs it grouped by payer then. **Not checked in the browser:** an account admin's and a buyer's statements (the test site has no account with members and a debt yet; checked with the invoice portal in A12).
- **Review 2, points 2 to 4:** committed with the subject "Render receipt templates only once their tables exist, and give payment methods stable keys". `DefaultReceiptHtmlRenderer` applies the document template only when `IShellFeaturesManager.IsFeatureEnabledAsync(BusinessDocumentsConstants.Features.Area)` says Business Documents is recorded as enabled (its tables exist then), and the broad catch is gone, so no query runs against a missing table (which on PostgreSQL would abort the transaction that records the payment) and a real template error is no longer hidden. `ReceiptsUpgradeWindowTests` proves no template call is made in the window (a strict mock) and that a template error throws once the feature is on. Payment methods: `TransactionsConstants.OfflinePaymentMethods` (`cash`, `check`, `bank-transfer`, `card-terminal`, `other`); **Record payment** offers their localized labels through a `datalist` and still takes free text; `ITransactionCommandService.RecordPaymentAsync` stores a common method picked or typed by its key or label as its key (`TransactionDisplayText.NormalizeOfflinePaymentMethod`), so the admin form, the workflow task and any caller agree; the timeline shows the localized label. The timeline's "Takes back the {0} of {1} {2} recorded on {3}." and "Received {0}." are single strings with the date, converted with `ILocalClock`, as an argument. **Not checked in the browser:** the datalist and the timeline sentences (no transaction on the test site yet; checked with the first issued invoice).
- **TX6 (customer credit):** committed with the subject "Keep what payers pay above a balance as customer credit, and pay balances with it". `ICustomerCreditService` in Customers.Abstractions (the owner of store credit; `CanHold`, `GetBalanceAsync`, `AddAsync`, `SpendAsync`, with `CustomerCreditChange` and `CustomerCreditResult`/`CustomerCreditError`), keyed by the payer `PartyRef`; Customers.Abstractions now references Parties.Abstractions. The Store Credit feature implements it (`StoreCreditCustomerCreditService`): a user's credit is on their customer record, made on the first credit; accounts, contacts and guests can't hold credit. `StoreCreditService.TryChangeAsync` spends only what the account holds and changes once per reference (`StoreCreditActivity.Reference`). Transactions: `TransactionPaymentRequest.CreditExcess` keeps the excess as credit before the payment is saved (`TransactionEvent.CreditedAmount`, `TransactionCommandResult.CreditedAmount`) and refuses the payment when credit can't be kept; `ITransactionCommandService.ApplyCreditAsync` spends credit and records a payment with settlement method `customer-credit`; `ReverseAsync` gives credit back for a credit payment and takes back the credit of a payment whose excess was kept, refused when it was spent; a credit change made for a command whose transaction then fails to save is undone (reference + `:undo`, logged as an error when the undo fails). Admin: the checkbox **Keep anything above what is owed as customer credit** on Record payment, a warning when an excess was not recorded, and **Apply customer credit** when the payer has credit. Tests: 4 store credit (once per reference, never spends more than there is, no account, parties that can't hold credit) and 8 ledger (excess kept, refused without the feature or for an account payer, apply credit, refused above the balance, take back gives or takes the credit back, refused when spent). **Deviation:** the plan said overpayment goes to credit "instead of being logged"; it goes to credit only when staff ask (`CreditExcess`), because cash above a balance is often handed back; otherwise the console says how much was not recorded. **Not done:** credit on a business account (store credit belongs to a customer record), the excess option on the Record Transaction Payment workflow task, and a test of the undo after a failed save (the fake store does not fail saves). **Not checked in the browser:** the checkbox and the Apply customer credit card (no transaction on the test site yet; checked with the first issued invoice).
- **TX6 follow-up:** committed with the subject "Keep the credit reference on store credit activity only". The reference had been added to the shared `BalanceLedgerEntry`, which changed the stored shape of gift card activity (caught by `BalanceLedgerTests` in the Checkout tests, which the TX6 run had not included). It is now `StoreCreditActivity.Reference`, and gift card activity keeps exactly its fields.
- **TX11 (payment request through BusinessDocuments):** committed with the subject "Print the payment request of a transaction as a business document of its own kind". `ITransactionInvoiceBuilder` returns a `BusinessDocument` (Transactions.Abstractions now references BusinessDocuments.Abstractions) of the kind `TransactionsConstants.BusinessDocumentKinds.PaymentRequest`, registered by `ReceiptPagesStartup` as *Payment requests*, so a site gives it its own template. `TransactionInvoiceBuilder` keeps the receipt settings' business details (through `IReceiptService`), maps with `ReceiptBusinessDocumentMapper`, sets the title, the number (not repeated as a field), leaves off adjustments that were taken back, prints a tax line per rate from `Transaction.TaxLines`, addresses the payer (`PayerContact`) before the owner, and applies the template only once Business Documents is recorded as enabled. `TransactionInvoiceViewModel.Invoice` and `TransactionPayViewModel.Invoice` are `BusinessDocument`s, and the invoice pages (admin and site theme) and the pay page render the `BusinessDocument` shape instead of the Receipts view. The Receipts view `ReceiptDocument.cshtml` got the same feature check before reading the template (review 2 point 2 had only covered the renderer). Tests: 3 builder tests (kind, title, number, adjustments in force, total due, status; tax per rate and payer; template only once the feature is on), and the page service and DI tests moved to the new type. **Kept:** the title stays "Invoice" and the number comes from the invoice series, as customers know it; the receipt pages still use the Receipts view. **Not checked in the browser:** the invoice and pay pages (no transaction on the test site yet; checked with the first issued invoice).
- **A6 payers where debts are posted:** committed with the subject "Name the payer where account terms and work orders post their debts". `DeferredBalanceDebit.Payer` (Checkout.Core) is set on the transaction by `DeferredBalanceTenderProviderBase`; account terms name the account (`PartyKinds.Account`, the account's content item id), so the account's admins see the balance through TX2. A billed work order is owed by its account, else its customer user, else its CRM contact, with the work order's contact as `PayerContact`; it no longer stores an account id as the owner: the customer user owns it, and one with no user is owned as a guest (the work order id) and reached at the contact's e-mail. Bookings and Pay Later already resolve to the buyer (a user or a guest) and are unchanged. Tests: the debit names the account as payer; an account's work order is owed by the account and owned as a guest; a customer's is owed by the user. With this, A6 is complete.
- **A9 manager:** committed with the subject "Add the invoice store, manager and commands, and the Invoicing module". Module `CrestApps.OrchardCore.Invoicing` (feature depends on Transactions, Payment Terms, Business Documents; in the slnx, Cms.Core.Targets and the tests). Invoicing.Core: `InvoiceIndex`/`CreditNoteIndex` with providers and `InvoicingMigrations`; `IInvoiceStore`/`InvoiceStore` and `ICreditNoteStore`/`CreditNoteStore` (concurrency checks); `IInvoiceManager`/`InvoiceManager` with `InvoiceQuery`; `InvoiceHandler` (audit, line rules, refuses changes after issue); `InvoiceIssueRules` and `InvoiceIssueMessages` (bad discounts, quantities, charges, negative totals refused); `IInvoiceCommandService`/`DefaultInvoiceCommandService` with Issue (lock, billing profile and user fallback through `ICustomerContactResolver`/`IPartyDirectory`, final calculation, term due date, issuer from `StoreProfileSettings`, number from `IFinancialDocumentNumberGenerator`, one transaction posted in the same unit of work, zero total paid at once, content hash, observers after the save), Void (refused with an issued credit note; `ITransactionCommandService.CancelUnpaidAsync`, new, refuses under the transaction lock when anything was paid), WriteOff (amount from the transaction; the adjustment refuses more than is owed under its lock) and Reinstate (reverses the write-off); `IInvoiceObserver`; `InvoicingPermissions` (every permission implies View; View is not implying the others). Module: permission provider, settings driver, view, menu (Settings > Commerce > Invoicing), settings recipe schema, and `TaxationStartup` (`[RequireFeatures]` Taxation) registering `TaxationDocumentTaxProvider` over `ITaxService` and `IPartyTaxProfileService`. `IndexPredicates.OrElse` extracted into YesSql.Core (TransactionStore uses it; the Omnichannel copy remains). Tests: 9 SQLite integration tests (issue once, user fallback, bad discount takes no number, zero total, void and repeat, void after a payment the invoice did not show yet, void with a credit note, write-off and reinstate from the ledger, no change after issue) and 2 Taxation provider tests; 85 Invoicing and 334 Transactions tests pass. Separate fix committed: `618398d9f` payment request tax rates printed in percent. Docs: module README, `modules/invoicing.md`, `user-manual/commerce/invoicing.md`, both sidebars, feature reference, changelog. The guards found `DateTime.UtcNow` in the Taxation provider; it takes `IClock` now. **Browser:** after the review 3 commits, the settings page (Settings > Commerce > Invoicing) opens once the feature is enabled, saves, stores the currency in capitals (`eur` became `EUR`) and refuses a four-letter currency and a negative rounding step in the browser.
- **Review 3 (customer credit), one commit per point:**
  - Point 5, "Prove a work order debt with no user reaches the work order's contact": the reminder and the receipt e-mail of a work order transaction owned as a guest (the work order id) go to the contact's address (tests); the pay link carries only the transaction id.
  - Point 4, "Export and erase store credit as personal data": `StoreCreditPersonalDataContributor` exports balances with their activity and scrubs reasons and actors on erasure; the Customers docs say keeping credit for a user without a customer record creates one.
  - Point 1, "Let every payer but a guest hold customer credit": a store credit account belongs to a customer record (as before) or to another party (`StoreCreditAccount.PartyKind`/`PartyId`, `StoreCreditHolder`, index columns and `StoreCreditMigrations.UpdateFrom1Async`; existing accounts keep their customer, so no balance moved). `ICustomerCreditService.CanHold` accepts every party but a guest; the new `CanSpendAsync` lets a user spend their own credit or a party's they have `PartyAccess.Billing` for (an account's admins, not its buyers); staff apply credit through Manage transactions. The transaction page shows an account payer's credit. Tests: an account holds and spends its own credit; only an account admin may spend it; a guest is refused; the ledger keeps an account payer's excess as the account's credit and pays another of its debts with it.
  - Point 3, "Keep a balance's latest changes and move older ones to records": `IBalanceLedgerArchive` (Checkout.Core, one implementation for every balance) is called from the balance store's `SavingAsync` and keeps `BalanceLedger.RecentEntryCount` (50) entries on the document, moving older ones to `BalanceLedgerEntryRecord` documents indexed by ledger, balance and reference (`BalanceLedgerEntryIndex`, created by `CheckoutTenderMigrations.UpdateFrom1Async`; Store Credit and Gift Cards depend on Checkout). `StoreCreditStore` and `GiftCardStore` use it (Orders.Core now references Checkout.Core). `StoreCreditService.TryChangeAsync` finds a reference in the at most 50 recent entries or with a keyed lookup in the records. The personal data contributor exports and scrubs the moved-out records too. Test on SQLite: 60 changes leave 50 on the account and 10 records, and repeating an old and a recent change both find it already made. Loyalty points keep their own `LoyaltyActivity` (not the shared base) and are not changed.
  - Point 2, "Save the ledger entry before changing customer credit, and complete it later": the timeline entry is the durable intent and the credit change its idempotent follow-up. `TransactionEvent.CreditChange` (signed) and `CreditState` (`None`, `Pending`, `Settled`, `Failed`); `TransactionIndex.PendingCredit` with `TransactionMigrations.UpdateFrom3Async`. Record payment with `CreditExcess`, Apply credit and Reverse check first that credit can change (feature on, payer can hold it; for a spend, the balance), save the entry `Pending`, then `TransactionCreditSettlement` (internal) changes the credit under the transaction lock with the reference `Transaction:{id}:{entryId}`, reads the transaction again (the credit commit detaches it) and marks the entry. A spend that finds too little credit takes its payment back with a reversing entry (`TransactionReversal`, shared with `ReverseAsync`); a change that can't be made marks the entry `Failed` and records a `PaymentIssueKinds.CreditNotReconciled` issue through `IPaymentIssueRecorder` (logged when none is recorded). Waiting entries are completed before every command (`ExecuteAsync`), by the reminders background task (up to 100 transactions a run) and by the new `ITransactionCommandService.ReconcileCreditAsync`; the old undo with `:undo` is gone. `TransactionCommandResult.Warnings` carries what did not finish, and the admin page shows it. Tests on SQLite with the real ledger, store credit and archive (`TransactionCreditDurabilityTests`): excess kept as credit saves the transaction once with no concurrency failure; a stop after the credit change, then two retries, moves the credit once; a waiting entry is completed by the next command; a credit payment with too little credit is taken back; a payer that can't hold credit gets a payment issue. **Not done:** the Record Transaction Payment workflow task still has no excess-to-credit option.
- **A10 invoice admin:** committed with the subject "Add the invoice admin: list, draft editor, invoice page and actions". `InvoicesAdminController` (Commerce > Invoices, `InvoicesAdminMenu`): the list through `AdminList` (name `Invoices`, List and Grid, search over number, bill-to name and reference, Status and Payment filters with `CreateAdminListFiltersAsync`), the draft editor (Create and Edit share `Edit.cshtml`; plain form rows mapped by `InvoiceDraftEditor`, which keeps a row's id and what the editor doesn't show, drops rows ticked Remove and ignores the blank row unless something was typed; **Update** saves and stays, **Save draft** opens the invoice, **Save and issue** saves then runs `IssueAsync` and needs Issue invoices too), the invoice's page built by `InvoiceDisplayDriver` for the new `InvoicingConstants.DisplayTypes.DetailAdmin` (summary, lines with taxes and totals, history in the site's time zone, actions card), and Issue, Void, WriteOff, Reinstate and Delete (drafts only) actions, each checking its permission. The bill-to selector is the Resources `ItemSelector` over a new endpoint `Admin/api/crestapps/invoicing/parties/search` that searches every `IPartyDirectory` kind but guests and posts `PartyRef.ToString()`; the feature now depends on Resources. `InvoiceCalculation` (Invoicing.Core) is the one calculation issue and the editor use, so a saved draft shows the totals it is issued with. `InvoiceCheckoutReferenceDescriber` names the invoice on its transaction (the transaction page showed "Invoice(id)" before). **Found by the browser pass and fixed here:** the A9 startup never declared the `Invoicing` collection in `StoreCollectionOptions`, so on a real site every invoice page failed with "no such table: Invoicing_Document" (the SQLite harness creates the collection itself, so the A9 tests passed); the guard `ModuleWiringTests.EveryStoreCollection_IsDeclaredInStoreCollectionOptions` compared only the constant's last name segment, so `TransactionsConstants.CollectionName` hid the missing `InvoicingConstants.CollectionName`; it now compares the class and the constant (options read through an instance by member, and one constant, `ProfileBuilderStorage.CollectionName`, listed as naming the AI collection by value). Also fixed: the transaction timeline printed "Paid by Check.Received …" without a space. Tests: 4 editor mapping (ids kept, removed and blank rows, new row id; bill-to and contact; no bill-to; one blank row), the party search across kinds without guests, the draft totals equal the issued totals (with cash rounding), and the reference describer; 92 Invoicing tests pass. **Browser (A10 and the owed pass):** list in List and Grid with every column filled, search by number and name, both filters; new invoice with the customer search (found the admin user), a discounted line and a section heading, Update (totals 3 × 120 less 10% = 324.00 and a new blank row), Save draft, the draft page, Issue with its confirmation (INV-2026-1001, due on issue); Save and issue for a contact-only bill-to with part payments allowed (INV-2026-1002); write off, reinstate and void (the transaction became Canceled); delete draft (the page then 404s); the editor refuses a four-letter currency and a 150% discount, and an issued invoice opens on its page instead of the editor. Owed checks: the driver-built transaction page (payer, source, reference link, tax line, invoice link), Record payment with the method datalist (a 100 part payment by check: Partially paid, the timeline says "Paid by Check" and the local received time), the printed receipt R-1001, take back with its confirmation (owed again, the reversal sentence with its local date), the payment request (the business document with the pay link and "USD 100.00 has been received; USD 224.00 is still due"), and the pay page with a part payment (150 carried into checkout as "Due now USD 150.00"). **Not checked in the browser:** finishing that online payment (no payment provider on the test site), a business document PDF (no page renders one yet: the download comes with A12), Apply customer credit and the keep-as-credit checkbox (Store Credit is not enabled on the test site), an account admin's and a buyer's statements (still no account with members), the Menu actions layout and a phone-width window. **Not done:** the editor sets no issue date or due date by hand (issue sets today and the term's due date), and offers no document charges or manual line taxes yet; the party search endpoint lives in Invoicing because Parties has no module (a candidate to move if a second module needs it).
- **A11 payments and credit notes:** committed with the subject "Keep invoices in step with their payments, and credit them with credit notes". **Payments:** `InvoiceLedgerMirror` (pure) and `InvoiceLedgerSync` (Invoicing.Core) copy the transaction onto the issued invoice: paid less refunded, credited (the issued credit notes' totals), still due, settlement (paid and credited against the total; a write-off is not paid), and one history entry per payment, take-back and refund, keyed by the ledger entry's id. `InvoiceTransactionHandler` (payment and event handler, registered by `AddInvoicingCore()`) runs it after each ledger change, under the invoice's lock; while an invoice command holds the lock it skips (the command mirrors what it causes), and the invoice's page catches up whatever was missed. **Credit notes:** `ICreditNoteCommandService` (`CreditNoteCommandResult`) and `DefaultCreditNoteCommandService`: `CreateDraftAsync` (lines still creditable, `CreditedLineId`, the default disposition from what is owed and whether the payer can hold credit, one draft at a time) and `IssueAsync` (calculation, line rules, `CreditNoteRules.Validate`, the disposition checked before numbering, then a `Credit` adjustment naming the credit note, `RefundAsync`, or `ICustomerCreditService.AddAsync` keyed `CreditNote:{id}`; a refusal cancels the session so no number is used). `InvoiceCalculation` now takes any `InvoicingDocument`. Admin: the invoice page's Credit notes card (`InvoiceCreditNotes.DetailAdmin`, New credit note while something is creditable and the user may issue credit notes), `CreditNotesAdminController` (Create from the invoice, Edit with reason, disposition, notes and each line's quantity, price, discount or Remove; Update, Save draft, Save and issue; Detail; Issue; Delete draft), the shared `_DocumentLines` partial for the lines and totals of both documents, and `InvoiceDraftEditor.BuildLines`/`ApplyLines` shared by both editors (a credit note row keeps the credited line's text). Tests: the invoice commands now assert one stored invoice after void, write-off and reinstate (the ledger commits mid-command); 4 ledger mirror tests (payments until paid, a take-back makes it owed again, mirroring again adds each entry once, a payment missed while the invoice was busy is caught up when opened; the A9 void test now keeps the invoice busy to show the ledger decides); 5 credit note tests (a draft credits each line and a second request opens it; issuing on an open invoice lowers what is owed and settles it, once; more than is owed is refused and takes no number; customer credit is kept once with the credit note's reference; a fully credited invoice can't be credited again); the credit note row mapping. 102 Invoicing tests pass. **Browser:** the invoice page caught up an earlier payment and its take-back with their ledger times; a ledger payment showed on the invoice list without opening the invoice (handler path); New credit note, a credit larger than what was owed refused with its message (the draft kept), half an hour credited and issued as CN-1001 (invoice: 224.00 paid, 54.00 credited, 46.00 due; the ledger shows the credit adjustment naming CN-1001), the credit note page, and deleting a draft credit note. **Deviations:** payments are recorded and credit applied on the transaction's page, which the invoice links to, rather than on a second form on the invoice (one form, owned by Transactions); a user with Record invoice payments but not Manage transactions can't reach it yet. **Not checked in the browser:** the Refund and Customer credit dispositions (no payment provider and no Store Credit on the test site; covered by tests for customer credit). **Not done:** voiding an issued credit note (the plan's `Void` status), and a guard so the ledger's Take back on a credit note's adjustment can't leave the credit note issued with nothing applied; the timeline prints the document kind as stored ("CreditNote"); a credit notes list page.
- **A12 delivery:** committed with the subject "Print, archive, send and share invoices". Invoicing.Abstractions (now references BusinessDocuments.Abstractions): `IInvoiceDocumentBuilder`, `IInvoiceDocumentContributor`, `IInvoicePdfService` with `InvoicePdf`, `IInvoiceShareLinkService`, `IInvoiceSender` with `InvoiceSendRequest`/`InvoiceSendResult`, and `InvoicingConstants.BusinessDocumentKinds`, `EmailKinds` (with the template value names) and `RouteNames`. Invoicing.Core: `InvoiceDocumentBuilder` (issuer and bill-to as kept, dates, references, rows, tax breakdown in percent, totals with paid, credited and due once issued, Draft and Void watermarks, contributors, then the kind's template), `InvoicePdfService` and `InvoiceArchiveFileStore` (the archived PDF wins once issued; never replaced; a read failure renders again), `InvoiceArchiveObserver` (archives at issue; credit notes archive in `IssueAsync`), `InvoiceShareLinkService` (`SignedTokenService`, `SiteUrlGenerator`, `ShareLinkDays`), `DefaultInvoiceSender` (issues a draft first only when `IssueOnSend`; refuses void invoices and sites without e-mail; bill-to e-mail and copy addresses by default; PDF attached; share and pay links as values; records `SentUtc`, `Sent` and a history entry), `InvoiceViewTracker`. `AddInvoicingCore()` registers them and `AddSignedLinks()`. Module: the business document kinds *Invoices* and *Credit notes*, the commerce e-mail kind *Invoice sent to its customer*, the archive store (`AddTenantFileStore`, local under App_Data), the admin actions Document, Pdf and Send (credit notes: Document and Pdf), the public `InvoiceController` (`invoices/view/{token}` and `/pdf`, anonymous, issued invoices only, viewed only for users without View invoices), the Send and share card and the credit note's Print card. Tests (7, with a fake PDF renderer, e-mail sender and share links in the harness): the document's rows, totals, due amount and draft watermark; an issued invoice keeps the PDF it was issued with after the look changes; sending e-mails the PDF to the bill-to with the copy addresses and records it; a draft is sent only when sending issues it; nothing is sent without e-mail; a share link names its invoice and a tampered one is refused; the first open is recorded once. 109 Invoicing tests pass. **Browser:** the card on the invoice page; the print page (number, dates, status, lines, totals, paid, credited, amount due); a real 56 KB PDF named INV-2026-1001.pdf; the share link opened as staff (not counted) and anonymously (the site theme page with Pay this invoice, Download PDF and Print, its PDF, and the first open recorded with its local time); a bad token shows that the link no longer works; the send dialog with the bill-to's address and the warning that e-mail isn't set up (the button disabled); Save and issue of a new invoice archived its PDF under `App_Data_Invoicing/Sites/Default/InvoiceArchives/invoices/2026/` and two downloads were identical; the credit note's print page and CN-1001.pdf. **Fixed during the pass:** the archive store first named an Azure configuration section, which logs an error on every site without one; the base module registers a local store now, as the other modules do, and Azure Blob is left to a module of its own. **Not checked in the browser:** an e-mail actually sent (no e-mail provider on the test site; covered by tests), and an issuer block (the test site has no store profile). **Not done:** an Invoicing.Azure module for the archive in Azure Blob Storage; the send dialog does not preview the message; credit notes are not sent by e-mail yet (the `credit-note-issued` kind is declared but not registered or used); documents render in the site's culture, not the bill-to's.

### Still open in PR A

- **TX10 deviation:** only `payment-request` was added. An `invoice` kind in Transactions would name the same message twice; the invoice e-mails belong to Invoicing (`invoice-issued` and the others in §12).
- **TX12/TX13 browser check (A7 pass):** the workflow editor lists all nine transaction activities with their thumbnails, and a task placed on the canvas shows its design view (Done, NotFound, Failed) and its editor. The driver-built transaction detail page was opened in the A10 pass, on the transaction an issued invoice posted.
- **A13 to A16** as listed in §23.
- Docs still describe the old financial number generator in `src/CrestApps.Docs/docs/modules/commerce.md`, `installment-plans.md` and `observability.md`; update them in A16. The building-blocks catalog rows are also A16.

### How to build and test here

- Build from the mapped drive (`W:\` is `C:\Code\CrestApps\CrestApps.OrchardCore\.claude\worktrees`), with the .NET 10 SDK: `global.json` rolls forward to SDK 11, whose new analyzers fail on unrelated code. Switch `"latestMajor"` to `"latestFeature"` for the build and restore it straight after; never commit that change.
- Tests are xUnit v3: run the built test DLL with `-class` or `-namespace` (one filter kind per run). The architecture namespace takes more than ten minutes.

# Invoicing and billing: final report

Branch `ma/invoicing-billing-module-ec1aa5` (worktree `invoicing-billing-module-ec1aa5`), on `main`: the plan,
then one commit per slice, then the fixes the validation passes found. Not pushed; no pull request opened. Each
slice's detail (what was built, the tests, the runs, the browser pass, the deviations and what was not verified) is
in §28 Progress of [invoicing-plan.md](invoicing-plan.md). This page is the summary.

## What is in the branch

- **Stage A, building blocks and the invoice.**
  - Shared building blocks: money format, number series, the shared PDF library, the ledger's payer, part payments
    and reversals, payment terms, business documents and templates, party billing and tax profiles and members,
    commerce e-mails with attachments, customer credit.
  - The invoice itself: the invoicing document model and calculator, and the Invoicing module (store, commands,
    admin, invoice page, credit notes, print, archive, send, share).
  - Around it: customers' invoices and the account page's Invoices section, work order billing, workflows and
    webhooks, recipes and deployment, reports, privacy, and the building-blocks catalog and skill.
- **Stage B.** Estimates (online acceptance, invoicing, part invoicing by percent), recurring plans, staged reminders
  and the saved-card charge service with invoice autopay, late fees as debit notes, deposits, statements, invoicing
  orders paid on account, the commerce API, and billing expenses, charges and time.
- **Stage C.** Usage meters with tiered pricing on recurring plans, renewal invoices, the Exchange Rates building block
  with the issue day's rate on invoices, approvals and revisions, and AI tools.
- **Review 7.**
  1. **One charge-and-retry path (B3).** Installment plans and booking balances are charged by
     `ISavedPaymentMethodChargeService`, with their own schedules as inputs; their own charge loops are gone.
  2. **Per-party exclusion** from reminders and late fees.
  3. **A staff payment schedule** on one invoice.
  4. **B7, the full Orders bridge (decision ID2, as the product owner decided).** With Invoicing on:
     - **Draft orders.** Sending a draft order issues an Invoicing invoice from it: its lines, shipping, discounts,
       bill-to and due date, the order number as reference, `SourceType` Order. The number, PDF and e-mail are
       Invoicing's, and sending again sends the same invoice.
     - **One debt, one payment path.** There is one debt and one way to pay it, the order's own checkout. The
       invoice's pay links, reminders and the ledger's pay page lead to the order's pay page
       (`ITransactionPayUrlProvider`). The order's payment posts to the ledger transaction the invoice owns (the
       option I picked), so the order has no debt of its own.
     - **Shipping and tax added at checkout** go on a debit note of the invoice, paid by the same payment.
     - **Cancel and refund.** Canceling the order voids the unpaid invoice. Refunding it credits the invoice and its
       debit notes with **Refunded elsewhere** credit notes, and the ledger splits the refund over the debts the one
       payment settled.
     - **Quotes.** With Invoicing Estimates on, a quote is an Invoicing estimate. Accepting it on the estimate's page
       sends the buyer to the order's checkout. Once the order is paid, the estimate shows **Invoiced**. Declining
       and expiry carry over both ways.
     - **The seams.** Orders gains `IOrderDocumentSender` in Orders.Core and still references nothing of Invoicing.
       The bridge lives in Invoicing's `[RequireFeatures]` Orders startup.
     - **With a feature off.** Without Invoicing, or without Estimates for quotes, the order's own invoice and quote
       are unchanged.
- **Review 8, the bridge's open items.**
  1. **Charged less at the checkout.** A coupon at the order's checkout, or any lower total, is put on a credit note
     that lowers the invoice. The discount is spread over the lines it applies to, each at its line's taxes, so the
     tax is split by rate. Gift cards and store credit are recorded as payments. The invoice settles.
  2. **Part refunds with their tax.** A part refund is credited with the tax from the payment's tax snapshot
     (`ITaxRefundCalculator`), one line per set of rates, still **Refunded elsewhere**. A full refund credits line for
     line. Credit notes now keep the taxes their lines carry (`DocumentCalculationContext.KeepTypedTaxes`) instead of
     today's rules.
  3. **Paying several balances.** The ledger's checkout refuses a debt paid through a page of its own. **My invoices**
     gives an order's invoice its own **Pay** button to the order's checkout, with a note, and leaves it out of a
     combined payment.
  4. **What the order page says.** *Invoice INV-… emailed to …*, or *made … but not emailed* when e-mail isn't set up
     (and the same for a quote's estimate).

## Final validation (on the final tree)

| Check | Result |
| --- | --- |
| Clean Release build of `CrestApps.OrchardCore.slnx` (`--no-incremental`, warnings as errors, analyzers on) | 0 warnings, 0 errors |
| The whole test project | 16,299 tests: 0 failed, 1 skipped (a live Asterisk test) |
| Architecture namespace | 315 passed |
| PublicApi tests | 69 passed |
| JS lint and unit tests (`npx vitest run`) | 90 files, 1,475 tests, all passing |
| Docs site (`npm run build`) | Built; Docusaurus warns only on two pages this branch does not touch |
| `npm run check:manuals` | 161 User Manual pages, 189 Technical Manual pages: passed |
| End-to-end browser pass on a third fresh site, List then Grid layout | Passed; server log read after the passes: no warning or error |

The final pass (`App_Data_InvoicingFinal3`, all features on, Taxation included) covered:

- all 94 admin menu pages in both layouts, all 42 lists as lists and then as grids with row menus;
- an invoice issued, printed as PDF and paid from its share link with the test card;
- a credit note on a second invoice, whose rest was then recorded as a check payment;
- a draft order sent as an invoice ("made but not emailed" on the order page), shown on **My invoices** with its own
  **Pay** button, and paid at the order's checkout with a coupon and Express shipping: the coupon on a credit note, the
  shipping on a debit note, all settled;
- a part refund of that order, credited on the invoice, which stays paid;
- a quote sent as an estimate, accepted on its page, paid at the order's checkout, and shown as **Invoiced**.

The review 8 browser check, on the second fresh site, also covered a combined payment that left an order's invoice out.

The earlier validation pass, before B7, covered the rest of the module on the first fresh site: credit notes,
estimates, approvals, exchange rates, payment schedules, recurring catch-up, late fees, usage meters and statements.
It is in the Progress entry "Validation pass before B7".

## Found by the validation passes and fixed

1. **Before B7.**
   - Issuing an invoice failed on a fresh site: the PDF feature was not a dependency.
   - Four catalogs registered their rules outside the feature that imports them.
   - A recurring plan with nothing to bill could be started.
   - The Statements form broke the action-bar lint.
2. **The Invoice button of a sent draft order returned 404.** The order's invoice actions refused a draft before
   asking for the Invoicing invoice. They now open the invoice first.
3. **Refunding an order whose checkout added shipping** recorded the whole refund on the invoice's debt and left the
   debit note's debt paid. The bridge's credit note was then refused, so the invoice read **Open, Unpaid** and would
   have been chased. Two fixes:
   - The ledger now splits such a refund over the debts of that checkout that took a share of the payment
     (`ITransactionStore.GetByCheckoutSessionAsync`).
   - The bridge credits the invoice, then its debit notes.

   There is a test for it, written first, which fails without the split.
4. **Sending a draft order with Taxation on returned 500** (found in the review 8 browser check). A draft order made
   in the admin has no currency, and the tax service refuses a document without one. The invoice now takes the
   site's default currency, and the tax provider leaves such a document to its typed taxes. Its test fails without
   the fix with the site's error.

## Open, for the product owner

- **Tax by rate not seen in the browser.** The EU VAT sample rules didn't make the order's checkout charge VAT on the
  test site in the time given. The split by rate is covered by the tests.
- **Discounts on the invoice of an order paid on account, or of a paid quote.** It is built from the paid order with
  its discounts as lines out of scope of tax (the builder accepted earlier), so its tax is the lines' before the
  discount.
- **Draft orders without Invoicing.** The order page still shows **Invoice** on a draft order, and it returns 404.
  This is older than this branch.

## Not verified

- E-mail: there is no e-mail service on the test sites, so sends fall back to the share link.
- A charged staff payment schedule: no wallet card on the test sites. Covered by the tests.
- Bookings in the browser: no bookable product. Covered by the tests.
- A second approver approving on the fresh site. Seen on the working site in the C6 pass.
- A part refund with tax, in the browser (no VAT was charged on the test site).

## Test sites

- `App_Data_Invoicing` (http://invoicing.localhost:5482): admin / Password1!, and `approver` / Password1!.
- `App_Data_InvoicingFinal` (http://final.localhost:5492): the first fresh site, used for the pass before B7 and
  B7's own browser pass. Admin / Password1!.
- `App_Data_InvoicingFinal2` (http://final2.localhost:5502): the final pass. Admin / Password1!. INV-1002 and its
  order keep the state the refund defect left before the fix.
- `App_Data_InvoicingFinal3` (http://final3.localhost:5512): the final validation pass. Admin / Password1!.

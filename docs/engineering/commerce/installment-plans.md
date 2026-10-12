# Installment plans (admin-created payment plans)

Status: in progress on `ma/installment-plans` (based on `ma/subscribtions`).

## Goal

An administrator sells something on a payment plan without the customer going through the storefront:

1. Pick the customer (an existing user, or a new one created from a name and an email).
2. Enter the total, the down payment collected now, the number of later payments, how often they fall due
   (weekly, every two weeks, monthly, quarterly) and the first due date.
3. Choose how the later payments are collected: **charge the saved card automatically** on each due date, or
   **invoice the customer**, who pays each one from **My Transactions**.
4. Take the down payment by entering the customer's card on the admin screen (a phone or in-person order). The
   card is saved to the customer at the gateway so the later payments can be charged without them present.

After that the plan runs itself: reminders go out before each due date, due payments are charged (or invoiced),
failed charges are retried and then fall back to an invoice, a receipt is sent after every payment, and the
admin sees what is paid, what is outstanding and what failed.

## Design in one paragraph

A plan is a durable `InstallmentPlan` record that owns a schedule. **Every payment in the schedule is an ordinary
`Transaction`** (`Source = "installment-plan"`, `ReferenceType = "InstallmentPlan"`, `ReferenceId = plan id`), so
the ledger, the Transactions admin screens, **My Transactions** with its **Pay** action, offline recording, the
overdue reminders and refunds all work on installments without knowing about plans. **All money moves through the
checkout engine**: the down payment and every automatic charge are checkout sessions that settle a transaction
(`ReferenceType = "Transaction"`), so attempts, verification, the reconciliation sweep and refunds are the same as
for any other payment. The plan reacts to payments through a new provider-neutral hook on Transactions, so a
payment recorded online, by the sweep or by hand all move the plan the same way.

## Framework changes (reusable, not plan-specific)

### Checkout

| Change | Why |
| --- | --- |
| `StartCheckoutRequest.OwnerId` | The session belongs to the customer, not the admin who started it, and a background charge has no request to read a user from. When set, `CheckoutSessionStore` stamps that owner and does not touch `HttpContext`. |
| `PaymentBeginOutcome.ProviderErrorMessage` | A declined card has to be shown to the admin (and recorded on the installment) as the gateway worded it; today the engine logs it and returns a generic message. |
| `PaymentProviderCapabilities.SupportsSavedPaymentMethods` + `ICheckoutSavedPaymentMethodProvider` + `SavedPaymentMethod` | Provider-neutral contract for "save this card while charging it" and "charge the saved card off-session". The plan stores the `SavedPaymentMethod` (provider key, customer reference, payment-method reference, brand, last four, expiry); only the provider interprets the references. |
| `CheckoutPaymentDataKeys` (`savePaymentMethod`, `offSession`, `savedCustomerReference`, `savedPaymentMethodReference`) | The provider-data keys a caller uses to ask for either behavior. |
| `BillingItem.ExcludeFromTax` / `CheckoutLineItem.ExcludeFromTax` | **Fixes an existing defect**: settling a transaction through checkout taxed it again, although the transaction already carries its tax. Transaction settlement now marks its line as excluded. |
| `CheckoutFlowPaymentMethod.SavePaymentMethod` | Lets a page ask a provider panel to tokenize a reusable payment method even when nothing recurring is on the invoice. |
| `checkout-payment.js` `requestHeaders` option | Lets an admin page send the antiforgery token with the begin/status calls. |

### Stripe

- `BeginAsync` with `savePaymentMethod`: requires the browser-tokenized `paymentMethodId`, resolves (creates) the
  Stripe customer with that payment method attached, and creates the PaymentIntent with
  `setup_future_usage = off_session` for that customer and payment method. The browser confirms it as today.
- `BeginAsync` with `offSession`: creates and confirms the PaymentIntent server-side with `off_session = true` for
  the saved customer and payment method. A decline is returned as a failure carrying Stripe's message; a success
  returns a reference with no client action.
- `StripeCheckoutPaymentProvider : ICheckoutSavedPaymentMethodProvider` reads the PaymentIntent back and describes
  the card that was saved (customer, payment method, brand, last four, expiry).
- `PaymentIntentDetails` gains `CustomerId`, `PaymentMethodId`, `LastPaymentErrorMessage`.

### Transactions

- `ITransactionHandler.PaymentRecordedAsync(TransactionPaymentRecordedContext)` raised after a payment is applied:
  online settlement (checkout), admin **Record payment** and **Mark paid**. Context carries the transaction, the
  amount, the method (online/offline), the payment attempt id (online) and the event id.
- `TransactionEvent` gains `Id` and `Amount` so each payment on a transaction is addressable (receipts).
- **Upcoming reminders**: `TransactionReminderSettings.UpcomingReminderDays` (default 3, 0 = off). The reminder
  sweep also sends one "payment due on …" reminder for `Pending`/`Outstanding` transactions whose `DueUtc` falls
  inside the window. Recorded on `Transaction.UpcomingReminderSentUtc`. A transaction that will be charged
  automatically (`TransactionAutoCollection` metadata) is worded as "will be charged to Visa •••• 4242 on …".
- **Payment receipts** feature (`CrestApps.OrchardCore.Transactions.Receipts`, depends on Transactions + Receipts):
  on `PaymentRecordedAsync` it builds a receipt with `IReceiptService` and sends it to the customer (notification
  for a user, email for a guest, HTML body). Printable receipt pages: customer (`my-transactions/receipt/...`) and
  admin (`transactions/receipt/...`), both rendering the existing `ReceiptDocument` view.

## The plan feature

Feature `CrestApps.OrchardCore.Subscriptions.Installments` ("Installment Plans") in the Subscriptions module.
Depends on Transactions, Checkout, CrestApps Users (picker) and OrchardCore.Users. Permission
`ManageInstallmentPlans`. Admin menu: **Subscriptions → Installment Plans**.

### Model

`InstallmentPlan : CatalogItem` — Title, OwnerId, CustomerName, CustomerEmail, Currency, TotalAmount,
DownPaymentAmount, InstallmentCount, Frequency, FirstDueUtc, CollectionMethod (`AutoCharge`/`Invoice`), Status
(`Draft`, `Active`, `PastDue`, `Completed`, `Canceled`), SavedPaymentMethod, DownPaymentTransactionId,
DownPaymentSessionId, `IList<InstallmentPlanPayment> Payments` (number, due date, amount, transaction id, charge
attempts, last attempt, next attempt, last failure), `IList<InstallmentPlanEvent> Events`, Notes, CreatedById,
CreatedByName, CreatedUtc, UpdatedUtc, ActivatedUtc, CompletedUtc, CanceledUtc. Stored with
`DocumentCatalog<InstallmentPlan, InstallmentPlanIndex>` (collection `InstallmentPlan`), concurrency checked.

`InstallmentScheduleCalculator` (pure): splits `Total − DownPayment` into N payments at the currency's precision;
the rounding remainder goes on the last payment so the schedule always sums to the total. Due dates step from the
first due date by the frequency (month steps clamp to the end of short months from the original day).

Settings `InstallmentPlanSettings`: `RetryDays` (default `1,3,5` — days after a failed charge to retry), default
collection method. Settings → Subscriptions → Installment plans.

### Flow

1. **Create** (`installment-plans/create`): validates the inputs and the schedule, resolves or creates the customer,
   and creates in one unit of work: the `Draft` plan, the down-payment `Transaction` (`Outstanding`, due now) and a
   checkout session settling it (owner = customer, contact = customer). Redirects to **Collect down payment**.
2. **Collect down payment** (`installment-plans/pay/{id}`): the eligible providers' panels (providers with
   `SupportsSavedPaymentMethods` and embedded elements), driven by `checkout-payment.js` against two admin JSON
   endpoints (`installment-plans/{id}/payment/begin|status`) that require `ManageInstallmentPlans`, same origin
   and the antiforgery header. Begin forces `savePaymentMethod` when the plan charges automatically.
3. **Activation**: on `PaymentRecordedAsync` for the down-payment transaction (or, as a backstop, the plan sweep
   seeing it paid), under a per-plan lock: read the saved payment method from the attempt, create the N payment
   transactions (`Pending`, `DueUtc` from the schedule, `TransactionAutoCollection` when auto-charged), set the
   plan `Active`. Idempotent.
4. **Sweep** (`InstallmentPlanBackgroundTask`, every 15 minutes, per-plan lock):
   - activate drafts whose down payment is paid;
   - invoice mode: `Pending` payments that are due become `Outstanding` (the overdue reminders take over);
   - auto-charge mode: due payments are charged through the engine (`offSession`); a decline records the failure,
     schedules the next retry from `RetryDays`, and notifies the customer; after the last retry the payment
     becomes `Outstanding` (customer can pay from **My Transactions**) and the plan `PastDue`;
   - recompute status: all paid → `Completed`; any failed/overdue → `PastDue`; otherwise `Active`.
5. **Admin detail** (`installment-plans/detail/{id}`): summary (paid, outstanding, next due, card on file),
   schedule table with each payment's state and links to its transaction, actions: **Charge now** (auto-charge),
   **Cancel plan** (cancels unpaid future transactions), and the history.
6. **Customer**: every payment shows in **My Transactions** with **Pay**; reminders before each due date; a
   receipt after each payment.

### Guard rails

- Money never moves outside the engine; a payment is never charged twice: per-payment lock, a paid transaction is
  never charged, and a session still awaiting the provider for that transaction is completed rather than replaced.
- The down payment must be greater than zero and less than the total; at least one later payment; total and
  amounts at currency precision; first due date in the future.
- A new customer is created disabled for nothing: an enabled account with an unusable random password (the
  customer sets one with **Forgot password**); the email must be unused.

## Tests

Calculator (rounding, month-end, frequencies); Stripe provider save/off-session/decline branches with fakes;
checkout owner override and provider error propagation; tax exclusion of settlement lines; upcoming reminder
cadence and wording; payment-recorded hook from settlement and admin actions; receipt handler; plan service
create/activate/charge/retry/fallback/complete/cancel with in-memory stores and a fake engine; admin controller
authorization; module wiring. Live: the whole flow against Stripe test mode with the Stripe CLI forwarding
webhooks.

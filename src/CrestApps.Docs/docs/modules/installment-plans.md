---
sidebar_label: Installment Plans
title: Installment Plans
description: How installment plans are configured, how a plan collects its payments through the checkout and the transaction ledger, how a module ties a plan to its own record, how customers choose installments at checkout, how double charges are prevented, and how to extend the feature.
user_manual:
  - user-manual/commerce/installment-plans
  - user-manual/use-cases/sell-on-a-payment-plan
---

| | |
| --- | --- |
| **Feature Name** | Subscriptions - Installment Plans |
| **Feature ID** | `CrestApps.OrchardCore.Subscriptions.Installments` |
| **Module** | Subscriptions |
| **Dependencies** | `CrestApps.OrchardCore.Transactions`, `CrestApps.OrchardCore.Checkout`, `OrchardCore.Users` |

**Subscriptions - Installment Plans** lets a balance be paid in installments. A plan is created in one of three ways:

- **By an administrator**, who sells something on a payment plan without the customer going through the storefront, for example over the phone or at a counter. The administrator enters the customer, the total, a down payment (or none) and a schedule. The first payment is taken by card on the spot.
- **By another module**, for a record of its own, through `IInstallmentPlanService.CreateAsync` with a `ReferenceType` and `ReferenceId` (for example a registration). Every payment of the plan carries that reference, so the module finds its record from any payment.
- **By the customer at checkout**, who chooses on the **Choose how to pay** step to pay an outstanding transaction in installments instead of in full. See [Installments at checkout](#installments-at-checkout).

The remaining balance is collected in a fixed number of equal payments, either charged to the kept card on each due date or invoiced to the customer.

The feature lives in the Subscriptions module but does not depend on the **Subscriptions** feature or on **Products**: a site that only lets customers pay balances in installments enables **Subscriptions - Installment Plans** with Transactions and Checkout alone. When Products is enabled, the admin form offers its managed currencies in a list; without it, the currency code is typed.

The feature owns no payment code. Every payment on a plan is an ordinary [transaction](transactions), and every charge goes through the [Checkout](checkout) engine. So plan payments are verified against the gateway, recorded on the durable payment ledger, receipted, reminded and refundable exactly like any other payment.

For the admin screens, see the User Manual page [Installment Plans](../user-manual/commerce/installment-plans.md).

<YouTube video="commerce" title="Narrated video of setting up an installment plan and taking the down payment" start={304} end={363} />

## Requirements

| Requirement | Why |
| --- | --- |
| A payment provider that can **keep a card** and show its card form **on the page** | The down payment is taken on the plan's own admin page, and the card is kept for the schedule. In the checkout's terms, the provider sets both `PaymentProviderCapabilities.SupportsSavedPaymentMethods` and `SupportsEmbeddedElements` and registers an `ICheckoutSavedPaymentMethodProvider`. The [Stripe](payments) provider does all three. Without one, a new plan is refused with a message saying so. |
| **Transactions** | Each payment is a transaction with the source `installment-plan`. |
| **Transaction Reminders** (optional) | Tells the customer before each payment falls due and chases overdue ones. Invoiced payments rely on it: it sends the invoice and its pay link. See [Reminders](transactions#reminders). |
| **Send Payment Receipts** (optional) | Sends a receipt after every payment and a notice after every refund. Printable receipts and invoices need only the Receipts module, which Subscriptions already enables. See [Payment receipts](transactions#payment-receipts). |
| A public **Base URL** (**Settings > General**) | Pay links in reminders are absolute URLs built from it. When it is empty, a link made outside a request (by the background task) is left out. |
| Background tasks running | The schedule is driven by a background task. See [The background task](#the-background-task). |

## Configuration

The settings are site settings, edited on **Settings > Installment Plans** (site settings group `installment-plans`). Editing them requires the *Manage installment plans and charge customers' saved cards* permission. The *Manage subscriptions settings* permission also still lets a user edit them, because they used to be a card on **Settings > Subscriptions**.

| Setting | Property | Default | Meaning |
| --- | --- | --- | --- |
| Collect scheduled payments by | `DefaultCollectionMethod` | `0` (charge the saved card) | What a new plan preselects. `0` charges the kept card on each due date; `1` invoices the customer. The administrator can change it for each plan. |
| Retry a declined card after (days) | `RetryDays` | `[1, 3, 5]` | The number of days after each declined automatic charge before the next try, in order. After the last retry, the payment is handed to the customer. An empty list hands a declined payment to the customer straight away. |
| Offer for balances from | `CheckoutSources` | `[]` | The transaction sources (`Transaction.Source`) whose balances the built-in option provider lets a customer split at checkout, for example `pay-later`. Empty offers nothing from these settings. |
| Numbers of payments offered | `CheckoutInstallmentCounts` | `[]` | The choices the built-in provider offers, for example `[3, 6]`. Up to six numbers between 2 and 120. |
| Payments fall due | `CheckoutFrequency` | `2` (monthly) | How often the payments chosen at checkout fall due: `0` weekly, `1` every two weeks, `2` monthly, `3` every three months. |
| Smallest balance | `CheckoutMinimumAmount` | `0` | Balances below this amount are only offered **Pay in full**. |

The settings are stored in the site document as `InstallmentPlanSettings`, so a recipe sets them with the standard `Settings` step:

```json
{
  "steps": [
    {
      "name": "Settings",
      "InstallmentPlanSettings": {
        "DefaultCollectionMethod": 1,
        "RetryDays": [ 2, 4 ],
        "CheckoutSources": [ "pay-later" ],
        "CheckoutInstallmentCounts": [ 3, 6 ],
        "CheckoutFrequency": 2,
        "CheckoutMinimumAmount": 100
      }
    }
  ]
}
```

They are not read from `appsettings.json`.

### Permissions

| Permission | Display name | Default roles |
| --- | --- | --- |
| `ManageInstallmentPlans` | Manage installment plans and charge customers' saved cards | Administrator |

The permission is deliberately separate from the other subscription permissions, because whoever holds it can charge a customer's kept card with **Charge now**.

## How a plan runs

<YouTube video="commerce" title="Narrated video of managing installment plans: Charge now, Cancel the plan, and the list" start={362} end={409} />

### Creating a plan

`IInstallmentPlanService.CreateAsync` validates the request and creates three things in one unit of work:

1. **The customer.** An existing user, or a new user created from a name and an email. A new user gets a unique user name derived from the email and **no password**; they set one with **Forgot password** when they want to sign in. An email that already belongs to a user is refused, so the administrator picks that user instead.
2. **The plan**, in the `Draft` status, with its full schedule. Payment `0` is the down payment, due now; payments `1` to `n` are the scheduled ones. A plan without a down payment has no payment `0`: payment `1` is due now and is taken in its place (`InstallmentPlan.FirstPayment` returns whichever comes first).
3. **The first-payment transaction** (`Outstanding`) and the **checkout session** that pays it, referenced to that transaction.

The validation rules are:

- the down payment is zero or more, and less than the total;
- there are 1 to 120 later payments;
- with a down payment, the first scheduled payment falls due after today, because the down payment is what is collected today;
- without a down payment, `FirstDueUtc` is left unset or is today: the first payment is due now, and the schedule runs from now;
- `ReferenceType` and `ReferenceId` are both set or both empty;
- the balance splits into payments that are all above zero at the currency's precision.

`ReferenceType`, `ReferenceId` and `Properties` on the request are copied onto the plan. Every transaction of the plan gets `ParentReferenceType` and `ParentReferenceId` from the plan's reference, and a copy of its `Properties` (see [Linking a plan to your own record](#linking-a-plan-to-your-own-record)).

### The schedule

`InstallmentScheduleCalculator` splits the balance (the total minus the down payment) into equal payments rounded to the currency's minor unit. The **last payment absorbs the rounding**, so the schedule always adds up to the total exactly.

| Frequency | Value | Due dates |
| --- | --- | --- |
| Weekly | `Weekly` (0) | Every 7 days from the first due date. |
| Every two weeks | `BiWeekly` (1) | Every 14 days. |
| Monthly | `Monthly` (2) | Counted from the first due date, so a plan that starts on the 31st falls on the last day of a short month and returns to the 31st afterwards. |
| Quarterly | `Quarterly` (3) | Every three months, counted the same way. |

### Taking the first payment

The plan's payment page shows the provider's embedded card form. With Stripe, it also shows Apple Pay or Google Pay where the device offers them. The page begins the payment on the plan's checkout session through the checkout engine, by posting to two JSON endpoints under the admin prefix: `installment-plans/BeginPayment/{itemId}` (route `InstallmentPlansBeginPayment`) and `installment-plans/PaymentStatus/{itemId}` (route `InstallmentPlansPaymentStatus`), both minimal APIs that need a signed-in user with *Manage installment plans and charge customers' saved cards* and the page's antiforgery token in the `RequestVerificationToken` header.

- **Keeping the card.** When the plan charges the card, the server adds `CheckoutPaymentDataKeys.SavePaymentMethod`, so the provider keeps the card for later use.
- **What the browser cannot do.** The browser cannot set that key. The controller strips every saved-card key (`savePaymentMethod`, `offSession`, `savedCustomerReference`, `savedPaymentMethodReference`) from what the browser sends, and bounds the size of the rest.

Once the checkout settles the down-payment transaction, the Transactions feature raises `ITransactionPaymentHandler.PaymentRecordedAsync`. The feature's `InstallmentPlanTransactionPaymentHandler` reacts to transactions whose `ReferenceType` is `InstallmentPlan`. It calls `ProcessAsync`, which **activates** the plan:

1. It reads the kept card back from the provider (`ICheckoutSavedPaymentMethodProvider.GetSavedPaymentMethodAsync`). It stores the card's brand, last four digits and expiry on the plan as a `SavedPaymentMethod`. Card numbers never reach the site; only the gateway's references are kept.
2. It creates a `Pending` transaction for every scheduled payment. When the plan charges the card, each transaction gets a `TransactionAutoCollection` naming the card.
3. It moves the plan to `Active`.

If the provider could not keep the card, an auto-charge plan switches itself to invoicing and records why in its history. That is better than a plan that never charges anybody.

Nothing is scheduled until the first payment (the down payment, or payment `1` of a plan without one) is received:

- A `Draft` plan an administrator created whose first payment is never taken stays a draft.
- A `Draft` plan a customer chose at checkout is closed by `ProcessAsync` once its checkout is canceled, expired or failed, or once the transaction it replaces was settled another way. See [Installments at checkout](#installments-at-checkout).
- Canceling a draft closes its checkout session.

### Collecting the scheduled payments

`ProcessAsync` brings a plan up to date with its transactions and the clock. For each payment that is due:

| Collection method | On the due date |
| --- | --- |
| Charge the saved card (`AutoCharge`) | The kept card is charged off-session through a new checkout session. The provider data is `CheckoutPaymentDataKeys.ForOffSessionCharge(plan.PaymentMethod)`. |
| Invoice the customer (`Invoice`) | The transaction becomes `Outstanding`. It then appears in the customer's **My Transactions** and gets an invoice number, and the reminders send a pay link. |

A payment can also be paid another way:

- the customer pays it early from **My Transactions** or a pay link;
- an administrator records it in the Transactions console.

Either way it is picked up from its transaction, so it is never charged again.

### Declines and retries

| What happened | Result |
| --- | --- |
| The gateway could not be reached, or refused to start | Nothing is known about the card. The charge is tried again an hour later and does not use up a retry. |
| The card was declined, and retries are left | The payment is `Failed`, and so is its transaction (`TransactionStatus.Failed`, still collectable). The next try is scheduled from `RetryDays`, and the customer is told *We could not take your payment*. A retry that goes through settles the transaction. |
| The card was declined on the last retry | The payment is handed to the customer: its transaction becomes `Outstanding` with no automatic collection. The customer is told *Your payment is overdue*, and the overdue reminders take over. It is not charged again unless an administrator clicks **Charge now**. |
| An administrator's **Charge now** on or after the due date was declined | The payment and its transaction are `Failed`. No automatic retry is scheduled. |
| An administrator's **Charge now** before the due date was declined | The payment stays `Scheduled`, and it is still charged (with its retries) from its due date. An early charge is a favor, not a deadline. |

### Statuses

| Plan status | Meaning |
| --- | --- |
| `Draft` | Created, waiting for the down payment. |
| `Active` | The down payment was received and the scheduled payments are being collected. |
| `PastDue` | A payment is overdue or could not be charged. |
| `Completed` | Every payment was received. |
| `Canceled` | Canceled; payments not yet received are no longer collected. |

| Payment status | Meaning |
| --- | --- |
| `Scheduled` | Not yet due. |
| `Due` | Due, and waiting to be charged or paid by the customer. |
| `Paid` | Received. |
| `Failed` | Charging the card failed. It is retried, or left for the customer. |
| `Canceled` | No longer collected because the plan was canceled. |

Every change is recorded in the plan's history as an `InstallmentPlanEvent`, with the acting administrator where there is one. The event types are `Created`, `Activated`, `PaymentReceived`, `ChargeFailed`, `PaymentDue`, `Completed`, `Canceled` and `Note`.

### Canceling

`CancelAsync` keeps the payments already received:

- It cancels every payment not yet received, together with its transaction.
- It closes a draft's open checkout session.
- It tells a customer whose plan had started (any status but `Draft`) *Your payment plan was canceled*.

Money already collected is refunded separately, from **Commerce > Payments**.

## Installments at checkout

A checkout that pays an outstanding transaction (`ReferenceType` `Transaction`) can offer a **Choose how to pay** step: **Pay in full**, or one of the installment options offered for that transaction. Choosing an option turns the transaction into a plan while the customer is present, takes the first payment in the same checkout, and keeps the card for the rest, which the background task then charges on their due dates like any other plan.

### Where the options come from

Options come from `IInstallmentOptionProvider` implementations, one per transaction source:

```csharp
public interface IInstallmentOptionProvider
{
    // Matched against Transaction.Source, ignoring case. Null or empty asks the provider about every transaction.
    string Source { get; }

    Task<IEnumerable<InstallmentOption>> GetOptionsAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
```

An `InstallmentOption` has an `Id` (what the form posts, at most 64 characters, never `full`), an optional `Title`, `InstallmentCount`, `Frequency`, `DownPaymentAmount` (zero, the default, means the first installment is taken now) and `Properties` copied onto the plan. The amounts are always the transaction's own outstanding amount and currency; an option only says how it is split.

The feature ships `SiteSettingsInstallmentOptionProvider`, which offers the counts in `CheckoutInstallmentCounts` for the sources in `CheckoutSources` (see [Configuration](#configuration)). Register your own for your source:

```csharp
public sealed class SeasonFeeInstallmentOptionProvider : IInstallmentOptionProvider
{
    public string Source => "club-registration";

    public Task<IEnumerable<InstallmentOption>> GetOptionsAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<InstallmentOption>>(
        [
            new InstallmentOption
            {
                Id = "3-monthly",
                Title = "3 monthly payments",
                InstallmentCount = 3,
                Frequency = InstallmentFrequency.Monthly,
            },
        ]);
}

// In a startup that requires CrestApps.OrchardCore.Subscriptions.Installments:
services.AddScoped<IInstallmentOptionProvider, SeasonFeeInstallmentOptionProvider>();
```

`InstallmentOptionResolver` collects the options and keeps only the ones the plan service accepts. It offers nothing unless:

- the transaction can be split (`InstallmentTransactionEligibility.CanSplit`): it is `Outstanding` with nothing paid yet, it belongs to an authenticated owner (`CustomerOwnerKind.Authenticated`, which includes paying it from a pay link), it has a currency, and it is not a plan payment, a recurring charge or a balance with automatic collection;
- a payment provider that can keep a card and show its form on the page is enabled (the same test as for administrator plans).

A provider that throws is logged and skipped; the others still offer theirs.

### The step

`InstallmentOptionsCheckoutHandler` adds the step (key `SubscriptionConstants.InstallmentPlans.CheckoutStepKey`, `ChooseHowToPay`, order `10`, `CollectData = true`) when the checkout is activated and at least one option is offered. The step sits after the concealed **Outstanding payment** step (`TransactionsConstants.SettlementStepKey`, order `0`) and before **Payment**, and because it collects data the payment step can't be reached until the customer has chosen.

`InstallmentOptionsCheckoutFlowDisplayDriver` renders the step (shape `InstallmentOptionsStep`) and handles the post. Only the option id is posted. It is matched against the options read again on the server (`InstallmentOptionResolver.FindOptionAsync`), so a tampered form can only choose an option that is really offered. The choice is saved in `SavedSteps["ChooseHowToPay"]` as `{ "optionId": "...", "planId": "..." }`; once a plan is made, posting the step again keeps it rather than making a second one.

### Turning the transaction into a plan

`IInstallmentPlanService.CreateFromTransactionAsync(CreateInstallmentPlanFromTransactionRequest)` takes the `TransactionId`, the `CheckoutSessionId` the customer is paying in, and the chosen `Option`. It refuses when the transaction can't be split, when the checkout is no longer `Pending`, no longer references the transaction or already has payment attempts, or when an active, past-due or completed plan already replaces the transaction. A draft plan left by an earlier abandoned checkout for the same transaction is canceled first.

The plan it creates:

- belongs to the transaction's owner, has the transaction's title, currency and outstanding amount, and collects by charging the saved card;
- pays for the transaction's parent reference when it has one, otherwise for the transaction's own reference, and carries the transaction's `Properties` merged with the option's;
- records the transaction as `SourceTransactionId`.

Its first payment is a `Pending` transaction (not `Outstanding`: it is being paid right now, and is not reminded if the customer walks away). The customer's checkout is then moved onto it:

- `CheckoutSession.ReferenceId` becomes the first payment's transaction, and the settlement step's billing item becomes its amount;
- `CheckoutSession.SavePaymentMethod` is set;
- the invoice is rebuilt, so the customer is shown, and charged, the first payment only.

The original transaction stays `Outstanding` until the first payment is received. When it is, activation keeps the card from that checkout's attempt, schedules the remaining payments with automatic collection, and cancels the original transaction with a timeline entry naming the plan. If the original transaction was settled some other way in the meantime, it is left as it is and the plan's history asks a person to check.

### Keeping the card in a public checkout

`CheckoutSession.SavePaymentMethod` is set only by server code. When it is set:

- the payment step offers only providers with `PaymentProviderCapabilities.SupportsSavedPaymentMethods` (`CheckoutPaymentMethodSelector.GetEligible(..., savesPaymentMethod: true)`) and tells the customer the card is saved for the remaining payments;
- each provider panel is rendered with `CheckoutFlowPaymentMethod.SavePaymentMethod`, so Stripe tokenizes a reusable card;
- `ICheckoutEngine.BeginPaymentAsync` refuses a provider that can't keep a card, and adds `CheckoutPaymentDataKeys.SavePaymentMethod` to the provider data itself.

The public begin-payment endpoint (`checkout/{sessionId}/payment/begin`) drops `savePaymentMethod`, `offSession`, `savedCustomerReference` and `savedPaymentMethodReference` from what the browser sends, so a browser can neither decide that a card is kept nor spend a card kept earlier.

## Payment safety

A payment plan charges a card without the customer present, so the feature is built never to take money twice:

- **One writer per plan.** Every change to a plan runs under the distributed lock `INSTALLMENT_PLAN_{planId}`.
  - The background task and **Charge now** wait for the lock, for up to 10 seconds.
  - The payment handler does not wait. If the plan is locked, the plan is collecting that very payment, and it reads the result itself when the charge returns.
  - On deployments with several nodes, enable a distributed lock provider such as Redis.
- **Money taken is applied before charging again.** The checkout's payment attempts are committed before anything that records the payment elsewhere.
  - Before each charge, every succeeded attempt for the transaction (`IPaymentAttemptStore.GetByReferenceAsync`) that is not yet on the transaction is applied through `ITransactionSettlementService`.
  - So a charge the gateway accepted counts even when its recording was interrupted by a crash, a timeout or a lost response. The payment is not charged again because of a balance that was never recorded.
- **A charge still settling is finished, not repeated.**
  - When the payment's previous session is `AwaitingProvider` or `PaymentPending`, the plan completes it instead of starting a new one.
  - A session that never reached the gateway is canceled, so it cannot be paid later by accident.
- **Off-session charges are server-only.** Only the server sets the saved-card keys, so nothing the browser sends can spend a kept card.
- **Idempotent gateway calls.** The Stripe provider sends deterministic idempotency keys. See [Payment resiliency](payments#payment-resiliency).

## The background task

`InstallmentPlanBackgroundTask` runs every 15 minutes (`*/15 * * * *`):

- It loads the open plans with `IInstallmentPlanStore.GetOpenAsync`. Those are the `Draft`, `Active` and `PastDue` plans.
- It calls `ProcessAsync` on each one. When a plan fails, it logs the error and continues with the next.

So a payment is charged within 15 minutes after its due time (00:00 UTC on the due date), and a retry within 15 minutes of its retry time.

Like every Orchard Core background task, its schedule can be changed, or the task disabled, for each tenant under **Configuration > Tasks**. Disabling it stops all scheduled collection.

## Customer notices

| Notice | When | Sent by |
| --- | --- | --- |
| *We could not take your payment* | A scheduled charge was declined and will be retried. | Installment Plans |
| *Your payment is overdue* | The last retry was declined. | Installment Plans |
| *Your payment plan was canceled* | A started plan was canceled. | Installment Plans |
| Upcoming payment reminder, naming the card for a payment that is charged | A few days before each due date. | Transaction Reminders |
| Invoice with a pay link | The first time the customer is told about an invoiced payment. | Transaction Reminders |
| Overdue reminders | While a payment the customer pays is overdue. | Transaction Reminders |
| Receipt | After every payment, the down payment included. | Send Payment Receipts |
| Refund notice | When a refund of a plan payment succeeds. | Send Payment Receipts |

The Installment Plans notices go through Orchard Core's `INotificationService` when the Notifications feature is enabled, so they honor each user's preferred notification methods.

## Data

| What | Where |
| --- | --- |
| Plans | YesSql documents of type `InstallmentPlan` in the `InstallmentPlan` collection. `InstallmentPlanIndex` holds the title, customer, status, currency, total, next due date, creation date, `ReferenceType`, `ReferenceId` (both up to 255 characters) and `SourceTransactionId`. The last three were added by an additive migration step (version 2); plans created before it have them empty. |
| Payments | The plan's `Payments` list. Each payment names its transaction (`TransactionId`) and the checkout sessions that charged it. |
| Transactions | The source is `installment-plan` and the `ReferenceType` is `InstallmentPlan`. `ReferenceId` is the plan id, and `ObligationId` is the payment number (`0` is the down payment). `ParentReferenceType` and `ParentReferenceId` name the record the plan pays for, and are indexed in `TransactionParentReferenceIndex`; `Properties` is a copy of the plan's. |

The plan's computed values (`DownPayment`, `FirstPayment`, `AmountPaid`, `AmountOutstanding` and `NextPayment`) are `[JsonIgnore]`. Orchard Core's document serializer populates existing collections when it reads a document. So a stored computed property that returns a list item is read back into that same item, and its lists double on every save. Keep any property you add that derives from other members out of the document the same way.

## Extending

### Creating and managing plans from code

`IInstallmentPlanService` covers the whole lifecycle. Use it rather than the store, so the transactions, sessions, locks and history stay consistent:

```csharp
public sealed class PhoneSalesService
{
    private readonly IInstallmentPlanService _plans;

    public PhoneSalesService(IInstallmentPlanService plans)
    {
        _plans = plans;
    }

    public async Task<string> StartPlanAsync(string customerUserId)
    {
        var result = await _plans.CreateAsync(new CreateInstallmentPlanRequest
        {
            Title = "Annual service",
            CustomerUserId = customerUserId,
            Currency = "USD",
            TotalAmount = 1200m,
            DownPaymentAmount = 300m,
            InstallmentCount = 3,
            Frequency = InstallmentFrequency.Monthly,
            FirstDueUtc = DateTime.UtcNow.Date.AddMonths(1),
            CollectionMethod = InstallmentCollectionMethod.AutoCharge,
        });

        if (!result.Succeeded)
        {
            // Each error is keyed by the request member it is about.
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Value)));
        }

        // The down payment is taken on the plan's payment page, or by any code that completes
        // result.Plan.DownPaymentSessionId through ICheckoutEngine.
        return result.Plan.ItemId;
    }
}
```

| Method | Use |
| --- | --- |
| `CreateAsync` | Creates a draft plan with its first-payment transaction and checkout session. |
| `CreateFromTransactionAsync` | Turns an outstanding transaction into a plan in the customer's own checkout. See [Installments at checkout](#installments-at-checkout). |
| `ActivateAsync` | Starts the schedule once the down payment is paid. It is idempotent, and `ProcessAsync` calls it for you. |
| `ChargeAsync(planId, paymentNumber)` | Charges one payment now, as **Charge now** does. |
| `ProcessAsync` | Brings a plan up to date. It is safe to call at any time. |
| `CancelAsync` | Cancels a plan. |

To read plans, use `IInstallmentPlanStore`:

- `PageAsync` filters by status, customer or text (`InstallmentPlanQuery`).
- `GetOpenAsync` returns the plans that still have work to do.
- `GetByReferenceAsync(referenceType, referenceId)` returns the plans that pay for a record.
- `GetBySourceTransactionAsync(transactionId)` returns the plans that replace an outstanding transaction.

### Linking a plan to your own record

A module that sells something of its own, such as a season registration, passes its record as the plan's reference. It needs no table of its own that maps plans to records:

```csharp
var result = await _plans.CreateAsync(new CreateInstallmentPlanRequest
{
    Title = "2027 season fee",
    CustomerUserId = parentUserId,
    Currency = "USD",
    TotalAmount = 300m,

    // No down payment: the first of the three payments is taken now, while the card is entered.
    DownPaymentAmount = 0m,
    InstallmentCount = 3,
    Frequency = InstallmentFrequency.Monthly,
    CollectionMethod = InstallmentCollectionMethod.AutoCharge,
    ReferenceType = "Registration",
    ReferenceId = registration.Id,
    Properties = new Dictionary<string, object>
    {
        ["season"] = "2027",
    },
});
```

From any payment of the plan, for example in an `ITransactionPaymentHandler`, the record is `transaction.ParentReferenceType` and `transaction.ParentReferenceId`. The other way round:

| To find | Call |
| --- | --- |
| Every payment of the record | `ITransactionStore.GetByParentReferenceAsync("Registration", registrationId)` |
| The plans of the record | `IInstallmentPlanStore.GetByReferenceAsync("Registration", registrationId)` |
| The plan of a payment | `IInstallmentPlanStore.FindByIdAsync(transaction.ReferenceId)` |

### Adding a payment provider that can keep a card

Any provider can take installment plans. It needs to:

1. Report `SupportsSavedPaymentMethods = true` and `SupportsEmbeddedElements = true` in its `PaymentProviderCapabilities`.
2. Honor the `CheckoutPaymentDataKeys` in `BeginPaymentOptions.ProviderData`:
   - keep the payment method when `savePaymentMethod` is `true`;
   - when `offSession` is `true`, charge the method named by `savedCustomerReference` and `savedPaymentMethodReference` without the payer.
3. Register an `ICheckoutSavedPaymentMethodProvider` with the same `Key`. It reads the kept method back from a settled `PaymentAttempt`:

```csharp
public sealed class AcmeSavedPaymentMethodProvider : ICheckoutSavedPaymentMethodProvider
{
    public string Key => "acme";

    public Task<SavedPaymentMethod> GetSavedPaymentMethodAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default)
    {
        // Read what the gateway kept for this attempt. Return null when nothing was kept.
        return Task.FromResult(new SavedPaymentMethod
        {
            ProviderKey = Key,
            CustomerReference = "...",
            PaymentMethodReference = "...",
            Brand = "visa",
            Last4 = "4242",
            ExpirationMonth = 12,
            ExpirationYear = 2030,
        });
    }
}
```

For the rest of the provider contract, see [Adding another payment provider](payments#adding-another-payment-provider).

### Reacting to plan payments

A plan payment is a transaction payment. To react to it, implement `ITransactionPaymentHandler` and filter on the reference type. It is raised once for every payment applied to a transaction, however it was paid:

```csharp
public sealed class PlanPaymentHandler : ITransactionPaymentHandler
{
    public Task PaymentRecordedAsync(TransactionPaymentRecordedContext context, CancellationToken cancellationToken = default)
    {
        var transaction = context.Transaction;

        if (transaction.ReferenceType != "InstallmentPlan")
        {
            return Task.CompletedTask;
        }

        // transaction.ReferenceId is the plan id; transaction.ObligationId is the payment number (0 = down payment).
        // transaction.ParentReferenceType and ParentReferenceId name the record the plan pays for, when it has one.
        // context.Payment is the payment event: its amount, method and receipt number.
        return Task.CompletedTask;
    }
}
```

Register it with `services.AddScoped<ITransactionPaymentHandler, PlanPaymentHandler>()`.

For refunds, implement `IPaymentRefundHandler.RefundSucceededAsync`. It is raised once per refund, however the refund finished:

- immediately;
- from a webhook;
- by reconciliation;
- marked settled by an administrator.

### Numbering, invoices and pay links

Plan payments use the Transactions services. Each can be replaced like any other scoped service:

| Service | Default | Replace it to |
| --- | --- | --- |
| `IFinancialDocumentNumberGenerator` | `SequentialFinancialDocumentNumberGenerator`: a per-tenant series under a distributed lock, starting at 1001 (`R-1001` for receipts, `INV-1001` for invoices). | Use your own numbering, for example a series per year. |
| `ITransactionInvoiceService` | Gives a transaction its invoice number the first time its owner is told about it. | Change when invoices are numbered. |
| `ITransactionPayLinkService` | Signed links to `/transactions/pay/{token}` that expire after 60 days. | Change the link's format or lifetime. |

See [Invoices and pay links](transactions#invoices-and-pay-links).

### Notices

The plan's own notices are sent through `ICustomerOwnerNotifier`, the same notifier that sends transaction receipts and reminders, which reaches the plan's customer through `INotificationService`. To send notices another way, or to add your own (for example to the administrator who created the plan), you can:

- replace `ICustomerOwnerNotifier`, which changes how every commerce notice reaches a buyer;

- react to the transaction events above;
- add notification method providers to Orchard Core's Notifications module.

## Operations

- **Reconciling a plan.** Every plan payment is a transaction, and each attempt the gateway accepted or declined is listed in **Commerce > Payments** with a link to its transaction.
- **A plan is stuck in `Draft`.** The first payment was never completed. Open the plan and take the payment again, or cancel the plan. A plan a customer chose at checkout is closed automatically once its checkout expires.
- **Payments are not being charged.** Check that background tasks run on the tenant, and that `InstallmentPlanBackgroundTask` is not disabled under **Configuration > Tasks**. Then check the plan's history for declines.
- **Reminders have no pay link.** Set the site's **Base URL**.
- **An auto-charge plan switched to invoicing.** The provider did not keep the card used for the down payment, for example because the card or wallet does not allow future use. The plan's history says so.

## Related

- [Subscriptions](subscriptions): the module this feature belongs to (the feature does not need the Subscriptions feature itself).
- [Transactions](transactions): the ledger every plan payment is recorded on, with reminders, receipts, invoices and pay links.
- [Checkout](checkout): the engine every charge goes through.
- [Payments](payments): the Stripe provider, saved cards, and Apple Pay / Google Pay.

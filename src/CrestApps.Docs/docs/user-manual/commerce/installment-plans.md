---
sidebar_label: Installment Plans
title: Installment Plans
description: Sell something on a payment plan by taking a first payment by card today and collecting a fixed number of scheduled payments, charged to the saved card or invoiced to the customer, and let customers choose to pay a balance in installments at checkout.
technical_manual:
  - modules/installment-plans
  - modules/transactions
  - modules/checkout
---

An **installment plan** lets you sell something on a payment plan without the customer going through your online store, for example over the phone or at a counter. You take a **down payment** by card today, and the rest of the price is collected as a fixed number of **scheduled payments**, either charged to the customer's saved card on each due date or invoiced to the customer, who pays each one online. A plan can also have no down payment: the first scheduled payment is then taken today instead.

Customers can also set up a plan themselves: when they pay an outstanding balance, checkout can offer them a **Choose how to pay** step where they pick, for example, *3 monthly payments* instead of paying in full. See [Let customers choose installments at checkout](#let-customers-choose-installments-at-checkout).

| | |
| --- | --- |
| **Menu** | Subscriptions > Installment Plans |
| **Permission** | Manage installment plans and charge customers' saved cards |
| **Feature** | Subscriptions - Installment Plans |

<AskYourAdmin />

:::note[A card provider is required]
Installment plans need a payment provider that can keep a card for later payments and show its card form on the page, such as Stripe. Without one, **New plan** shows the warning *No payment provider that can keep a card for later payments is enabled*. See [Payment providers](payment-providers.md).
:::

<YouTube video="commerce" title="Narrated video of setting up an installment plan and taking the down payment" start={304} end={363} />

This video shows how a plan works, the settings, creating and managing a plan, and what happens with a card, declines and checkout installments:

<YouTube video="installment-plans" title="Narrated video overview of installment plans" />

## How a plan works

A plan is made of:

- a **total**: everything the customer pays, the down payment included;
- a **down payment**, charged to a card today, or none, in which case the first scheduled payment is charged today instead;
- a **number of payments after it**, which split the rest of the total into equal amounts;
- how often those payments **fall due** (every week, every two weeks, every month or every three months) and when the **first payment** is due;
- how the payments are **collected**: charged to the card automatically, or invoiced to the customer.

The rest of the total is split into equal payments. When it does not divide exactly, the last payment takes the difference, so the schedule always adds up to the total. Monthly and three-monthly payments fall on the same day of the month as the first one; in a shorter month they fall on its last day.

Nothing is scheduled until the down payment (or, without one, the first payment) is received. Every payment on the plan, the down payment included, is also a [transaction](transactions.md), so it appears in **Commerce > Transactions** and in the customer's **My Transactions**, and a payment that was received can be refunded from **Commerce > Payments** like any other (see [Payments and refunds](payments-and-refunds.md)).

## Create a plan

<YouTube video="installment-plans" title="Video of creating an installment plan" start={135} end={200} />

1. Open **Subscriptions > Installment Plans** and click **New plan**.
2. In **Customer**, choose who is paying (see [Pick or create the customer](#pick-or-create-the-customer)).
3. Fill in the **Plan** card. The **Schedule** card at the bottom of the page shows every payment, its due date and its amount as you type.
4. In **Collecting the payments**, choose how the scheduled payments are collected.
5. Click **Continue to the down payment**. The plan is saved as **Waiting for down payment** and the down payment page opens.
6. Take the down payment (see [Take the down payment](#take-the-down-payment)).

### Pick or create the customer

| Field | What it does |
| --- | --- |
| **Existing customer** | Click **Choose a customer**, type in **Search customers**, and pick the person. |
| **New customer** | Enter the customer's **Name** and **Email**. An account is created for them with that email. It has no password yet: the customer sets one with **Forgot password** when they want to sign in to see or pay their payments. |

If someone already has an account with that email, you are asked to choose them from the list instead.

### Plan

| Field | What it does |
| --- | --- |
| **What the plan is for** | A short description, such as *Kitchen remodel*. It is shown to the customer on every payment, reminder and receipt. Required. |
| **Currency** | The currency of every payment on the plan. |
| **Total** | Everything the customer pays, the down payment included. |
| **Down payment today** | The amount charged to the card today. It must be less than the total. Enter **0** for no down payment: the first scheduled payment is then charged today instead, and **First payment due** must be today. |
| **Number of payments after it** | How many scheduled payments split the rest, from 1 to 120. |
| **Payments fall due** | **Every week**, **Every two weeks**, **Every month** or **Every three months**. |
| **First payment due** | The due date of the first scheduled payment. With a down payment it must be after today, because today's payment is the down payment. Without one it must be today, because the first payment is the one taken today. |

### Collecting the payments

| Field | What it does |
| --- | --- |
| **Charge the card on each due date** | The card used for the down payment is kept and charged automatically on each due date. The customer is told a few days before each charge. A declined card is retried, then left for the customer to pay. |
| **Invoice the customer for each payment** | The customer is reminded before each due date and pays from their transactions. The card is used for the down payment only. |
| **Internal note** | A note for your team. It is shown on the plan page, never to the customer. |

The option that is selected when the form opens is set in the plan settings (see [Change the plan settings](#change-the-plan-settings)).

**Example:** a total of 1,200.00 with a down payment of 300.00 and **6** payments **Every month** gives a down payment of 300.00 today and six payments of 150.00, one a month from the first due date. With a down payment of **0** instead, the same plan is six payments of 200.00: the first today, then one a month.

## Take the down payment

The **Take the down payment** page shows the card form on the left and a summary of the plan on the right: the customer, the total, the **Down payment now** (or **First payment now** for a plan without a down payment), the payments that follow, and how they are **Collected by**.

1. Make sure the customer has agreed. When the plan charges the card, the page says the card is charged the down payment now and kept for the scheduled payments.
2. Enter the **Name on card** and the **Card details**.
3. Click the **Charge** button, which shows the currency and the amount, for example **Charge USD 300.00**.
4. When the payment is confirmed, the plan page opens and the plan is **Active**.

If the device you take the payment on has Apple Pay or Google Pay set up, those buttons appear above the card form, followed by **or pay by card**. On a device without a wallet, only the card form is shown.

If the bank asks for an extra check, the card form shows it. When the payment takes a moment to confirm, the page says *The payment is still being confirmed. The plan starts as soon as it is; you can leave this page.*

:::tip[Taking the down payment later]
A plan that is still **Waiting for down payment** shows **Take the down payment** in the list and on its page, so you can come back to it. If the down payment can no longer be taken, the plan page says so: cancel the plan and create it again.
:::

If the card used for the down payment cannot be kept for later payments, the plan's **History** says so and the scheduled payments are invoiced to the customer instead.

## Manage a plan

<YouTube video="installment-plans" title="Video of the plan list, the plan page and canceling a plan" start={200} end={249} />

<YouTube video="commerce" title="Narrated video of managing installment plans: Charge now, Cancel the plan, and the list" start={362} end={409} />

Open a plan by clicking its title or **Manage** in the list. The plan page has these cards:

| Card | What it shows |
| --- | --- |
| **Summary** | The status, the **Customer**, the **Total**, what has been **Received**, what is **Outstanding**, the **Schedule**, **Pays for** (the record the plan pays for, when another part of the site created it for one), **Replaces** (when the customer chose the plan at checkout to pay an outstanding balance), how it is **Collected by** (the card on file and its expiry, or *Invoicing the customer*), when and by whom it was **Created**, and the internal **Note**. |
| **Payments** | One row per payment: the **Down payment**, then **Payment 1 of 6** and so on, each with its status, amount, due date, the date it was received, the **Next try** of a declined card, and the reason of the last decline. **Transaction** opens the payment's transaction and **Receipt** opens the receipt of a received payment. |
| **History** | Everything that happened to the plan, newest first: created, down payment received, payments received, declines and retries, cancellation. |
| **Cancel the plan** | Shown while the plan is not finished. |

When the plan charges the card and the card on file has expired, the page warns *The card on file has expired, so the next charges will be declined. Ask the customer for a new card.*

### Charge a payment now

Use **Charge now** to collect a payment before its due date, or to retry a declined card straight away. It is shown on each payment that is not yet received, when the plan is **Active** or **Past due** and charges the saved card.

1. Click **Charge now** on the payment.
2. The confirmation names the amount, the card and the payment. Click **Charge now** to charge the card immediately, or **Don't charge**.

The page then says whether the payment was charged and received, or that it was sent to the card and is still being confirmed. If the card is declined, the reason appears under the payment and in **History**. A declined early charge leaves the payment on its schedule: it is still charged on its due date.

### Cancel a plan

1. In the **Cancel the plan** card, optionally enter a **Reason (optional)**. It is added to the plan's **History**.
2. Click **Cancel the plan**.
3. In the confirmation, click **Cancel the plan** again, or **Keep the plan**.

Payments already received stay received and can be refunded from **Commerce > Payments**. Payments not yet received are canceled and will not be collected. When the plan had started, the customer is notified that it was canceled, with how much was received. A plan still **Waiting for down payment** took nothing from the customer, so no notice is sent.

## When a card is declined

<YouTube video="installment-plans" title="Video of declined cards and statuses" start={296} end={336} />

When the plan charges the card, each payment is charged on its due date. If the card is declined:

1. The payment is marked **Failed**, and its transaction in **Commerce > Transactions** shows **Failed** too. The plan becomes **Past due**, and the customer is notified that the payment could not be taken and when it will be tried again.
2. The card is tried again after the number of days set in **Retry a declined card after (days)**, by default 1, 3 and 5 days after each failure.
3. After the last retry, the payment becomes **Due**: an outstanding balance the customer pays themselves from **My Transactions**. The customer is told the payment is overdue, and the site's overdue payment reminders take over (see [Transactions](transactions.md)).

If the payment service could not be reached at all, nothing is known about the card, so the charge is simply tried again shortly and does not use up a retry.

## Find a plan

The list shows one row per plan, with its title, status and these badges: **Customer**, **Total**, **Received**, **Outstanding** (on open plans that still owe money) and **Next payment** (the amount and its due date).

- Type in **Search by plan or customer** and press Enter to find a plan by its title or the customer's name or email.
- Use the status filter in the list header to show only plans with one status, or **Any status** for all of them.
- Use the pager at the bottom to move between pages, and **Items per page** to show more or fewer plans on a page.

## Plan statuses

| Status | What it means |
| --- | --- |
| **Waiting for down payment** | The plan was created, but the down payment has not been received. Nothing is scheduled yet. |
| **Active** | The down payment was received and the scheduled payments are being collected. |
| **Past due** | A charge to the card failed, or an invoiced payment is past its due date and unpaid. A payment due today is not past due yet. |
| **Completed** | Every payment was received. |
| **Canceled** | The plan was canceled. Payments not yet received will not be collected. |

## Payment statuses

| Status | What it means |
| --- | --- |
| **Scheduled** | Not due yet. |
| **Due** | The customer owes it now: an invoiced payment that reached its due date, or a declined payment with no retries left. |
| **Paid** | Received. |
| **Failed** | The card was declined. **Next try** shows when it is charged again. |
| **Canceled** | The plan was canceled before this payment was received. |

## What the customer receives

| When | What the customer gets |
| --- | --- |
| A few days before each payment | A reminder. When the card is charged, it names the card and says there is nothing to do. When the plan is invoiced, the reminder is the invoice: it carries the invoice number and a link that pays it online, no sign-in needed. |
| After each payment | A receipt, including for the down payment. |
| When a charge is declined | A notice that the payment could not be taken and when it will be tried again, then an overdue notice once no retries are left. |
| When a started plan is canceled | A notice that the plan was canceled and how much was received. |

The customer can also see every payment of the plan in **My Transactions** and pay what is due from there. When they pay online from a phone with Apple Pay or Google Pay set up, they can pay with the wallet. Reminders need the **Transaction Reminders** feature and receipts are sent with the **Send Payment Receipts** feature. See [Transactions](transactions.md) and [Invoices and receipts](invoices-and-receipts.md).

## Let customers choose installments at checkout

<YouTube video="installment-plans" title="Video of customers choosing installments at checkout" start={336} end={380} />

A customer paying an outstanding balance, from **My Transactions** or from the link in a reminder, can be offered a **Choose how to pay** step before the payment: **Pay in full**, or one of the installment choices you offer, such as **3 monthly payments**. When they choose installments:

1. A plan is created for them, with no down payment: the first payment is charged in the same checkout, and the card is saved.
2. Once the first payment is received, the plan is **Active**, the original balance is canceled because the plan replaces it, and the remaining payments are charged to the saved card on their due dates, exactly like a plan you set up yourself.
3. If the customer leaves checkout without paying, the plan is closed and they still owe the original balance.

You turn this on in **Settings > Installment Plans**, in the **Installments at checkout** section (see [Change the plan settings](#change-the-plan-settings)): pick the kinds of balance it is offered for in **Offer for balances from**, and the choices in **Numbers of payments offered**. Other parts of your site can offer their own choices too, for example a registration that lets families pay a season fee in three payments.

Installments are offered only for a balance still owed in full by a customer with an account, and only when a payment provider that can keep a card, such as Stripe, is enabled. A balance that is already a plan payment, or a recurring charge, is never split again.

A plan a customer chose appears in **Subscriptions > Installment Plans** like any other. While its first payment has not been paid, its page says *The customer chose this plan at checkout and has not paid the first payment yet.* and has no **Take the down payment** button: the customer pays it in their own checkout.

## Change the plan settings

<YouTube video="installment-plans" title="Video of the installment plan settings" start={87} end={135} />

The plan settings are on **Settings > Installment Plans**, which needs the **Manage installment plans and charge customers' saved cards** permission.

| Field | What it does |
| --- | --- |
| **Collect scheduled payments by** | What a new plan starts with: **Charging the saved card on each due date** or **Invoicing the customer for each payment**. You can still change it on each plan. |
| **Retry a declined card after (days)** | When charging the saved card fails, it is tried again this many days after each failure, in order, for example *1, 3, 5*. Enter up to 10 whole numbers between 1 and 60, separated by commas. Leave it empty to hand a declined payment to the customer straight away. |

### Installments at checkout

| Field | What it does |
| --- | --- |
| **Offer for balances from** | The kinds of balance customers may pay in installments, such as *Pay Later*. Only kinds that are enabled on your site are listed. Leave them all unticked to offer nothing from these settings. |
| **Numbers of payments offered** | The choices customers see, for example *3, 6* for **3 monthly payments** and **6 monthly payments**. Enter up to 6 whole numbers between 2 and 120, separated by commas. The first payment is taken at checkout. |
| **Payments fall due** | **Every week**, **Every two weeks**, **Every month** or **Every three months**. |
| **Smallest balance** | Balances below this amount can only be paid in full. **0** allows any balance. |

How many days before a due date the customer is reminded is set in **Remind before the due date (days)** under **Settings > Commerce > Transactions** (see [Transactions](transactions.md)).

## Tips and troubleshooting

- **The menu or New plan is missing.** Ask your administrator to enable **Subscriptions - Installment Plans** and to give you the **Manage installment plans and charge customers' saved cards** permission. The feature does not need **Subscriptions** or **Products** to be enabled.
- **Customers don't see Choose how to pay.** Check **Settings > Installment Plans**: the kind of balance must be ticked in **Offer for balances from** and **Numbers of payments offered** must not be empty. The balance must be owed in full by a customer with an account, and a payment provider that can keep a card must be enabled.
- **The down payment page has no card form.** No payment provider that can keep a card is enabled. Ask your administrator to set one up, such as Stripe.
- **The customer cannot sign in.** A customer created from the plan has no password. Ask them to use **Forgot password** on the sign-in page with the email you entered.
- **The card has changed.** The plan keeps the card used for the down payment. Use **Charge now** only after the customer confirms the card works; otherwise let the declined payment become due so the customer pays it themselves from **My Transactions**.
- **You need to change the amounts or dates.** A plan cannot be edited after it is created. Cancel it and create a new one for the remaining balance.
- **Refund a payment.** Open the payment's **Transaction**, or go to **Commerce > Payments**, and refund it there. See [Payments and refunds](payments-and-refunds.md).

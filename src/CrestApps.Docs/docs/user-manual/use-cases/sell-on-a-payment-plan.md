---
sidebar_label: Sell on a Payment Plan
title: Sell on a Payment Plan
description: Take a down payment by card today and collect the rest in scheduled payments, charged to the customer's card or invoiced with a link that pays it, with reminders, receipts and refunds along the way.
technical_manual:
  - modules/installment-plans
  - modules/transactions
  - modules/payments
---

## Who it's for and what you get

This use case is for businesses that sell over the phone, at a counter or in person, where the customer can't pay the full price at once.

When it is done:

- a store manager sets up a plan in one screen: the customer, the total, a down payment and the schedule;
- the down payment is taken by card on the spot, or with Apple Pay or Google Pay;
- the rest is collected in equal payments, weekly, every two weeks, monthly or quarterly, in one of two ways:
  - **charged to the customer's card** on each due date, with automatic retries if it is declined;
  - **invoiced to the customer**, with a link that pays it without signing in;
- the customer is reminded before each payment and gets a receipt after it;
- every payment appears under **Commerce > Transactions** and **Commerce > Payments**, where it can be refunded.

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an installment plan for a new customer and taking the down payment by card">
  <source src="/img/docs/um-installment-plan-create.mp4" type="video/mp4" />
</video>

## Before you start

| You need | Who sets it up |
| --- | --- |
| The features **Subscriptions - Installment Plans**, **Stripe**, **Transaction Reminders** and **Payment Receipts** | Your administrator, in **Tools > Features** |
| Stripe connected, with your keys saved | Your administrator; see [Payment providers](../commerce/payment-providers.md) |
| The site's **Base URL** set to its public address under **Settings > General**, so the links in reminders work | Your administrator |
| The permission **Manage installment plans and charge customers' saved cards** for whoever sets up plans | Your administrator; see [Roles and permissions](../getting-started/roles-and-permissions.md) |

<AskYourAdmin />

## Steps

1. **Choose how payments are collected by default.** Under **Settings > Subscriptions**, in **Installment Plans**, pick **Charging the saved card on each due date** or **Invoicing the customer for each payment**. Then set how many days after a declined card to try again. See [Installment plans](../commerce/installment-plans.md).
2. **Check the reminders.** Under **Settings > Commerce > Transactions**, check how many days before a due date the customer is reminded, and how often overdue payments are chased. See [Transactions](../commerce/transactions.md#payment-reminders).
3. **Brand the paperwork.** Under **Settings > Commerce > Receipts**, enter your business name, logo and footer. They appear on every invoice and receipt. See [Invoices and receipts](../commerce/invoices-and-receipts.md).
4. **Set up the plan.** Open **Subscriptions > Installment Plans** and click **New plan**. Pick the customer, or create one from a name and email. Enter the **Total**, the **Down payment today**, the **Number of payments after it**, how often **Payments fall due**, and the **First payment due** date. Check the schedule preview, choose how the payments are collected, and click **Continue to the down payment**. See [Installment plans](../commerce/installment-plans.md).
5. **Take the down payment.** Enter the customer's card, or let them use Apple Pay or Google Pay on their phone, and click the **Charge** button. The plan becomes **Active**, and the customer is sent a receipt. See [Installment plans](../commerce/installment-plans.md).
6. **Let it run.** Charged payments are taken on their due dates. Invoiced payments are sent to the customer as invoices with a link that pays them. See [Invoices and receipts](../commerce/invoices-and-receipts.md).
7. **Follow up.** The **Installment Plans** list shows each plan's status. Filter it by **Past due** to find plans that need attention. On a plan, use **Charge now** to collect early or retry a declined card. See [Installment plans](../commerce/installment-plans.md).
8. **Handle the exceptions.** Record a payment the customer made by cash or bank transfer from its transaction. Use **Cancel the plan** to stop collecting. Refund a payment from **Commerce > Payments**. See [Transactions](../commerce/transactions.md) and [Payments and refunds](../commerce/payments-and-refunds.md).

## Check that it works

Use Stripe's test mode and its test card, so no real money moves.

1. Create a plan for a test customer, with a first payment due tomorrow.
2. Take the down payment with the test card. The plan shows **Active**, the down payment shows **Received**, and a receipt is sent.
3. Open **Commerce > Transactions** and filter by **Installment plan** in **Any source**. You see the down payment as **Paid**, and one transaction for each scheduled payment.
4. On the plan, click **Charge now** on the first payment and confirm. The payment shows **Received**.
5. For an invoiced plan, send a reminder from the payment's transaction. Open the link in the reminder in a private window, and pay it with the test card.

## Tips

- Nothing is scheduled until the down payment is received. A plan waiting for its down payment can be finished later with **Take down payment**.
- A customer you create gets an account with no password. They can set one with **Forgot password** when they want to sign in, but they don't need to: invoiced payments are paid from the link in the reminder.
- If the customer's card expires, the plan page warns you. Ask the customer to pay the next payments from their invoices, or set up a new plan.
- Canceling a plan stops future payments only. Money already received stays received, and you refund it separately from **Commerce > Payments**.

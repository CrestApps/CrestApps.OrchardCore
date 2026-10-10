---
sidebar_label: Invoices and Receipts
title: Invoices and Receipts
description: How customers are invoiced for payments they make themselves, how the pay link in a reminder works, and how receipts and refund notices are sent, printed and branded.
technical_manual:
  - modules/transactions
  - modules/receipts
---

An **invoice** tells a customer what they owe and gives them a link to pay it. A **receipt** is their proof that a payment arrived. Both have short numbers that customers can quote to you, both print cleanly, and both carry your business's name, logo and contact details.

| | |
| --- | --- |
| **Menu** | Commerce > Transactions (administrators), Commerce > My Transactions (customers) |
| **Permission** | Manage transactions, or View own transactions for a customer's own invoices and receipts |
| **Feature** | Payment Receipts (printable invoices and receipts, receipts and refund notices), Transaction Reminders (invoices and pay links) |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of sending a reminder that numbers an invoice, opening the invoice and copying its pay link">
  <source src="/img/docs/um-invoice.mp4" type="video/mp4" />
</video>

## Invoice numbers

A transaction gets an **invoice number** the first time the customer is told about a payment they pay themselves, which is the first [payment reminder](transactions.md#payment-reminders) for it: either the notice that a payment is coming due, or the first reminder once it is due. Numbers run in order across the whole site: **INV-1001**, **INV-1002**, and so on.

- A transaction keeps its number for good. Every later reminder, and the printed invoice, shows the same number.
- A payment charged automatically to a saved card, such as a scheduled payment of an [installment plan](installment-plans.md), gets no invoice. The customer is only told which card will be charged and when.
- If a reminder can't be delivered, the number it was given is kept, so the next attempt sends the same number.

Once a transaction has an invoice number, it shows as **Invoice** on the transaction's page, for administrators and for the customer.

## Pay from a reminder

Every reminder for a payment the customer pays themselves carries a **link that pays it without signing in**. Email reminders show it as a button, such as **Pay USD 50.00**.

- The link opens a page for that one payment. Nobody needs an account or a password to use it, so a guest who bought without an account can pay too.
- Each link works for **60 days**. Every reminder sends a fresh link, so the newest reminder always has one that works.
- Anyone who has the link can pay with it. Forwarding the reminder to someone else, such as the person who handles payments at the customer's company, is fine.

<video controls preload="metadata" width="100%" aria-label="Screencast of a customer opening a pay link without signing in and paying the invoice by card">
  <source src="/img/docs/um-pay-link.mp4" type="video/mp4" />
</video>

### The pay page

The page shows the payment's title, the **Amount due** and the **Due on** date, and a button such as **Pay USD 50.00**. Clicking it opens [checkout](checkout.md) with the balance ready to pay. With the **Payment Receipts** feature on, the full invoice is shown under the button, with a **Print invoice** button.

The page says instead:

| Message | What it means |
| --- | --- |
| *This payment has been received. Thank you.* | The balance is already paid. |
| *This payment was canceled, so there is nothing to pay.* | The transaction was canceled. |
| *Online payment is not available right now. Please contact us to pay.* | Online payment isn't set up on the site. |
| *This payment link no longer works* | The link is more than 60 days old, was changed, or the payment no longer exists. The customer can sign in and pay from **My Transactions**, or ask you for a new link. |

## Print an invoice

The invoice page shows the invoice exactly as it prints.

1. Open the transaction: **Commerce > Transactions** for an administrator, **Commerce > My Transactions** for a customer.
2. Click the invoice number next to **Invoice**.
3. Click **Print invoice**. Only the invoice is printed, without the site's menus.

The invoice shows:

- your business's logo, name and address, from the [receipt branding settings](#brand-your-invoices-and-receipts), under the heading **Invoice**;
- **Invoice:** with the invoice number, and the **Date** the transaction was recorded;
- **Billed to:** the customer's name and email;
- the **Status**: **Due** while something is owed, **Paid** once the balance is paid;
- the line for what the payment is for, any **Tax**, the **Subtotal** and the **Total**;
- **Notes** with the due date and, after a part payment, how much has been received and how much is still due;
- your footer text and contact details.

A customer whose invoice is still due also sees **Pay now** on the invoice page.

### Copy the payment link for a customer

Administrators can send a customer the pay link themselves, for example in a reply to their email.

1. Open the transaction from **Commerce > Transactions** and click the invoice number.
2. Under **Payment link**, click **Copy**. The button changes to **Copied**.
3. Paste the link into your message.

The link works for 60 days, and a new one is made each time you open the page. It is shown only while there is something to pay.

## Receipts

With the **Payment Receipts** feature on, the customer is sent a receipt after **every payment** on a transaction, however it was paid: online through checkout or a pay link, charged automatically to a saved card, or recorded by an administrator with **Record payment** or **Mark as paid**.

- Each receipt gets its own number, in order across the site: **R-1001**, **R-1002**, and so on. The number never changes, so a reprinted receipt shows the number the customer was sent.
- The message is titled *Receipt for your payment of*, followed by the amount, thanks the customer, gives the receipt number, and includes the receipt itself.
- Customers with an account get it through the site's notifications, on the methods they chose. Guests get it by email at the address they gave when they bought.
- The transaction's **Timeline** notes that a receipt was sent.

A receipt shows:

- your business's logo, name and address, under your **Header title** (*Payment receipt* unless you change it);
- **Receipt:** with the receipt number, and the **Date** of the payment;
- **Billed to**, and the **Status** **Paid**;
- the amount paid, with its share of the transaction's tax;
- **Notes**: *Paid offline.* for a payment an administrator recorded, and *Payment for invoice INV-1001.* when the payment settled an invoice;
- a **Test payment** badge when the payment was made through a payment provider in test mode, unless you turn the badge off;
- your footer text and contact details.

### Print a receipt

1. Open the transaction: **Commerce > Transactions** for an administrator, **Commerce > My Transactions** for a customer.
2. Find the payment in the **Timeline** (administrators) or **History** (customers) and click **View receipt**.
3. Click **Print receipt**.

## Refund notices

With the **Payment Receipts** feature on, when a refund of a payment on a transaction succeeds, the customer is told. The message is titled *Your refund of*, followed by the amount, and says the money went back to the card or account they paid with and can take a few business days to appear on their statement. The transaction's **Timeline** notes that the customer was told. A refund that an administrator settles by hand with **Mark settled** sends the same notice. See [Payments and Refunds](payments-and-refunds.md).

## Brand your invoices and receipts

Invoices and receipts share one set of branding settings.

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Receipts |
| **Permission** | Manage receipt settings |
| **Feature** | Receipts |

<AskYourAdmin />

1. Open **Settings > Commerce > Receipts**.
2. Fill in the fields below and click **Save**.

| Field | What it does |
| --- | --- |
| **Header title** | The heading printed at the top of every receipt. Empty uses *Payment receipt*. Invoices always use the heading *Invoice*. |
| **Business name** | Your business's name, shown at the top. Empty uses the site name. |
| **Logo URL** | The web address of the logo shown at the top. |
| **Business address** | Your postal address, shown under your name. |
| **Contact email** | An email address printed at the bottom. |
| **Contact phone** | A phone number printed at the bottom. |
| **Website** | A website address printed at the bottom. |
| **Footer text** | Free text printed at the bottom, such as a thank-you note or your return policy. |
| **Show a test-payment badge for test-mode payments** | Marks receipts for payments made in a payment provider's test mode with a **Test payment** badge. On by default. |

## Make sure links point at your site

The pay links in reminders, and the link you copy from an invoice, use your site's public address. Your administrator sets it in **Base url** under **Settings > General**, for example *https://shop.contoso.com*.

Set it before you rely on pay links. Without it, a link copied from the invoice page uses whatever address you opened the admin from, which a customer may not be able to reach, and the reminders the site sends on its schedule go out **without** a pay link: customers with an account are asked to sign in to pay instead.

## Related pages

- [Transactions](transactions.md): the report, recording payments and reminder settings.
- [Payments and Refunds](payments-and-refunds.md): refunding a payment.

---
sidebar_label: Customers
title: Customers
description: Find and manage the people and businesses who buy from your store, whether they have an account or bought as a guest.
technical_manual:
  - modules/customers
---

Everyone who buys from your store is a **customer**: a person or a business, with an account on your site or as a guest. Use this page to find a customer, see how they're addressed, and keep their details up to date.

| | |
| --- | --- |
| **Menu** | Commerce > Customers |
| **Permission** | **View customers** to look, **Manage customers** to add, edit, export and erase; **Manage own customer profile and addresses** for **My Account** |
| **Feature** | Customers |

<AskYourAdmin />

## Customers and accounts

A customer's sign-in, password and email stay on their **account**, which is managed under **Security > Users** like any other user. The customer record keeps what your store needs on top of that: how to address them, their company and tax id, and whether they want news and offers.

A customer who signs in and places an order becomes a customer the first time they order. Their customer record starts with their user name and the account's email.

Someone who buys as a guest becomes a **guest** customer with the name, email, phone and billing address they gave at checkout. Each guest checkout makes its own guest customer: an email typed at checkout proves nothing, so it never joins someone else's record.

## Guests who create an account

When someone who bought as a guest creates an account with the same email, or signs in to one they already have, their guest purchases join the account, but only once the account's email is confirmed. An email typed at checkout proves nothing, so nobody can claim someone else's orders by typing their email.

When that happens:

- the guest's orders and Pay Later balances move to the account, and appear under **My Orders** and **My Transactions**;
- their addresses join the account's address book, without repeating one already there;
- the guest customer is marked **Merged**, links to the account's customer, and can no longer be edited. The account's customer lists the guest checkouts merged into it.

## Find a customer

1. Open **Commerce > Customers**. The most recently active customers come first.
2. Type the start of a name, company or email in the search box and press Enter.
3. Use the list beside the count to show only customers **With an account**, **Guests**, **Businesses**, **Disabled** ones, or **All customers**.

Each customer shows their email and when they were last active, with badges for guests, businesses and disabled customers.

## Add a customer

For a customer you deal with outside the site, for example someone who orders by phone:

1. Open **Commerce > Customers** and click **Add customer**.
2. Fill in their details (below). Enter at least a name, a company or an email, so you can find them again.
3. Click **Save**.

## Edit a customer

Click a customer to open them, change their details, and click **Save**.

| Field | What it does |
| --- | --- |
| **Kind** | **Person** or **Business**. A business shows **Company** and **Tax id**. |
| **Status** | **Active**, or **Disabled** when you stop doing business with them. A disabled customer keeps their orders and history. |
| **First name**, **Last name** | The person's name. |
| **Display name** | How your store addresses the customer. Leave it empty to use their name, or a business's company. |
| **Company** | Required for a business. |
| **Tax id** | Such as a VAT number. |
| **Exempt from tax** | For a business that pays no tax, such as a reseller or a charity you hold a certificate for. When they check out signed in, no tax is charged. Guests are always taxed. |
| **Why they are exempt** | Such as the resale certificate number, for your records. |
| **Email** | For a guest, the email you write to. For a customer with an account, it's the account's email and is changed on the account. |
| **Phone** | A number to reach them. |
| **Accepts marketing** | Tick it when the customer agreed to news and offers. Order and payment messages are always sent. |

Below the details, **Account** links to the customer's account when they have one, and shows when they became a customer and when they were last active. **Orders** lists the customer's recent orders, each linking to the order, and **Merged guest checkouts** lists any guest customers merged into this one.

If someone else saved the customer while you were editing, your changes aren't saved and you're asked to review theirs first.

## Store credit and points

With the **Store Credit** and **Loyalty Points** features on, a customer's page has two more sections below **Orders**:

- **Store credit** shows what the store owes the customer in each currency, or *No store credit.*, a form to give or take away credit, and the latest changes with their reasons.
- A section named after your rewards program, such as **Rewards**, shows the customer's points and what they're worth, such as *1,250 points worth $12.50*, a form to give or take away points, and the latest changes.

Both need **Manage customers** to change, and a reason for every change. Only customers with an account have them: a guest's page says so instead of showing the form. See [Store credit, loyalty points and referrals](store-credit-and-rewards.md).

## Addresses

A customer keeps an address book of up to 20 addresses, such as a home and an office. One can be the **default billing** address and one the **default shipping** address; they're the ones offered first when the customer buys.

1. Open the customer. Their addresses are listed under **Addresses**, with badges on the defaults.
2. Click **Add address**, or **Edit** on an address.
3. Choose the **Country** first. The rest of the form follows it: the fields the country uses, in its order and with its names, such as **State** and **ZIP code** for the United States, **Post town** and **Postal code** for the United Kingdom, or **Prefecture** first for Japan. Where a country has a list of states or provinces, you pick one from it.
4. Fill in the address, give it a **Label** such as *Home* or *Office* if you like, and click **Save**.

| Field | What it does |
| --- | --- |
| **Label** | A name for the address, such as *Home* or *Office*. |
| **Country** | Required. Decides the other fields. |
| **Full name** | Who receives mail at the address. Required. |
| **Company** | The business at the address, if any. |
| The address fields | Street address and the second line, then the parts the country uses (city or post town, state, province or prefecture, postal code…). The ones marked * are required for that country. |
| **Phone** | A number for the courier. |
| **Default billing address**, **Default shipping address** | Tick one to make this address the default. Ticking it moves the default from another address; unticking it on the default leaves none. |

The address is checked against its country: a postal code has to look like that country's, and belong to the state or province chosen. A new address starts with the customer's name, company and phone, in the country of their other addresses, and a customer's first address becomes both defaults.

To remove an address, click **Delete** and confirm. Orders keep their own copy of the addresses they used, so changing or deleting an address never changes a past order.

## Privacy requests

People can ask what you hold on them, or ask you to erase it. Open the customer; the **Privacy** section at the bottom has both, for those with **Manage customers**:

- **Download data** gives you a JSON file with everything the store holds on the customer: their details, addresses, and their orders with the payments for them, and, under *commerce*, their checkouts, transactions, payments, refunds, subscriptions, installment plans, coupons used and the customer record Stripe keeps for them. Send it to the person who asked.
- **Erase personal data** removes the customer's name, company, tax id, email, phone, addresses and marketing consent, and the name, email, phone and billing address of their orders. It also removes their name, email and phone from their checkouts, transactions, subscriptions and installment plans, the IP address and browser details their checkouts recorded, what they typed into checkout steps, installment plan notes, the card description shown on plans and transactions, and their name and email at Stripe. You're asked to confirm; it can't be undone. The orders, transactions and plans themselves stay, with their lines, amounts, dates, statuses, invoice and receipt numbers, and the billing country and region, because your accounts and tax records need them. Saved cards stay at Stripe, so a plan or subscription still running is still charged. An erased customer shows *This customer's personal data was erased* and can't be edited.

Erasing a customer doesn't delete their account. If they also want the account gone, delete the user under **Security > Users**.

Each download and erasure is listed under **Privacy** with when it happened and who did it.

## How long guest data is kept

Under **Settings > Commerce > Customers**, **Privacy** sets how long you keep guests' personal data. Once a day the store applies it:

| Setting | What happens | Default |
| --- | --- | --- |
| **Erase inactive guests after (months)** | A guest customer who hasn't bought or paid in this many months is erased, as above. | 24 |
| **Anonymize guest orders after (months)** | A paid or canceled guest order this old loses its name, email, phone and billing address but the country and region, and its checkouts lose the same details, the IP address and browser details, and what was typed into the checkout steps. Orders still awaiting payment keep them until they're settled. | 12 |

Enter **0** to keep the data. Customers with an account are kept while the account exists; erase them on request instead.

## My Account

A signed-in customer manages their own details under **My Account**, with the **Manage own customer profile and addresses** permission. Every signed-in user has it by default.

- The top of the page edits how the store addresses them: whether they buy for **Myself** or **A business**, their name, company, tax id, phone and marketing consent. Their email is shown but is changed on their account.
- **Addresses** lists their address book, where they add, edit and delete addresses the same way as above.

The first time a user opens **My Account**, their customer record is made from their account, just as it is on their first order.

On the storefront the same details are on **Account details** and **Addresses** in the buyer's account; see [The buyer's account](carts-and-orders.md#the-buyers-account). With Store Credit, Loyalty Points or Referrals on, the account also has **Store credit**, **Rewards** and **Refer a friend**.

## Try it with the sample store

The **Product listing: online store sample** recipe (**Configuration > Recipes**) enables Customers and adds two sample customers: *Sam Carter*, a person with a home and a work address, and *Contoso Supplies*, a business in Canada with a tax id. Open them under **Commerce > Customers** to see the details, address book and privacy actions.

## Related pages

- [Carts and orders](carts-and-orders.md)
- [Transactions](transactions.md)
- [Store credit, loyalty points and referrals](store-credit-and-rewards.md)

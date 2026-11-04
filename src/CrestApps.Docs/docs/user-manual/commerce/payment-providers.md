---
sidebar_label: Payment Providers
title: Payment Providers
description: Connect your Stripe account so buyers can pay by card, Apple Pay or Google Pay, offer Pay Later so buyers can confirm now and settle the balance afterwards, and try the checkout on a test site with test payments.
technical_manual:
  - modules/payments
  - modules/pay-later
  - modules/checkout
---

A **payment provider** is how the money for a purchase is collected. Your site offers two:

- **Stripe** takes card payments online, and Apple Pay or Google Pay on devices that have them. The payment is confirmed while the buyer waits.
- **Pay Later** lets the buyer confirm the purchase now and pay afterwards. Nothing is charged at checkout; the amount owed is recorded as an outstanding balance.

For trying out your setup there is also **Test payment**, which confirms a payment without taking any money. It is only available on test and development sites; see [Try the checkout with test payments](#try-the-checkout-with-test-payments).

Every provider that is turned on appears as a choice on the **Payment** step of the [checkout](checkout.md). When both are offered, the card option is selected first.

<YouTube video="commerce" title="Narrated video of connecting Stripe and choosing currencies" start={100} end={131} />

## Connect Stripe

<YouTube video="stripe-wallets-cards-disputes" title="Entering your Stripe keys" start={89} end={127} />
This video walks through connecting Stripe, the offline methods, transactions, refunds and webhooks:

<YouTube video="payments" title="Video overview of payments" />

| | |
| --- | --- |
| **Menu** | Settings > Payments > Stripe |
| **Permission** | Manage Stripe Settings |
| **Feature** | Stripe |

<AskYourAdmin />

You need a Stripe account. Your keys are on the Stripe dashboard under **Developers > API keys**; the **Test mode** switch on that page chooses between test keys and live keys. Each environment (test and production) has its own keys and its own connection, so you can connect the test account first and switch to production when you are ready.

1. Open **Settings > Payments > Stripe**.
2. Leave **Enable Production** cleared to set up the test environment, or tick it to set up production. The fields below change to match.
3. Paste the **Publishable Key** and the **Secret Key** for that environment.
4. Click **Save**. Saving a change reloads the site's settings.
5. Under **Status**, click **Connect**. The button only appears once a secret key is saved. If you change a key and have not saved yet, the page shows *You changed the keys. Save the settings first, then connect with the new keys.* instead of the button.
6. A green message confirms the connection, and **Status** now shows *Connected to the test Stripe account* (or *production Stripe account*) followed by the Stripe account number.

### Values configured from the application settings

Your administrator or hosting team can supply the Stripe keys, and whether production is used, outside this page, in the site's application settings. This is common on test and demonstration sites that are created automatically, and on sites that keep their keys in a secure vault.

When a value comes from the application settings:

- The page shows the note *Configured from the application settings.* under it, and the box or check box cannot be changed. A banner at the top of the page says that some values are configured from the application settings.
- A configured publishable key, and the **Enable Production** choice, are shown as they are. A configured secret key or webhook signing secret is never shown.
- That value is used instead of anything saved on this page, and saving the page does not change it. To change it, ask the person who manages the site's application settings.
- **Connect** works as usual with a configured secret key. If the webhook signing secret is configured too, **Connect** does not create a webhook: your developer registers it in the Stripe dashboard instead, and the message after connecting says so.
- **Disconnect** clears only what this page saved. Configured keys stay in use until they are removed from the application settings.

Values that are not configured work exactly as described on the rest of this page.

### What Connect does

<YouTube video="stripe-wallets-cards-disputes" title="What Connect does" start={127} end={174} />

When you click **Connect**, your site:

1. Checks the saved secret key with Stripe. If Stripe rejects it, an error says why and nothing else changes.
2. Remembers which Stripe account the key belongs to.
3. Creates a **webhook** at Stripe: an address on your site that Stripe calls to report payments, refunds, failed payments and disputes. Any webhook left over from an earlier connection is removed first. If the webhook cannot be created, for example because your site cannot be reached from the internet, the connection still succeeds and card payments still work; the message tells you so. Make the site publicly reachable and connect again so Stripe can report events.
4. Registers your site's domain with Stripe for **Apple Pay and Google Pay**. When that works, the success message ends with *Apple Pay and Google Pay are enabled for* your domain name. A site that is only reachable on your own computer (such as *localhost*) cannot be registered; card payments work anyway.

### Disconnect a Stripe account

Click **Disconnect** under **Status** and confirm. The confirmation warns that the keys are deleted. Your site removes the webhook it created and **clears the keys** for that environment, so Stripe payments stop until you enter the keys again, save and click **Connect**. Disconnecting one environment leaves the other one as it is.

### Stripe fields

<YouTube video="payments" title="Video of the Stripe settings" start={91} end={134} />

| Field | What it does |
| --- | --- |
| **Enable Production** | Ticked: the production (live) keys and account are used and real money is taken. Cleared: the test keys and account are used. While the test environment is in use, buyers see *Stripe is in test mode. No real payment will be taken.* on the payment step. |
| **Production Publishable Key** / **Test Publishable Key** | The public key the card form uses. It must start with `pk_live_` (production) or `pk_test_` (test). Without it, buyers who choose card see *Card payment is not fully configured* instead of the card form. |
| **Production Secret Key** / **Test Secret Key** | The private key your site uses to talk to Stripe. It must start with `sk_live_` or `sk_test_`. It is stored encrypted and never shown again; once saved, the box reads *A secret key is stored. Provide a new value to replace it.* Leave it empty to keep the stored key. |
| **Status** | Shows the connection and the **Connect** or **Disconnect** button for the environment you are looking at. |
| **Advanced — webhook signing secret** | Expands to show the **Production Webhook Signing Secret** or **Test Webhook Signing Secret**. It is filled in for you when you connect. Leave it alone unless your developer asks you to enter one; it is only entered by hand when the site cannot be reached from the internet. |

:::tip[Apple Pay and Google Pay]
There is nothing to turn on. After a successful **Connect** on a public domain, the wallet buttons appear above the card form, followed by *or pay by card*, when all of these are true: the buyer's device and browser have a wallet set up, and the checkout has an amount greater than zero due now. On a computer with no wallet, buyers simply see the card form. If you move the site to a new domain name, disconnect, re-enter and save your keys, and connect again so the new domain is registered.
:::

## Offline payments: Pay Later, bank transfer and cash on delivery

<YouTube video="payments" title="Video of the offline payment settings" start={134} end={172} />

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Offline payments |
| **Permission** | Manage transaction settings |
| **Feature** | Pay Later |

<AskYourAdmin />

The **Pay Later** feature adds three ways to buy now and pay afterwards. None of them takes money at checkout: each records what the buyer owes as a **transaction** with an outstanding balance, which you mark paid when the money arrives. Turn each on or off under **Settings > Commerce > Offline payments**.

| Method | Offered | What the buyer is told |
| --- | --- | --- |
| **Pay Later** | When you turn it on. On every checkout that buys something new, subscriptions included. | *Confirm now and pay later.* |
| **Bank transfer** | Off until you turn it on and add your bank details. One-time purchases only. | *Place the order now and transfer the money from your bank.* Your bank details, at checkout, on the confirmation and on the order's page, with the order number as the reference to quote. |
| **Cash on delivery** | Off until you turn it on. Only for orders that are delivered: an order of downloads or services doesn't offer it. | *Pay when your order is delivered.* and your note, such as *Our courier takes cash and cards.* |

The confirmation of an order paid by bank transfer or on delivery reads *Thank you — your order is placed.* rather than *your purchase is complete*. While money is still owed, the order's page shows **Payment due** with the amount, how to pay and the due date, for the buyer, and **Record payment**, for you. Click it, enter the amount received and a reference, such as the transfer's, and confirm: the balance is settled and the notice goes away.

### Pay Later

Pay Later isn't offered when a customer pays a balance they already owe, from **My Transactions** or from the link in a reminder: that balance has to be paid now. When the buyer picks it, the payment step explains: *Your order will be confirmed now and the balance recorded as outstanding.* and *You will be able to pay it from your account, and we will send you a reminder before it is due.*

When the buyer clicks **Pay now** with Pay Later selected:

1. The purchase is confirmed straight away. No money changes hands.
2. The amount owed is recorded as a **transaction** with an outstanding balance, so you can follow it up and the buyer can pay it later. See [Transactions](transactions.md).
3. For a subscription or another recurring purchase, a new outstanding balance is recorded at the start of each billing period. A free trial or a delayed start owes nothing until it ends.
4. A buyer who checked out without an account is recorded as a guest. Reminders can only reach a guest whose email address the checkout collected.

### Set up the offline methods

Open **Settings > Commerce > Offline payments**, change what you need, and click **Save**.

| Field | What it does |
| --- | --- |
| **Offer Pay Later at checkout** | Whether Pay Later is offered. Off until you tick it. |
| **Net term (days)** | How many days after the purchase a Pay Later balance falls due. The due date drives the reminders. Enter 0 to record the balance without a due date. The default is 30. |
| **Offer Bank transfer at checkout** | Whether Bank transfer is offered. It can't be turned on without bank details. |
| **Bank details** | The account name, bank, account number or IBAN, and anything else buyers need to transfer the money. Shown exactly as you type it, line by line. |
| **Due within (days)** | When a transfer falls due, which drives the reminders. 0 for no due date. The default is 7. |
| **Offer Cash on delivery at checkout** | Whether Cash on delivery is offered, for orders that are delivered. |
| **Note for buyers** | Optional, shown under Cash on delivery at checkout. |

:::note[Refunds]
Pay Later collects no money, so there is nothing to refund through it. Money the buyer later pays against the balance is handled like any other payment; see [Payments and refunds](payments-and-refunds.md).
:::

## Saved cards

<YouTube video="stripe-wallets-cards-disputes" title="Wallets and saved cards" start={174} end={211} />

With Stripe connected, a signed-in buyer paying for a one-time purchase can tick **Save this card for next time** under the card fields. Next time, the payment step offers **Pay with**: their saved cards, such as *VISA ending in 4242 expires 12/2034*, and **A new card**. Choosing a saved card hides the card fields; the buyer clicks **Pay now** and approves the payment if their bank asks.

The cards are kept by Stripe, not on your site. A buyer sees and removes theirs under **Payment methods** in their account. Guests, subscriptions and installment plans don't offer the box: a subscription or a plan keeps its card for its own charges.

### Cards for automatic payments

When a signed-in buyer pays something that keeps the card for later charges, such as the first payment of an installment plan or a booking deposit, the card is also listed under **Cards for automatic payments** on their **Payment methods** page. Your site can then charge it later without the buyer at the checkout, for example to renew something they agreed to. The first card they keep is their default, marked **Default**.

- **Make default** picks the card charged when a payment doesn't name another.
- **Remove** stops the card from being offered for new automatic payments. An installment plan already running keeps charging the card it was set up with.

A charge that doesn't go through, because the card was declined or the bank asked the cardholder to approve it, shows on the transaction's timeline as **Payment failed**. Nothing was taken, and the buyer pays it another way, such as from the invoice's pay link.

## Try the checkout with test payments

| | |
| --- | --- |
| **Menu** | None. The method appears on the **Payment** step of the checkout. |
| **Permission** | None |
| **Feature** | Test payments |

<AskYourAdmin />

**Test payment** lets you and your team go through a whole purchase, pay it, refund it and check what happens afterwards, without a Stripe account and without any money changing hands. It is meant for development and test sites. On a live site it does not appear, even when the feature is turned on, unless your developer has deliberately allowed it.

When the feature is on, the **Payment** step offers **Test payment**. When you pick it, the payment step shows *Test payment: no money moves, and the payment is confirmed at once.* Click **Pay now** and the purchase is confirmed straight away, exactly as a card payment would be: the confirmation page opens, an outstanding balance you paid is marked paid, and the payment appears under **Commerce > Payments**.

You can refund a test payment from **Commerce > Payments** like any other payment (see [Payments and refunds](payments-and-refunds.md)). The refund succeeds at once and shows on the transaction.

The amount being paid decides what happens, so you can also try out what a buyer sees when something goes wrong:

| Amount ends in | What happens |
| --- | --- |
| **.02**, for example 10.02 | The payment is declined. The buyer stays on the payment step and sees *The payment was declined. Please try again or choose another payment method.* |
| **.05**, for example 10.05 | The payment stays pending for about two minutes. The buyer sees *Your payment is still being processed.*, and the purchase completes on its own a few minutes later. |
| Anything else | The payment is confirmed. |

:::warning[Not for live sites]
Anyone who picks **Test payment** completes the purchase without paying. If you see it on your live site, ask your administrator to turn the **Test payments** feature off.
:::

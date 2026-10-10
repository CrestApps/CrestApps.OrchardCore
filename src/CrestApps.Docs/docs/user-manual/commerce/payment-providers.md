---
sidebar_label: Payment Providers
title: Payment Providers
description: Connect your Stripe account so buyers can pay by card, Apple Pay or Google Pay, and offer Pay Later so buyers can confirm now and settle the balance afterwards.
technical_manual:
  - modules/payments
  - modules/pay-later
  - modules/checkout
---

A **payment provider** is how the money for a purchase is collected. Your site offers two:

- **Stripe** takes card payments online, and Apple Pay or Google Pay on devices that have them. The payment is confirmed while the buyer waits.
- **Pay Later** lets the buyer confirm the purchase now and pay afterwards. Nothing is charged at checkout; the amount owed is recorded as an outstanding balance.

Every provider that is turned on appears as a choice on the **Payment** step of the [checkout](checkout.md). When both are offered, the card option is selected first.

<video controls preload="metadata" width="100%" poster="/img/docs/commerce-payment-setup.jpg" aria-label="Narrated video of connecting Stripe and choosing currencies">
  <source src="/img/docs/commerce-payment-setup.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/commerce-payment-setup.vtt" srcLang="en" label="English" default />
</video>

## Connect Stripe

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

### What Connect does

When you click **Connect**, your site:

1. Checks the saved secret key with Stripe. If Stripe rejects it, an error says why and nothing else changes.
2. Remembers which Stripe account the key belongs to.
3. Creates a **webhook** at Stripe: an address on your site that Stripe calls to report payments, refunds, failed payments and disputes. Any webhook left over from an earlier connection is removed first. If the webhook cannot be created, for example because your site cannot be reached from the internet, the connection still succeeds and card payments still work; the message tells you so. Make the site publicly reachable and connect again so Stripe can report events.
4. Registers your site's domain with Stripe for **Apple Pay and Google Pay**. When that works, the success message ends with *Apple Pay and Google Pay are enabled for* your domain name. A site that is only reachable on your own computer (such as *localhost*) cannot be registered; card payments work anyway.

### Disconnect a Stripe account

Click **Disconnect** under **Status** and confirm. Your site removes the webhook it created and **clears the keys** for that environment, so Stripe payments stop until you enter the keys again, save and click **Connect**. Disconnecting one environment leaves the other one as it is.

### Stripe fields

| Field | What it does |
| --- | --- |
| **Enable Production** | Ticked: the production (live) keys and account are used and real money is taken. Cleared: the test keys and account are used. While the test environment is in use, buyers see *Stripe is in test mode. No real payment will be taken.* on the payment step. |
| **Checkout Mode** | **Payment Elements (on-site)** or **Hosted Checkout (redirect)**. The checkout on this site collects card details on your own page, so leave this at **Payment Elements (on-site)**. |
| **Production Publishable Key** / **Test Publishable Key** | The public key the card form uses. It must start with `pk_live_` (production) or `pk_test_` (test). Without it, buyers who choose card see *Card payment is not fully configured* instead of the card form. |
| **Production Secret Key** / **Test Secret Key** | The private key your site uses to talk to Stripe. It must start with `sk_live_` or `sk_test_`. It is stored encrypted and never shown again; once saved, the box reads *A secret key is stored. Provide a new value to replace it.* Leave it empty to keep the stored key. |
| **Status** | Shows the connection and the **Connect** or **Disconnect** button for the environment you are looking at. |
| **Advanced — webhook signing secret** | Expands to show the **Production Webhook Signing Secret** or **Test Webhook Signing Secret**. It is filled in for you when you connect. Leave it alone unless your developer asks you to enter one; it is only entered by hand when the site cannot be reached from the internet. |

:::tip[Apple Pay and Google Pay]
There is nothing to turn on. After a successful **Connect** on a public domain, the wallet buttons appear above the card form, followed by *or pay by card*, when all of these are true: the buyer's device and browser have a wallet set up, and the checkout has an amount greater than zero due now. On a computer with no wallet, buyers simply see the card form. If you move the site to a new domain name, disconnect, re-enter and save your keys, and connect again so the new domain is registered.
:::

## Offer Pay Later

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Pay Later |
| **Permission** | Manage transaction settings |
| **Feature** | Pay Later |

<AskYourAdmin />

Once the **Pay Later** feature is on, **Pay Later** is offered on every checkout, with the note *Confirm now and pay later.* There is no separate on or off switch. When the buyer picks it, the payment step explains: *Your order will be confirmed now and the balance recorded as outstanding.* and *You will be able to pay it from your account, and we will send you a reminder before it is due.*

When the buyer clicks **Pay now** with Pay Later selected:

1. The purchase is confirmed straight away. No money changes hands.
2. The amount owed is recorded as a **transaction** with an outstanding balance, so you can follow it up and the buyer can pay it later. See [Transactions](transactions.md).
3. For a subscription or another recurring purchase, a new outstanding balance is recorded at the start of each billing period. A free trial or a delayed start owes nothing until it ends.
4. A buyer who checked out without an account is recorded as a guest. Reminders can only reach a guest whose email address the checkout collected.

### Set the payment term

1. Open **Settings > Commerce > Pay Later**.
2. Enter the **Net term (days)** and click **Save**.

| Field | What it does |
| --- | --- |
| **Net term (days)** | How many days after the purchase a Pay Later balance falls due. The due date drives the reminders. Enter 0 to record the balance without a due date. The default is 30. |

:::note[Refunds]
Pay Later collects no money, so there is nothing to refund through it. Money the buyer later pays against the balance is handled like any other payment; see [Payments and refunds](payments-and-refunds.md).
:::

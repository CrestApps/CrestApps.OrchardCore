# Features

## Stripe

Provides payment processing integration with [Stripe.com](https://stripe.com).

### Configuration

Enable the `Stripe` feature in your app. Navigate to the admin dashboard >> `Settings` >> `Payments` >> `Stripe` to configure the Stripe integrations. Follow the instructions listed in the settings page for guidance.

### Checkout Mode

Stripe can collect payment using two integration models, selectable from the Stripe settings page under **Checkout Mode**:

- **Payment Elements (on-site)** — the original integration. Card details are collected on your own site using Stripe Elements together with Payment/Setup Intents that are confirmed in the browser. This mode supports products that mix multiple billing intervals and up-front one-time fees.
- **Hosted Checkout (redirect)** — the integration Stripe currently recommends. The customer is redirected to a secure Stripe-hosted Checkout page created from a [Checkout Session](https://docs.stripe.com/payments/checkout). This minimizes your PCI scope. Because a single Checkout Session maps to a single Stripe subscription, this mode is used for products that have a **single billing interval** and **no separate up-front fee**. When a product is not eligible for hosted checkout, the flow automatically renders the Payment Elements experience instead.

Both modes reuse the same webhook-driven completion pipeline, so switching between them does not change how a completed subscription is recorded.

### Local Testing

To test webhooks with Stripe and let Stripe ping back your localhost app, you can use a tool like `stripe-cli`. The `stripe-cli` tool allows you to forward webhook events from Stripe to your local development server. Here's how you can set it up:

1. Enable the `Stripe` feature in your app and configure it's settings as mentioned in the [Configuration](#configuration) section.
2. If you are using the `stripe-cli`, create a webhook in the Stripe account to `https://github.com/stripe/stripe-cli` endpoint.
3. **Install `stripe-cli`**:
   - Follow the instructions listed on [Get started with the Stripe CLI](https://docs.stripe.com/stripe-cli#install) page.
   
4. **Login to your Stripe account**:
   - After installing `stripe-cli`, log in to your Stripe account by running:
     ```sh
     stripe login
     ```
   - This command will open a browser window for you to authenticate your Stripe account.

5. **Forward webhooks to your localhost**:
   - To start forwarding webhooks from Stripe to your local server, use the following command:
     ```sh
     stripe listen --forward-to https://localhost:your-port/stripe/webhook
     ```
   - Replace `your-port` with the port your local server is running on (e.g., `5000`).

   For example, if your local server is running on port `5000`, you would run:
   ```sh
   stripe listen --forward-to https://localhost:5000/stripe/webhook
   ```

By using `stripe-cli`, you can easily test how your application handles Stripe webhooks locally before deploying it to a live environment.

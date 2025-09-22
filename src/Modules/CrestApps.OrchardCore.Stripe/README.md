# Features

## Stripe

Provides payment processing integration with [Stripe.com](https://stripe.com).

### Configuration

Enable the `Stripe` feature in your app. Navigate to the admin dashboard >> `Settings` >> `Payments` >> `Stripe` to configure the Stripe integrations. Follow the instructions listed in the settings page for guidance.

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

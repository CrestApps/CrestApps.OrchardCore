# Features

## Subscriptions

Provides a subscription checkout flow that lets visitors purchase recurring subscription products.

### Payment providers and extensibility

The subscription flow ends with a **Payment** step that can offer more than one payment provider. Which providers appear is controlled by the `PaymentMethodOptions.PaymentMethods` registry, so new providers (for example, PayPal) can be added without changing the core module.

To add a payment provider:

1. **Register the payment method** so it shows up as an option on the Payment step:

   ```csharp
   services.Configure<PaymentMethodOptions>(options =>
   {
       options.PaymentMethods["PayPal"] = new PaymentMethod
       {
           Title = "PayPal",
           HasProcessor = true,
       };
   });
   ```

2. **Render the provider's payment UI** with a display driver grouped by the provider key. The driver is only invoked when its provider is the selected payment method:

   ```csharp
   services.AddScoped<IDisplayDriver<SubscriptionFlowPaymentMethod>, PayPalPaymentSubscriptionFlowDisplayDriver>();
   ```

   ```csharp
   public override IDisplayResult Edit(SubscriptionFlowPaymentMethod method, BuildEditorContext context)
       => Initialize<PayPalPaymentMethodViewModel>("PayPalPaymentMethod_Edit", model => { /* ... */ })
           .Location("Content")
           .OnGroup("PayPal");
   ```

3. **Collect payment and finalize the flow.** Populate the shared payment session (`SubscriptionPaymentSession`) with the collected payment(s) so the provider-agnostic `PaymentSubscriptionHandler.CompletingAsync` can validate the amounts and complete the subscription. Webhook events are mapped into the payment session through `IPaymentEvent` handlers.

The Stripe feature is a full reference implementation of this pattern.

### Stripe checkout modes

When the Stripe feature is enabled it contributes two ways to collect payment, selectable from the Stripe settings page (**Checkout Mode**):

- **Payment Elements (on-site)** — collects card data on your site.
- **Hosted Checkout (redirect)** — redirects the customer to a Stripe-hosted Checkout page.

#### Hosted Checkout finalization and limitations

Hosted Checkout creates a Stripe [Checkout Session](https://docs.stripe.com/payments/checkout) and redirects the browser to it. On return, the `Subscription/CheckoutReturn` action retrieves the session from Stripe, confirms it is complete and paid, records the Stripe subscription against the local session and finalizes the flow through the same completion pipeline used by Payment Elements.

Because a single Checkout Session maps to a single Stripe subscription, Hosted Checkout only supports products that have:

- a **single billing interval**, and
- **no separate up-front one-time fee**.

Products that do not meet these constraints automatically fall back to the Payment Elements experience.

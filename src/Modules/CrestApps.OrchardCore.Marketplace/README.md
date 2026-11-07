# Marketplace

Sells other sellers' products: vendors, the commission the store keeps on each of their sales, what they've earned held in escrow until it's released (after a hold, and once the order is fulfilled), and payouts, with refunds taken back from what they're owed.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Marketplace | `CrestApps.OrchardCore.Marketplace` | Sells other sellers' products: vendors, the commission the store keeps on each of their sales, what they've earned held in escrow until it's released (after a hold, and once the order is fulfilled), and payouts, with refunds taken back from what they're owed. |
| Marketplace payouts with Stripe Connect | `CrestApps.OrchardCore.Marketplace.StripeConnect` | Pays vendors to their own Stripe accounts: each vendor sets one up through Stripe's onboarding from their vendor page, and what they've earned on an order is transferred to it from that order's payment. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)
- [`CrestApps.OrchardCore.Orders`](../CrestApps.OrchardCore.Orders/README.md)
- [`CrestApps.OrchardCore.Stripe`](../CrestApps.OrchardCore.Stripe/README.md)

## Documentation

See [Marketplace](../../CrestApps.Docs/docs/modules/marketplace.md) in the technical manual.

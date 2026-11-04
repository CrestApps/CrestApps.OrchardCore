# Orders

Checks carts out through the checkout and records what was bought, with order lists for the store and its customers.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Orders | `CrestApps.OrchardCore.Orders` | Checks carts out through the checkout and records what was bought, with order lists for the store and its customers. |
| Gift Cards | `CrestApps.OrchardCore.Orders.GiftCards` | Sells gift cards and lets buyers pay part or all of an order with one, under Commerce > Gift cards. |
| Returns | `CrestApps.OrchardCore.Orders.Returns` | Lets buyers ask to return items of their orders, for a refund or an exchange, and the store approve, receive, restock and refund them under Commerce > Returns. |
| Orders API | `CrestApps.OrchardCore.Orders.Api` | Lets a headless storefront or app check out the cart, and the buyer see their orders and ask for returns, over the commerce JSON API; staff list, refund and cancel orders. Enable OpenAPI to document it. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)
- [`CrestApps.OrchardCore.Carts`](../CrestApps.OrchardCore.Carts/README.md)
- [`CrestApps.OrchardCore.Checkout`](../CrestApps.OrchardCore.Checkout/README.md)
- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)
- `OrchardCore.Title`
- `CrestApps.OrchardCore.Carts.Api`
- `CrestApps.OrchardCore.Checkout.Api`

## Extension points

- `IGiftCardStore` (CrestApps.OrchardCore.Orders.Core): Persists gift cards.
- `ICommerceEmailSender` (CrestApps.OrchardCore.Orders.Core): Sends the store's emails to its customers, such as an order's confirmation, a booking or a gift card: written in the email's culture, passed through the store's template of its kind and the store's layout, and sent, with a send that fails logged.
- `IFulfillmentLocationProvider` (CrestApps.OrchardCore.Orders.Core): The places orders are fulfilled from, such as the warehouses and shops inventory is kept at, for features that let an order name where it's fulfilled from, such as store pickup.
- `IOrderEmailSectionProvider` (CrestApps.OrchardCore.Orders.Core): Adds a section to an order's confirmation email, such as the links to download what was bought.
- `IOrderEmailTemplates` (CrestApps.OrchardCore.Orders.Core): The store's own templates for the emails about orders: each email's subject and body can be written over, with the email the store would send as `{{ Content }}`, and a layout around them all.
- and 9 more contracts in the same projects.

## Documentation

See [Carts and Orders](../../CrestApps.Docs/docs/modules/carts-and-orders.md) in the technical manual.

# Carts

Lets visitors add products sold online to a cart from listings and product pages. Enable Orders to check the cart out.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Carts | `CrestApps.OrchardCore.Carts` | Lets visitors add products sold online to a cart from listings and product pages. Enable Orders to check the cart out. |
| Wishlists | `CrestApps.OrchardCore.Carts.Wishlists` | Lets signed-in shoppers save products for later from the product page, and find them under Wishlist in their account, each with its add to cart. |
| Cart API | `CrestApps.OrchardCore.Carts.Api` | Lets a headless storefront or app read and change the caller's cart over the commerce JSON API. A guest's cart travels in the X-Cart-Token header. Enable OpenAPI to document it. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)
- [`CrestApps.OrchardCore.Products`](../CrestApps.OrchardCore.Products/README.md)
- `OrchardCore.Contents`

## Extension points

- `ICartStore` (CrestApps.OrchardCore.Carts.Core): Stores carts in their own YesSql collection.
- `ICartActionProvider` (CrestApps.OrchardCore.Carts.Core): Offers something to do with a cart besides checking out, such as asking for a quote; the cart page shows each as a link under the Checkout button.
- `ICartLineHandler` (CrestApps.OrchardCore.Carts.Core): Gives cart lines details beside the product, such as a departure date and the travelers on it, or the add-ons chosen: checks them when the line is added, prices and describes them whenever the cart is priced, and they are copied onto the order line.
- `ICartService` (CrestApps.OrchardCore.Carts.Core): The current visitor's cart: finding it, changing it, and pricing it.

## Documentation

See [Carts and Orders](../../CrestApps.Docs/docs/modules/carts-and-orders.md) in the technical manual.

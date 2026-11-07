# Shipping

Charges for shipping at checkout, by zone, with flat, table and free rates from product weights and shipping classes, under Commerce > Shipping.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Shipping | `CrestApps.OrchardCore.Shipping` | Charges for shipping at checkout, by zone, with flat, table and free rates from product weights and shipping classes, under Commerce > Shipping. |
| EasyPost | `CrestApps.OrchardCore.Shipping.EasyPost` | Quotes UPS, USPS, FedEx, DHL and other carriers' rates at checkout, buys and prints their labels from an order's shipments, and follows the parcels, through an EasyPost account. |
| Shipping API | `CrestApps.OrchardCore.Shipping.Api` | Lets a headless storefront or app quote shipping and choose a rate or a pickup location at checkout, and staff ship orders, over the commerce JSON API. Enable OpenAPI to document it. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)
- [`CrestApps.OrchardCore.Products`](../CrestApps.OrchardCore.Products/README.md)
- [`CrestApps.OrchardCore.Orders`](../CrestApps.OrchardCore.Orders/README.md)
- [`CrestApps.OrchardCore.Addresses`](../CrestApps.OrchardCore.Addresses/README.md)
- `OrchardCore.Contents`
- `CrestApps.OrchardCore.Checkout.Api`

## Extension points

- `ICarrierProvider` (CrestApps.OrchardCore.Shipping.Core): A carrier provider: a shipping account that quotes the carriers' rates for a parcel and buys their labels, such as EasyPost.
- `ICarrierProviderResolver` (CrestApps.OrchardCore.Shipping.Core): Finds the carrier provider the store chose as its default, on Settings › Commerce › Shipping.
- `IShipmentEventHandler` (CrestApps.OrchardCore.Shipping.Core): Hears about shipments as they change, in the same unit of work: to email the buyer, start a workflow, or tell a carrier.
- `IShipmentService` (CrestApps.OrchardCore.Shipping.Core): Ships orders, in one or several shipments, and keeps each order's fulfillment status.
- `IShippingAddressSource` (CrestApps.OrchardCore.Shipping.Core): Offers addresses a signed-in buyer can ship to besides their own address book, such as the locations of the business account they buy for.
- and 3 more contracts in the same projects.

## Documentation

See [Shipping](../../CrestApps.Docs/docs/modules/shipping.md) in the technical manual.

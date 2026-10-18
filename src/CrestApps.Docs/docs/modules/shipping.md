---
sidebar_label: Shipping
title: Shipping
description: Shipping zones by country and region, the methods each zone offers, the rate providers that price them, and product shipping data.
user_manual: user-manual/commerce/shipping
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Shipping` |
| **Core** | `CrestApps.OrchardCore.Shipping.Core` |
| **Category** | Commerce |
| **Depends on** | Commerce, Products, Addresses, `OrchardCore.Contents` |

The **Shipping** feature prices shipping. Zones say where; each zone's methods say how and what it costs, through rate providers. Products carry their weight and shipping class in the **Shipping** part.

## Model

- **`ShippingZone`** (`INamedCatalog<ShippingZone>`, one document): `Name`, `Position`, `Countries` (ISO 3166-1 alpha-2), `Regions` (ISO 3166-2, such as `US-AK`), `RestOfWorld` and `Methods`. A country with no region in `Regions` is covered whole.
- **`ShippingMethod`** (an `Entity`, inside its zone): `MethodId`, `Name`, `Description`, `ProviderName`, `Enabled`, `Position`, `Currency` (empty for any), `TaxCategoryCode` and `ClassSurcharges` (per unit, by class, matched ignoring case). The provider's settings are in the method's properties: `FlatRateSettings` (`Amount`, `PerItemAmount`), `TableRateSettings` (`Basis`: `Weight`, `Subtotal` or `Quantity`; `Rows` of `From` and `Amount`; `Maximum`) and `FreeShippingSettings` (`MinimumSubtotal`).
- **`ShippingPart`**: `Weight` (one unit, in the site unit), `ShippingClass`, and `VariantWeights` by variant id. `GetWeight(variantId)` falls back to the product's weight, then to zero.
- **`ShippingSettings`** (group `shipping`): `WeightUnit` (`Kilogram` or `Pound`) and `ShippingClasses`.

## Rates

- **`IShippingRateService`**
  - `FindZoneAsync(address)` returns the first zone by `Position` (then name) that covers the address, else the `RestOfWorld` zone, else `null`.
  - `GetRatesAsync(ShippingRateRequest)` asks the provider of each enabled method of that zone, in order. Methods in another currency, or whose provider is missing (logged), are skipped. Class surcharges are added, and amounts are rounded to the currency and never negative.
- **`ShippingRateRequest`**: `Destination` (`Address`), `Currency`, and `Lines` of `ShippingRequestLine` (`ContentItemId`, `VariantId`, `Title`, `Quantity`, `UnitPrice`, `Weight` per unit, `ShippingClass`), with `Subtotal`, `TotalWeight` and `ItemCount`.
- **`ShippingRate`**: `RateId` (the method id, then `:option` for a provider with several), `ZoneId`, `MethodId`, `ProviderName`, `Name`, `Description`, `Amount`, `Currency` and `TaxCategoryCode`.
- **`IShippingRateProvider`**: `Name`, `DisplayName`, `Description`, and `GetRatesAsync(ShippingRateContext)` returning none, one or several rates. `ShippingRateContext` has the `Zone`, `Method` and `Request`, and `CreateRate(amount, option, name)`.
  - Built in: `Flat`, `Table` (the row with the largest `From` at or below the value; none below the first row or above `Maximum`) and `Free` (from `MinimumSubtotal`).
  - A carrier module registers its own provider, and a `DisplayDriver<ShippingMethod>` for its settings that renders only for methods with its `ProviderName`.

## Checkout

- **`ShippingCheckoutHandler`** adds the **Shipping** step (`ShippingConstants.CheckoutStepKey`, order 2, after Contact) to an order's checkout when a line is a good (`ProductType.Good`). On completion it adds a signed-in buyer's new address to their address book when they asked, as their default shipping address if they had none.
- **`ShippingCheckoutFlowDisplayDriver`** renders and posts the step.
  - The address comes from the customer's address book (Customers, optional) or the address editor, with the recipient's name and phone.
  - **`ShippingOrderRateService`** prices the order's goods: their weight and class come from the product version bought, and the currency is the order's.
  - An address posted without a rate chosen for it (or with a rate the address doesn't offer) comes back with its rates and the error *Choose how to ship your order*, so the step finishes only with a rate priced on the server. One rate is chosen for the buyer.
  - The step's billing item is the shipping line (`TaxableKind = Shipping`, the method's `TaxCategoryCode`), then the invoice is rebuilt. The choice is kept as `ShippingCheckoutStep` in `SavedSteps` and as **`CheckoutShippingInfo`** (address, rate, method, zone, name, amount) in the session.
- **Tax destination.** `DefaultCheckoutTaxProfileProvider` now sets `CheckoutTaxProfile.Destination` to the `CheckoutShippingInfo` address, else the `CheckoutContactInfo` billing address. The Contact step rebuilds the invoice when it's saved, so an order that doesn't ship is taxed where it's billed.
- **Orders.** `OrderCheckoutHandler.CompletingAsync` copies `CheckoutShippingInfo` to `Order.ShippingAddress` and `Order.Shipping` (`OrderShipping`: method, rate, zone, provider, name, description, amount). `Order.Anonymize` keeps only the country and region of both addresses. The admin order page, the guest order page and the confirmation email show both.
- The price is the one shown when the buyer chose the method; editing a method later doesn't change checkouts already past the step.

## Shipments

- **`Shipment`** (YesSql, `Shipping` collection, `ShipmentIndex` on order, status and dates): `OrderId`, `OrderNumber`, `Number` (1, 2, … per order), `Lines` (`ShipmentLine`: order `LineId`, `Title`, `Sku`, `Quantity`), `Status` (`Pending`, `Shipped`, `Delivered`, `Returned`), `Carrier`, `TrackingNumber`, `TrackingUrl`, `Note`, and the created, shipped, delivered, returned and emailed times.
- **`IShipmentService`** (Core), in the caller's unit of work:
  - `GetRemainingAsync(order)`: units of each good not in a shipment, ignoring returned ones.
  - `CreateAsync(order, lines, details, ship)`: only for a paid order, never more than is left; pending, or shipped at once.
  - `ShipAsync`, `DeliverAsync` (shipped only), `ReturnAsync` (shipped or delivered; its units can be shipped again), `DeleteAsync` (pending only).
  - Each change sets `Order.FulfillmentStatus` (`OrderFulfillmentStatus`: `Unfulfilled`, `PartiallyFulfilled`, `Fulfilled`, from the units in shipments that left; `null` for an order without goods).
  - A tracking link must be `http` or `https`; an empty one is made from the carrier's template in `ShippingSettings.Carriers` (`{0}` is the escaped number).
- **`IShipmentEventHandler`** (`CreatedAsync`, `ShippedAsync`, `DeliveredAsync`, `ReturnedAsync`, with `ShipmentEventHandlerBase`) runs in the same unit of work. Shipping registers:
  - `ShippedEmailShipmentHandler`: the shipped email (`ShipmentEmailBuilder`), once per shipment (`ShippedEmailSentUtc`), when `SendShippedEmails` is on and an `IEmailService` is set up; a guest gets the order link from `IOrderLinkService`.
  - `ShipmentWorkflowHandler` (with **Workflows**): `ShipmentCreatedEvent`, `ShipmentShippedEvent`, `ShipmentDeliveredEvent`, `ShipmentReturnedEvent`, with the `Order`, the `Shipment`, its number, carrier, tracking number and link and the buyer's email as input, correlated by the shipment id.
- **Order pages.** Orders builds the order with `IDisplayManager<Order>` in the display types `OrderConstants.DisplayTypes.DetailAdmin` (the admin page, also My Orders) and `Detail` (the guest page), and renders the shapes in its pages. `OrderShipmentsDisplayDriver` adds `OrderShipments_DetailAdmin` (the shipments, a new-shipment form and the actions, for **Ship orders and update their shipments**) or `OrderShipments_Detail` (what left, with tracking links, for the buyer).
- **Admin actions** (`ShipmentsAdminController`): `shipping/orders/{orderId}/shipments` (create), and `shipping/shipments/{id}/ship`, `/deliver`, `/return`, `/delete`, each back to the order.

## Admin

- **Commerce > Shipping** (`shipping/zones`, `ShippingZonesController`) lists zones in the order they are tried. A zone's page edits it and lists its methods; **Add method** opens the method editor for a provider (`shipping/zones/{zoneId}/methods/create/{providerName}`).
- The method editor is `IDisplayManager<ShippingMethod>`: `ShippingMethodDisplayDriver` for the common fields, and one driver per provider.
- **Try rates** (`shipping/rates`) runs `IShippingRateService` for an address and an order described by subtotal, weight, items and class.

## Permissions

| Permission | Allows |
| --- | --- |
| **Manage shipping zones, methods and settings** (`ManageShipping`) | Commerce > Shipping and Settings > Commerce > Shipping (registered for the `shipping` settings group). |
| **Ship orders and update their shipments** (`FulfillOrders`) | The Shipments section of an order's page, and its actions. |

Administrators have it by default.

## Recipes and deployments

The **ShippingZones** step creates or updates zones, found by `ItemId` or `Name`. Methods without a `MethodId` get one. Provider settings go in the method's `Properties`, keyed by type name:

```json
{
  "name": "ShippingZones",
  "Zones": [
    {
      "Name": "United States",
      "Countries": ["US"],
      "Methods": [
        { "Name": "Standard", "Description": "3 to 5 business days", "ProviderName": "Flat", "Properties": { "FlatRateSettings": { "Amount": 5, "PerItemAmount": 1 } } },
        { "Name": "Free over $100", "ProviderName": "Free", "Properties": { "FreeShippingSettings": { "MinimumSubtotal": 100 } } }
      ]
    },
    { "Name": "Everywhere else", "RestOfWorld": true, "Position": 99, "Methods": [ { "Name": "International", "ProviderName": "Table", "Properties": { "TableRateSettings": { "Basis": 0, "Rows": [ { "From": 0, "Amount": 20 }, { "From": 2, "Amount": 35 } ] } } } ] }
  ]
}
```

The **Shipping Zones** deployment step (*Commerce*) exports every zone with its methods.

## Not included

Live carrier rates and labels (a carrier module adds an `IShippingRateProvider`), returns beyond marking a shipment returned, postal-code zones, dimensional weight, and pick lists.

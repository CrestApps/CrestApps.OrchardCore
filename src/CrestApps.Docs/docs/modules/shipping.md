---
sidebar_label: Shipping
title: Shipping
description: Shipping zones by country and region, the methods each zone offers, the rate providers that price them, store pickup, shipments, carrier rates and labels through ICarrierProvider and EasyPost, and product shipping data.
user_manual:
  - user-manual/commerce/shipping
  - user-manual/commerce/headless-api
---

| | |
| --- | --- |
| **Features** | `CrestApps.OrchardCore.Shipping`, `CrestApps.OrchardCore.Shipping.EasyPost` (EasyPost), `CrestApps.OrchardCore.Shipping.Api` (Shipping API) |
| **Core** | `CrestApps.OrchardCore.Shipping.Core` |
| **Category** | Commerce |
| **Depends on** | Commerce, Products, Addresses, `OrchardCore.Contents` |

The **Shipping** feature prices shipping. Zones say where; each zone's methods say how and what it costs, through rate providers. Products carry their weight and shipping class in the **Shipping** part.

## Model

- **`ShippingZone`** (`INamedCatalog<ShippingZone>`, one document): `Name`, `Position`, `Countries` (ISO 3166-1 alpha-2), `Regions` (ISO 3166-2, such as `US-AK`), `RestOfWorld` and `Methods`. A country with no region in `Regions` is covered whole.
- **`ShippingMethod`** (an `Entity`, inside its zone): `MethodId`, `Name`, `Description`, `ProviderName`, `Enabled`, `Position`, `Currency` (empty for any), `TaxCategoryCode` and `ClassSurcharges` (per unit, by class, matched ignoring case). The provider's settings are in the method's properties: `FlatRateSettings` (`Amount`, `PerItemAmount`), `TableRateSettings` (`Basis`: `Weight`, `Subtotal` or `Quantity`; `Rows` of `From` and `Amount`; `Maximum`) and `FreeShippingSettings` (`MinimumSubtotal`).
- **`ShippingPart`**: `Weight` (one unit, in the site unit), `ShippingClass`, and `VariantWeights` by variant id. `GetWeight(variantId)` falls back to the product's weight, then to zero.
- **`ShippingSettings`** (group `shipping`): `WeightUnit` (`Kilogram` or `Pound`) and `ShippingClasses`, plus:
  - `PickupLocations` (up to 20 `PickupLocation`: `LocationId`, `Name`, `AddressLine1`, `City`, `Region`, `PostalCode`, `Country`, `Instructions`, `Fee`, and `StockLocationId`, the inventory location its orders take units from, or `null` for any). `ToAddress()` gives the location as an `Address`.
  - `ShipFrom` (an `Address`), `DefaultParcelLength`, `DefaultParcelWidth`, `DefaultParcelHeight` (centimeters for a store weighing in kilograms, inches otherwise) and `PackagingWeight` (added to the items' weight), which carrier quotes and labels use.
- **Provider settings** of the newer providers: `LocalDeliverySettings` (`PostalCodes`, where `981*` covers every code starting with `981` and case, spaces and dashes are ignored, with `Covers(postalCode)`; `Amount`; `FreeFromSubtotal`; `MinimumSubtotal`) and `CarrierRateSettings` (`Carriers`, empty for all; `MaxRates`, 3 by default, 0 for all; `MarkupPercent`; `MarkupAmount`; `FallbackAmount`, `null` to offer nothing when the carriers can't be reached).

## Rates

- **`IShippingRateService`**
  - `FindZoneAsync(address)` returns the first zone by `Position` (then name) that covers the address, else the `RestOfWorld` zone, else `null`.
  - `GetRatesAsync(ShippingRateRequest)` asks the provider of each enabled method of that zone, in order. Methods in another currency, or whose provider is missing (logged), are skipped. Class surcharges are added, and amounts are rounded to the currency and never negative.
- **`ShippingRateRequest`**: `Destination` (`Address`), `Currency`, and `Lines` of `ShippingRequestLine` (`ContentItemId`, `VariantId`, `Title`, `Quantity`, `UnitPrice`, `Weight` per unit, `ShippingClass`), with `Subtotal`, `TotalWeight` and `ItemCount`.
- **`ShippingRate`**: `RateId` (the method id, then `:option` for a provider with several), `ZoneId`, `MethodId`, `ProviderName`, `Name`, `Description`, `Amount`, `Currency` and `TaxCategoryCode`.
- **`IShippingRateProvider`**: `Name`, `DisplayName`, `Description`, and `GetRatesAsync(ShippingRateContext)` returning none, one or several rates. `ShippingRateContext` has the `Zone`, `Method` and `Request`, and `CreateRate(amount, option, name)`.
  - Built in: `Flat`, `Table` (the row with the largest `From` at or below the value; none below the first row or above `Maximum`), `Free` (from `MinimumSubtotal`), `LocalDelivery` (`LocalDeliveryShippingProvider`: one rate when the destination's postal code is covered and the subtotal reaches `MinimumSubtotal`, free from `FreeFromSubtotal`) and `Carrier` (`CarrierShippingRateProvider`, see [Carriers](#carriers)).
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
- **Store pickup.** With `PickupLocations`, the step renders a choice between shipping and each location (`ShippingCheckoutStep.PickupLocationId`). Pickup needs no address or quote: the location's address becomes the shipping address (so the order is taxed there), and the rate is `pickup:{locationId}` with provider `Pickup` (`OrderShipping.PickupProviderName`), the location's fee and its instructions as the description. `CheckoutShippingInfo.LocationId` carries the location's `StockLocationId` to `OrderShipping.LocationId`, which Inventory takes the units from first. A location removed since it was chosen sends the buyer back to choose again.

## Shipments

- **`Shipment`** (YesSql, `Shipping` collection, `ShipmentIndex` on order, status and dates): `OrderId`, `OrderNumber`, `Number` (1, 2, … per order), `Lines` (`ShipmentLine`: order `LineId`, `Title`, `Sku`, `Quantity`), `Status` (`Pending`, `Shipped`, `Delivered`, `Returned`), `Carrier`, `TrackingNumber`, `TrackingUrl`, `Note`, and the created, shipped, delivered, returned and emailed times.
- **`IShipmentService`** (Core), in the caller's unit of work:
  - `GetRemainingAsync(order)`: units of each good not in a shipment, ignoring returned ones.
  - `CreateAsync(order, lines, details, ship)`: only for a paid order, never more than is left; pending, or shipped at once.
  - `ShipAsync`, `DeliverAsync` (shipped only), `ReturnAsync` (shipped or delivered; its units can be shipped again), `DeleteAsync` (pending only).
  - Each change sets `Order.FulfillmentStatus` (`OrderFulfillmentStatus`: `Unfulfilled`, `PartiallyFulfilled`, `Fulfilled`, from the units in shipments that left; `null` for an order without goods).
  - A tracking link must be `http` or `https`; an empty one is made from the carrier's template in `ShippingSettings.Carriers` (`{0}` is the escaped number).
- **`IShipmentEventHandler`** (`CreatedAsync`, `ShippedAsync`, `DeliveredAsync`, `ReturnedAsync`, with `ShipmentEventHandlerBase`) runs in the same unit of work. Shipping registers:
  - `ShippedEmailShipmentHandler`: the shipped email (`ShipmentEmailBuilder`), once per shipment (`ShippedEmailSentUtc`), when `SendShippedEmails` is on and an `IEmailService` is set up; a guest gets the order link from `IOrderLinkService`. For a pickup order (`OrderShipping.IsPickup`) it says the order is ready for pickup, where to collect it and the location's instructions.
  - `ShipmentWorkflowHandler` (with **Workflows**): `ShipmentCreatedEvent`, `ShipmentShippedEvent`, `ShipmentDeliveredEvent`, `ShipmentReturnedEvent`, with the `Order`, the `Shipment`, its number, carrier, tracking number and link and the buyer's email as input, correlated by the shipment id.
- **Pickup orders** go through the same states, shown as *Being got ready*, *Ready for pickup* and *Picked up*, without a carrier or tracking number.
- **Labels and tracking.** `Shipment` adds `LabelUrl`, `LabelCost`, `LabelCurrency`, `TrackingStatus` (the carrier's latest word) and `TrackingEvents` (`ShipmentTrackingEvent`: `Utc`, `Status`, `Message`, `Location`, newest first), shown on both order pages.
- **Order pages.** Orders builds the order with `IDisplayManager<Order>` in the display types `OrderConstants.DisplayTypes.DetailAdmin` (the admin page, also My Orders) and `Detail` (the guest page), and renders the shapes in its pages. `OrderShipmentsDisplayDriver` adds `OrderShipments_DetailAdmin` (the shipments, a new-shipment form and the actions, for **Ship orders and update their shipments**) or `OrderShipments_Detail` (what left, with tracking links, for the buyer).
- **Admin actions** (`ShipmentsAdminController`): `shipping/orders/{orderId}/shipments` (create), and `shipping/shipments/{id}/ship`, `/deliver`, `/return`, `/delete`, each back to the order. Each shipment links its packing slip (the Orders module's `OrdersPackingSlip`, with its lines and number).

## Carriers

- **`ICarrierProvider`** (Core) is a shipping account that quotes carriers' rates and buys their labels: `Name`, `DisplayName`, `IsEnabledAsync()`, `QuoteAsync(CarrierShipmentRequest)` returning a `CarrierQuote` (`QuoteId`, `Rates` of `CarrierRate`: `RateId`, `Carrier`, `Service`, `Amount`, `Currency`, `DeliveryDays`; or an `Error`), and `BuyLabelAsync(quoteId, rateId)` returning a `CarrierLabel` (`Carrier`, `Service`, `TrackingNumber`, `TrackingUrl`, `LabelUrl`, `Amount`, `Currency`, or an `Error`). `CarrierShipmentRequest` has `From`, `To`, a `Reference` and a `CarrierParcel` in ounces and inches (`ToOunces` and `ToInches` convert from the store's units). A provider is a feature: it registers its `ICarrierProvider` and a settings driver for its own tab on **Settings > Commerce > Shipping** (group `shipping`), where it's enabled, as Orchard Core's SMS and email providers are.
- **`ICarrierProviderResolver`** (`DefaultCarrierProviderResolver`) returns the default provider, `CarrierProviderSettings.DefaultProviderName` chosen on the **Providers** tab (`CarrierProviderSettingsDisplayDriver`), while it's enabled, and the enabled providers. Everything that quotes or buys labels goes through it.
- **`CarrierShippingRateProvider`** (`Carrier`) quotes the order's weight plus `PackagingWeight`, in the default box, from `ShipFrom` to the destination; caches a quote for ten minutes per destination and weight; keeps the `CarrierRateSettings.Carriers` and the order's currency; and offers the cheapest `MaxRates` with the markup, named like *USPS Ground Advantage*, with the business days as the description. Without a default carrier provider, a ship-from address or a destination, or when the quote fails (logged), it offers `FallbackAmount` or nothing.
- **Labels** (`ShipmentLabelsAdminController`, `FulfillOrders`): `shipping/shipments/{shipmentId}/label` for a pending shipment that isn't a pickup, prefilled with the items' weight plus the box; `/label/rates` quotes the parcel; `/label/buy` buys the chosen rate, keeps the label on the shipment and ships it with the label's carrier, tracking number and link, which sends the shipped email. `OrderShipmentsDisplayDriver` shows **Buy label** when there's a default carrier provider.
- **EasyPost** (feature `CrestApps.OrchardCore.Shipping.EasyPost`): `EasyPostCarrierProvider` calls EasyPost's API through the `EasyPost` named `HttpClient`. `EasyPostSettings` (the **EasyPost** tab of **Settings > Commerce > Shipping**, for `ManageShipping`) keeps `IsEnabled`, and `ApiKey` and `WebhookSecret` protected with data protection (purpose `EasyPostSettings`). Enabling it while no provider is the default makes it the default; disabling the default clears it. Saving it with a public https `BaseUrl` registers the webhook through EasyPost's API (`RegisterWebhookAsync`) with a secret made for it, again when the address or the key changes, and keeps `WebhookId` and `WebhookUrl`; the store never enters a secret. `POST shipping/easypost/webhook` (`EasyPostWebhook`, `EasyPostWebhookController`) accepts only bodies of up to 512 KB signed with the secret (`X-Hmac-Signature`, HMAC-SHA256); `EasyPostTrackingUpdater` applies `tracker.created` and `tracker.updated` events to the shipment with that tracking number, and delivers a shipped shipment when the tracker says `delivered`.

## API

The **Shipping API** feature (`CrestApps.OrchardCore.Shipping.Api`, it needs Shipping and the Checkout API) maps into the commerce JSON API at `api/commerce/v1`:

- The checkout's Shipping step, through `ShippingCheckoutStepService`: `checkout/{id}/pickup-locations`, `checkout/{id}/shipping-rates` (a quote for an address) and `PUT checkout/{id}/shipping` (a pickup location, or an address and its rate).
- `me/orders/{orderId}/shipments`, the signed-in buyer's tracking, with **View own orders**.
- The staff fulfillment endpoints, through `IShipmentService` with `FulfillOrders`: `orders/{orderId}/shipments` (what's left to ship, and packing a shipment), and `shipments/{shipmentId}/ship` and `/deliver`.

See [Headless commerce API](../user-manual/commerce/headless-api.md) for the endpoints, the conventions they share, and how apps sign in.

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

Return labels; carriers other than through EasyPost (another account implements `ICarrierProvider`); a parcel other than the default box when quoting at checkout; postal-code zones (local delivery lists postal codes within a zone); dimensional weight; and pick lists. Buyer returns are the Orders module's [Returns](carts-and-orders#returns) feature.

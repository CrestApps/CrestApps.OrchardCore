# Fulfillment Partners

Connects a store to the outside companies that make and ship its products for it, such as print-on-demand services. This module owns the **Settings > Commerce > Fulfillment partners** page, the **Commerce > Fulfillment requests** list and the `IFulfillmentProvider` contract. Each company is a separate provider module that adds its connection section to the settings page.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Fulfillment Partners | `CrestApps.OrchardCore.Fulfillment` | The settings page that lists the partners and whether each is connected, the order card that sends paid orders to them, the Fulfillment requests list, notifications, and automatic sending. |

No partner ships in this repository: a partner is a module that implements `IFulfillmentProvider` (see the technical manual), so a site adds the partners it uses.

## Installation

Enable **Fulfillment Partners** under **Configuration > Features**, or with the `Feature` step of a recipe. It enables Shipping too, and enabling a provider's feature enables it.

## Configuration

Open **Settings > Commerce > Fulfillment partners**. The page needs the **Manage fulfillment partners and their credentials** permission (`ManageFulfillmentPartners`), which the Administrator role has. Sending orders and the Fulfillment requests list need Shipping's **Ship orders and update their shipments** permission (`FulfillOrders`).

## What it does

- Connects the store to each partner and checks the connection.
- Gives each line of an order to the one partner that makes it, and asks a person when two partners claim the same line.
- Sends only what is left to ship, and reserves what it sends as a pending Shipping shipment, so typing the tracking number on it fulfills the order.
- Adds a **Fulfillment partners** card to an order's admin page, with a button that sends a paid order to its partners, and shows what each partner was sent and answered.
- Lists every request under **Commerce > Fulfillment requests**, with a status filter, a search by order number and **Send again**.
- With **Send paid orders automatically** on, records each paid order for the partners as it is paid; a background task sends it within a few minutes.
- Sends an order once. When the outcome is not clear (no answer within two minutes, a server error, or a send that was interrupted), the order is marked **Needs attention**, the people who ship orders are notified, and it is never sent again by itself.

Tracking numbers and cancellations do not come back from partners yet.

## Dependencies

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)
- [`CrestApps.OrchardCore.Orders`](../CrestApps.OrchardCore.Orders/README.md)
- [`CrestApps.OrchardCore.Shipping`](../CrestApps.OrchardCore.Shipping/README.md)

## Documentation

See [Fulfillment Partners](../../CrestApps.Docs/docs/modules/fulfillment-partners.md) in the technical manual.

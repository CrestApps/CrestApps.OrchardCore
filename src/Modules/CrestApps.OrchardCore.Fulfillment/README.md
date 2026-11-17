# Fulfillment Partners

Connects a store to the outside companies that make and ship its products for it, such as print-on-demand services. This module owns the **Settings > Commerce > Fulfillment partners** page and the `IFulfillmentProvider` contract. Each company is a separate provider module that adds its connection section to that page.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Fulfillment Partners | `CrestApps.OrchardCore.Fulfillment` | The settings page that lists the partners and whether each is connected, the order card that sends paid orders to them, and automatic sending. |

No partner ships in this repository: a partner is a module that implements `IFulfillmentProvider` (see the technical manual), so a site adds the partners it uses.

## Installation

Enable **Fulfillment Partners** under **Configuration > Features**, or with the `Feature` step of a recipe. Enabling a provider's feature enables it too.

## Configuration

Open **Settings > Commerce > Fulfillment partners**. The page needs the **Manage fulfillment partners and their credentials** permission (`ManageFulfillmentPartners`), which the Administrator role has.

## What it does

- Connects the store to each partner and checks the connection.
- Adds a **Fulfillment partners** card to a paid order's admin page, with a button that sends the order to the partners that make its products, and shows what each partner answered.
- With **Send paid orders automatically** on, records each paid order for the partners as it is paid; a background task sends it within a few minutes.
- Sends an order once. When the outcome is not clear (no answer, a server error, or a send that was interrupted), the order is marked **Needs attention** and is never sent again by itself.

Tracking numbers and cancellations do not come back from partners yet.

## Dependencies

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)
- [`CrestApps.OrchardCore.Orders`](../CrestApps.OrchardCore.Orders/README.md)

## Documentation

See [Fulfillment Partners](../../CrestApps.Docs/docs/modules/fulfillment-partners.md) in the technical manual.

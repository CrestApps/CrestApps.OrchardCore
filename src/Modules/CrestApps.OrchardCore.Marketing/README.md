# Marketing

Markets to the store's customers: what each has bought, segments of customers by it, such as lapsed buyers, and email campaigns to a segment's customers who accept marketing, with an unsubscribe link. While the [Mailing Lists](../CrestApps.OrchardCore.MailingLists) feature is on, a campaign can go to a mailing list's subscribed members instead; its unsubscribe link then unsubscribes that list.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Marketing | `CrestApps.OrchardCore.Marketing` | Markets to the store's customers: what each has bought, segments of customers by it, such as lapsed buyers, and email campaigns to a segment's customers who accept marketing, with an unsubscribe link. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)
- [`CrestApps.OrchardCore.Customers`](../CrestApps.OrchardCore.Customers/README.md)
- [`CrestApps.OrchardCore.Orders`](../CrestApps.OrchardCore.Orders/README.md)
- `OrchardCore.Email`, which sends the campaigns
- [`CrestApps.OrchardCore.Outreach`](../CrestApps.OrchardCore.Outreach/README.md), the engine campaigns are sent through

## Documentation

See [Marketing](../../CrestApps.Docs/docs/modules/marketing.md) in the technical manual.

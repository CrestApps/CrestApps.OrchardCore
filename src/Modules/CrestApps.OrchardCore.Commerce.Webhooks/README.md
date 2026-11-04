# Commerce Webhooks

Tells other systems when orders are placed, paid, canceled, refunded and shipped, customers are created, and stock runs low, by posting signed JSON to the URLs you subscribe, retried until they answer, with a log of every delivery.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Commerce Webhooks | `CrestApps.OrchardCore.Commerce.Webhooks` | Tells other systems when orders are placed, paid, canceled, refunded and shipped, customers are created, and stock runs low, by posting signed JSON to the URLs you subscribe, retried until they answer, with a log of every delivery. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)

## Documentation

See [Commerce Webhooks](../../CrestApps.Docs/docs/modules/commerce-webhooks.md) in the technical manual.

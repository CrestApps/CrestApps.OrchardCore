# Omnichannel Management

Adds the headless omnichannel contact, campaign, activity, disposition, subject-flow, and channel-endpoint services, permissions, and storage without any administration screens.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Omnichannel Activities | `CrestApps.OrchardCore.Omnichannel.Activities` | Adds the headless omnichannel contact, campaign, activity, disposition, subject-flow, and channel-endpoint services, permissions, and storage without any administration screens. |
| Omnichannel Channel Endpoints | `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints` | Adds only the channel-endpoint (number/address) administration screen and services, so a feature that needs to reuse channel endpoints can depend on this without pulling in the full Omnichannel management screens. Enabled by the features that depend on it. |
| Omnichannel Management | `CrestApps.OrchardCore.Omnichannel.Managements` | Adds the omnichannel contact, campaign, activity, disposition, subject-flow, and channel-endpoint administration screens. |
| Omnichannel CRM | `CrestApps.OrchardCore.Omnichannel.Crm` | Adds leads, accounts and opportunities to the omnichannel CRM: lead records that are called and texted like contacts but kept apart until they are converted, accounts that hold contacts and opportunities, and opportunity stages for the pipeline. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Omnichannel`](../CrestApps.OrchardCore.Omnichannel/README.md)
- [`CrestApps.OrchardCore.Users`](../CrestApps.OrchardCore.Users/README.md)
- [`CrestApps.OrchardCore.ContentFields`](../CrestApps.OrchardCore.ContentFields/README.md)
- [`CrestApps.OrchardCore.PhoneNumbers`](../CrestApps.OrchardCore.PhoneNumbers/README.md)
- `OrchardCore.Contents`
- `OrchardCore.Flows`
- `OrchardCore.Users`
- [`CrestApps.OrchardCore.TimeZones`](../CrestApps.OrchardCore.TimeZones/README.md)
- and 7 more; see `Manifest.cs`.

## Documentation

See [CrestApps Omnichannel Management (CRM)](../../CrestApps.Docs/docs/omnichannel/management.md) in the technical manual.

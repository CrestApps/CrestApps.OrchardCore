# Omnichannel

Provides shared omnichannel messages, endpoints, preferences, and processing contracts across communication channels.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Omnichannel | `CrestApps.OrchardCore.Omnichannel` | Provides shared omnichannel messages, endpoints, preferences, and processing contracts across communication channels. |
| Omnichannel - Azure Communication Services | `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` | Enables Azure Communication Services email and SMS providers for omnichannel communications. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `OrchardCore.Email.Azure`
- `OrchardCore.Sms.Azure`

## Extension points

- `IOmnichannelEventHandler` (CrestApps.OrchardCore.Omnichannel.Core): Defines the contract for omnichannel event handler.
- `IOmnichannelProcessor` (CrestApps.OrchardCore.Omnichannel.Core): Defines the contract for omnichannel processor.
- `IOmnichannelContactMatch` (CrestApps.OrchardCore.Omnichannel.Core): An index row that names a contact or lead found by one of its phone numbers or addresses, with what is needed to rank it against the other records found the same way.
- `IActivityBatchLoader` (CrestApps.OrchardCore.Omnichannel.Core): Defines a strategy that loads activities into an `OmnichannelActivityBatch` for a specific activity batch source.
- `IActivityDialerContributor` (CrestApps.OrchardCore.Omnichannel.Core): Contributes optional dialer profiles and queueing behavior to Omnichannel activity management.
- and 40 more contracts in the same projects.

## Documentation

See [Omnichannel Communications](../../CrestApps.Docs/docs/omnichannel/index.md) in the technical manual.

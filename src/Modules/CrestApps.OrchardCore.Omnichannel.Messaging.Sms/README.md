# SMS Messaging Channel

Sends and receives text messages in the Omnichannel Messaging workspace.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| SMS Messaging Channel | `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` | Adds SMS as a channel of the Omnichannel Messaging workspace: SMS numbers as omnichannel addresses with a per-number provider, SMS inbound entry points that route each number's texts, the per-number provider dispatcher, two-way send and receive through every SMS provider that raises inbound messages, the carrier keywords (STOP, START, HELP), and a Send SMS button beside phone-number fields. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Omnichannel.Messaging`](../CrestApps.OrchardCore.Omnichannel.Messaging/README.md)
- `CrestApps.OrchardCore.ContactCenter.ProviderInbox`
- `OrchardCore.Sms`

## Extension points

- `ISmsDispatchProvider` (CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Core): Implemented by an SMS provider that can report its own identifier for a message it accepted.
- `ISmsDispatcher` (CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Core): Sends an outbound SMS through the provider that owns the sending number.
- `ISmsMediaDispatchProvider` (CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Core): Implemented by an SMS provider that can send picture messages (MMS).
- `ISmsMediaSender` (CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Core): Sends picture messages (MMS) for an SMS provider whose own `ISmsProvider` carries text only, such as a provider that ships with OrchardCore.

## Documentation

See [Omnichannel Messaging Workspace](../../CrestApps.Docs/docs/omnichannel/messaging-workspace.md) in the technical manual.

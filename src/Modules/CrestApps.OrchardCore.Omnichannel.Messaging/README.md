# Omnichannel Messaging

A human-operated, channel-agnostic messaging workspace: one inbox and one conversation view for every non-voice channel (SMS today; email, WhatsApp, Messenger and others as further channel features), routed to agents and queues by the inbound entry points that answer each number, built on the Omnichannel addresses and the channel-neutral Contact Center features.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Omnichannel Messaging Workspace | `CrestApps.OrchardCore.Omnichannel.Messaging` | Adds the human messaging workspace: a customer-centric inbox where each customer's conversations on every enabled channel sit side by side, routing to agents and queues through inbound entry points, templates, broadcasts, and its own SignalR hub for real-time messaging. It carries no channel of its own — enable a channel feature such as SMS Messaging Channel to send and receive. Reuses only the shared Contact Center agent-profile services for operator identity (via the dependency-only Agent Services feature) and the inbound entry points, which say where each number's messages go. |
| Omnichannel Messaging Routed Distribution | `CrestApps.OrchardCore.Omnichannel.Messaging.RoutedDistribution` | Push-assigns new department conversations, on every messaging channel, to the least-loaded available agent using Contact Center work distribution, and re-pools threads nobody picks up. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints`
- `CrestApps.OrchardCore.ContactCenter.AgentServices`
- `CrestApps.OrchardCore.ContactCenter.EntryPoints`
- `OrchardCore.SignalR`
- `CrestApps.OrchardCore.ContactCenter.Queues`

## Extension points

- `IMessagingRealTimeNotifier` (CrestApps.OrchardCore.Omnichannel.Messaging.Abstractions): Publishes real-time messaging events to the messaging workspace (toasts, unread badges, live delivery ticks).
- `IMessagingAttachmentStore` (CrestApps.OrchardCore.Omnichannel.Messaging.Core): Keeps the pictures exchanged in messaging conversations, encrypted at rest and out of the public media library, so a customer's photo is only ever served to someone allowed to read the conversation it belongs to.
- `IMessagingAttachmentUrlProvider` (CrestApps.OrchardCore.Omnichannel.Messaging.Core): Builds the address a provider downloads an outbound picture from.
- `IMessagingInboundMediaIngestor` (CrestApps.OrchardCore.Omnichannel.Messaging.Core): Copies the files of a received message out of the provider and into the workspace's own attachment store.
- `IMessagingMediaRequestAuthenticator` (CrestApps.OrchardCore.Omnichannel.Messaging.Core): Signs the download of a received picture for the provider that hosts it.
- and 28 more contracts in the same projects.

## Documentation

See [Omnichannel Messaging Workspace](../../CrestApps.Docs/docs/omnichannel/messaging-workspace.md) in the technical manual.

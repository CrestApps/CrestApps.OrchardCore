# Automated Voice

Runs an automated AI voice conversation over any telephony provider.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Automated Voice | `CrestApps.OrchardCore.Omnichannel.Voice` | Runs an automated voice conversation driven by an AI chat profile: the call is answered, the assistant speaks and listens through whichever telephony provider is carrying it, the caller can be escalated to a live agent, and the activity is settled with a summary and a disposition. A telephony provider supplies the audio; enable the provider's own automated voice feature alongside this one. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Telephony`](../CrestApps.OrchardCore.Telephony/README.md)
- [`CrestApps.OrchardCore.AI`](../CrestApps.OrchardCore.AI/README.md)
- `CrestApps.OrchardCore.AI.Chat.Core`
- [`CrestApps.OrchardCore.Omnichannel.Managements`](../CrestApps.OrchardCore.Omnichannel.Managements/README.md)

## Extension points

- `IAIVoiceCallCommandRelay` (CrestApps.OrchardCore.Omnichannel.Voice.Core): Carries a command for a live AI call to the other nodes of a tenant, when the node that received it does not hold the call.
- `IAIVoiceCallCommands` (CrestApps.OrchardCore.Omnichannel.Voice.Core): Reaches the live session holding an AI call on this node.
- `IAIVoiceCallMessageBus` (CrestApps.OrchardCore.Omnichannel.Voice.Core): A publish and subscribe channel shared by every node of a tenant, which carries commands for live AI calls to the node holding the call.
- `IAIVoiceCallShutdown` (CrestApps.OrchardCore.Omnichannel.Voice.Core): Hands this node's live AI voice calls on before the node stops, so a restart does not leave their callers on a silent line.
- `IAIVoiceCallSupervision` (CrestApps.OrchardCore.Omnichannel.Voice.Core): What a supervisor, or a workflow, can do to a call the AI is holding.
- and 9 more contracts in the same projects.

## Documentation

This module has no page of its own in the documentation site yet; its feature ids are listed in the [feature reference](../../CrestApps.Docs/docs/feature-reference.md).

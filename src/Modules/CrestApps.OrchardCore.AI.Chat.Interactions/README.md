# AI Chat Interactions

Provides ad-hoc AI chat interactions with configurable parameters without predefined profiles.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| AI Chat Interactions | `CrestApps.OrchardCore.AI.Chat.Interactions` | Provides ad-hoc AI chat interactions with configurable parameters without predefined profiles. |
| AI Profile Builder | `CrestApps.OrchardCore.AI.Chat.Interactions.ProfileBuilder` | Builds AI profiles by role-play: give the AI feedback on its replies and it rewrites its own instructions, then save the result as an AI profile. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `OrchardCore.Liquid`
- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)
- `CrestApps.OrchardCore.AI.Chat.Core`
- `OrchardCore.SignalR`
- [`CrestApps.OrchardCore.AI`](../CrestApps.OrchardCore.AI/README.md)

## Documentation

See [AI Chat Interactions Module](../../CrestApps.Docs/docs/ai/chat-interactions.md) in the technical manual.

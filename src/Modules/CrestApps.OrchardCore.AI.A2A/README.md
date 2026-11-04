# Agent-to-Agent (A2A) Protocol

Provides a user interface for connecting to remote Agent-to-Agent (A2A) hosts, enabling AI profiles to leverage external agents for multi-agent orchestration.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Agent-to-Agent (A2A) Client | `CrestApps.OrchardCore.AI.A2A` | Provides a user interface for connecting to remote Agent-to-Agent (A2A) hosts, enabling AI profiles to leverage external agents for multi-agent orchestration. |
| Agent-to-Agent (A2A) Host | `CrestApps.OrchardCore.AI.A2A.Host` | Exposes all AI Agent profiles through the A2A protocol, enabling external agents and clients to discover and communicate with locally hosted agents. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.AI`](../CrestApps.OrchardCore.AI/README.md)
- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)

## Documentation

See [Artificial Intelligence Suite](../../CrestApps.Docs/docs/ai/index.md) in the technical manual.

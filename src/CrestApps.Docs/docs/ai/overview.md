---
sidebar_label: AI Services
sidebar_position: 1
title: AI Services
description: Orchard Core foundational AI features, module composition, and admin setup guidance.
---

# AI Services

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.AI` |
| **Primary feature IDs** | `CrestApps.OrchardCore.AI`, `CrestApps.OrchardCore.AI.ChatCore`, `CrestApps.OrchardCore.AI.ChatApi`, `CrestApps.OrchardCore.AI.ConnectionManagement` |

`CrestApps.OrchardCore.AI` is the Orchard foundation for the AI suite. It wires shared CrestApps.Core AI services into Orchard Core and adds the admin surfaces used by the rest of the AI modules.

## What this module adds in Orchard Core

- feature manifests and Orchard dependency wiring
- connection and deployment management UI
- AI profile management and site settings
- recipe, workflow, and admin integration points
- Orchard-aware configuration and startup registration

For the reusable framework pieces underneath those features, see **[CrestApps.Core AI documentation](https://core.crestapps.com/docs/core/ai-core)**.

## Enable the core AI features

The base AI module exposes multiple Orchard features:

| Feature ID | Purpose |
| --- | --- |
| `CrestApps.OrchardCore.AI` | Base AI services and shared admin wiring |
| `CrestApps.OrchardCore.AI.ChatCore` | Profile-driven chat services and session processing |
| `CrestApps.OrchardCore.AI.ChatApi` | REST endpoints for AI chat and completion access |
| `CrestApps.OrchardCore.AI.ConnectionManagement` | Provider connection management UI |

In a typical Orchard setup:

1. Enable **AI Services**.
2. Enable **AI Connection Management**.
3. Enable one or more provider modules such as OpenAI, Azure OpenAI, Azure AI Inference, or Ollama.
4. Add feature modules such as AI Chat, AI Chat Interactions, Documents, Data Sources, MCP, A2A, or Memory as needed.

## Where to manage AI in the Orchard admin

Once enabled, the main AI screens are available under **Artificial Intelligence**:

- **Artificial Intelligence -> Profiles**
- **Artificial Intelligence -> Provider Connections**
- **Artificial Intelligence -> Templates**

Site-wide settings are available under **Settings -> Artificial Intelligence**.

## Common module combinations

| Goal | Enable these areas |
| --- | --- |
| Manage providers and AI profiles | AI Services + one or more [provider modules](./providers/) |
| Add profile-driven chat | AI Services + [AI Chat](chat) |
| Add ad-hoc chat sessions | AI Services + [AI Chat Interactions](chat-interactions) |
| Add retrieval over uploaded documents | AI Services + [Documents](./documents/) |
| Add retrieval over external indexes | AI Services + [Data Sources](./data-sources/) |
| Add model-to-tool connectivity | AI Services + [MCP](./mcp/) or [A2A](./a2a/) |

## Key admin surfaces

After enabling the relevant features, Orchard Core exposes AI management in the admin UI, including:

- AI settings under **Settings -> Artificial Intelligence**
- feature enablement under **Tools -> Features**
- AI connection and deployment editors
- AI profile editors used by chat, agents, and related modules

## Configuration sources

The Orchard AI modules support both shell configuration and site settings.

### Shell configuration example

Use `appsettings.json` when you want startup defaults for Orchard-hosted AI behavior:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "DefaultParameters": {
        "Temperature": 0,
        "MaxOutputTokens": 800
      }
    }
  }
}
```

This configuration is useful for default completion parameters. Provider-specific connection examples are documented on the individual [provider pages](./providers/).

### Site settings

Use **Settings -> Artificial Intelligence** for tenant-managed options such as:

- usage tracking
- preemptive memory retrieval
- orchestrator defaults
- distributed caching and OpenTelemetry overrides

## Creating AI profiles

Once at least one provider is configured, create AI profiles from **Artificial Intelligence -> Profiles** and attach the capabilities you need for that profile, such as:

- documents
- data sources
- MCP connections
- A2A connections
- memory
- prompt templates
- agent access
- deployment and model selection

AI profile templates and related display drivers extend the profile editor automatically when their corresponding features are enabled.

## Recipes, deployment, and workflows

This module also wires Orchard integrations beyond the admin UI:

- recipe steps for AI profiles, profile templates, and deployments
- Orchard deployment plan support
- workflow activities for profile-based and direct AI completion
- workflow events for chat-session extraction, closure, and post-processing

## Framework references

The modules in this repository build on **CrestApps.Core**. Framework-oriented topics such as service APIs, orchestration loops, response handlers, tool registration, and programmatic host composition are documented on the Core site:

- [AI core](https://core.crestapps.com/docs/core/ai-core)
- [AI profiles](https://core.crestapps.com/docs/core/ai-profiles)
- [Tools](https://core.crestapps.com/docs/core/tools)
- [Response handlers](https://core.crestapps.com/docs/core/response-handlers)
- [Orchestration](https://core.crestapps.com/docs/core/orchestration)

## Next steps

1. Configure one or more [AI Providers](./providers/).
2. Choose a UI surface with [AI Chat](chat) or [AI Chat Interactions](chat-interactions).
3. Add optional capabilities such as [Memory](memory), [Documents](./documents/), [MCP](./mcp/), or [A2A](./a2a/).

---
sidebar_label: Overview
sidebar_position: 0
title: Artificial Intelligence Suite
description: Orchard Core AI modules built on top of CrestApps.Core, with Orchard admin experiences, feature wiring, and host integration guidance.
user_manual:
  - user-manual/ai/index
---

# Artificial Intelligence Suite

The AI modules in this repository bring **CrestApps.Core** AI capabilities into **Orchard Core**. They add feature manifests, admin editors, site settings, recipes, module startup wiring, document storage choices, and CMS-friendly runtime surfaces.

Framework documentation for shared concepts such as orchestration, tool pipelines, prompt rendering, provider abstractions, and reusable service APIs is available at **[core.crestapps.com](https://core.crestapps.com)**.

This section of the Technical Manual covers installation, feature IDs, configuration, recipes and extension points. For day-to-day use in the admin (creating profiles, chatting, widgets, knowledge, tools, analytics), see the User Manual, starting at [AI features at a glance](../user-manual/ai/index.md).

## Orchard-focused scope

Use this Orchard site when you need:

- module names and feature IDs
- Orchard admin paths and settings groups
- guidance on enabling provider, memory, document, MCP, and A2A modules together
- startup apps and sample-client references from this repository

Use the Core site when you need:

- orchestration internals
- service APIs and extension points
- framework-level provider implementation guidance
- shared prompt, tool, agent, memory, and response-handler concepts

## Module map

| Module area | Main feature IDs | Orchard docs | User Manual |
| --- | --- | --- | --- |
| Foundational AI features | `CrestApps.OrchardCore.AI`, `CrestApps.OrchardCore.AI.ConnectionManagement`, `CrestApps.OrchardCore.AI.Chat.Core`, `CrestApps.OrchardCore.AI.Chat.Api` | [AI Services](overview) | [Connections and deployments](../user-manual/ai/connections.md), [AI profiles](../user-manual/ai/profiles.md) |
| Profile-driven chat UI | `CrestApps.OrchardCore.AI.Chat`, `CrestApps.OrchardCore.AI.Chat.AdminWidget` | [AI Chat](chat) | [Chat](../user-manual/ai/chat.md), [Chat widgets](../user-manual/ai/chat-widgets.md) |
| Session analytics | `CrestApps.OrchardCore.AI.Chat.Analytics` | [AI Chat Session Analytics](chat-analytics), [AI Usage Analytics](usage-analytics) | [Analytics](../user-manual/ai/analytics.md) |
| Ad-hoc chat experiences | `CrestApps.OrchardCore.AI.Chat.Interactions` | [AI Chat Interactions](chat-interactions) | [Chat interactions](../user-manual/ai/chat-interactions.md) |
| Chat notifications (developer API) | `CrestApps.OrchardCore.AI.Chat` | [AI Chat Notifications](chat-notifications) | |
| Copilot orchestration | `CrestApps.OrchardCore.AI.Chat.Copilot` | [Copilot Integration](copilot) | [AI profiles](../user-manual/ai/profiles.md) |
| Claude orchestration | `CrestApps.OrchardCore.AI.Chat.Claude` | [Claude Integration](claude) | [AI profiles](../user-manual/ai/profiles.md) |
| Agent profiles and Orchard tools | `CrestApps.OrchardCore.AI.Agent` | [AI Agents](agent), [AI Tools](tools) | [Tools and agents](../user-manual/ai/tools-and-agents.md) |
| Prompt templates | `CrestApps.OrchardCore.AI.Prompting` | [AI Prompt Templates](prompt-templates) | [Templates](../user-manual/ai/prompt-templates.md) |
| Profile templates | `CrestApps.OrchardCore.AI` | [AI Profile Templates](profile-templates) | [Templates](../user-manual/ai/prompt-templates.md) |
| User memory | `CrestApps.OrchardCore.AI.Memory`, `.Memory.AzureAI`, `.Memory.Elasticsearch` | [AI Memory](memory) | [Memory](../user-manual/ai/memory.md) |
| Tool instances | `CrestApps.OrchardCore.AI.ToolInstances` | [AI Tool Instances](tool-instances) | [Tools and agents](../user-manual/ai/tools-and-agents.md) |
| Deployment capabilities and slots | `CrestApps.OrchardCore.AI` | [Model Capabilities](model-capabilities) | [Connections and deployments](../user-manual/ai/connections.md) |
| Spoken conversations | `CrestApps.OrchardCore.AI.Chat` | [Realtime Voice](realtime-voice) | [Chat](../user-manual/ai/chat.md) |
| Scheduled folder ingestion | `CrestApps.OrchardCore.AI.FileSources`, `.FileSources.Ftp`, `.FileSources.Sftp` | [AI File Sources](file-sources) | [Knowledge](../user-manual/ai/knowledge.md) |
| Workflow activities and events | `CrestApps.OrchardCore.AI` with `OrchardCore.Workflows` | [AI Workflows](workflows) | [AI in workflows](../user-manual/ai/workflows.md) |
| A2A modules | `CrestApps.OrchardCore.AI.A2A`, `CrestApps.OrchardCore.AI.A2A.Host` | [A2A](a2a/) | [Tools and agents](../user-manual/ai/tools-and-agents.md) |
| Provider modules | `CrestApps.OrchardCore.OpenAI`, `CrestApps.OrchardCore.OpenAI.Azure`, `CrestApps.OrchardCore.AzureAIInference`, `CrestApps.OrchardCore.Ollama` | [AI Providers](providers/) | [Connections and deployments](../user-manual/ai/connections.md) |
| Data source modules | `CrestApps.OrchardCore.AI.DataSources` and its providers, `CrestApps.OrchardCore.AI.WebCrawlers` | [Data Sources](data-sources/) | [Knowledge](../user-manual/ai/knowledge.md) |
| Document modules | `CrestApps.OrchardCore.AI.Documents` and its sub-features | [Documents](documents/) | [Knowledge](../user-manual/ai/knowledge.md) |
| MCP modules | `CrestApps.OrchardCore.AI.Mcp`, `.Mcp.Stdio`, `.Mcp.Server` | [MCP](mcp/) | [Tools and agents](../user-manual/ai/tools-and-agents.md) |

The [Feature Reference](../feature-reference.md) lists every feature ID with its display name.

## Recommended reading order

1. Start with [AI Services](overview).
2. Add one or more [AI Providers](providers/).
3. Choose your UI surface: [AI Chat](chat) or [AI Chat Interactions](chat-interactions).
4. Layer in [Documents](documents/), [Data Sources](data-sources/), [Memory](memory), [MCP](mcp/), or [A2A](a2a/) as needed.

## Related Core docs

- [CrestApps.Core AI overview](https://core.crestapps.com/docs/core/ai-core)
- [AI chat concepts](https://core.crestapps.com/docs/core/chat)
- [AI tools](https://core.crestapps.com/docs/core/tools)
- [AI agents](https://core.crestapps.com/docs/core/agents)
- [AI memory](https://core.crestapps.com/docs/core/ai-memory)
- [Prompt templates](https://core.crestapps.com/docs/core/ai-templates)

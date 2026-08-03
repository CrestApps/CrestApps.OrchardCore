---
sidebar_label: Overview
sidebar_position: 0
title: Artificial Intelligence Suite
description: Orchard Core AI modules built on top of CrestApps.Core, with Orchard admin experiences, feature wiring, and host integration guidance.
---

# Artificial Intelligence Suite

The AI modules in this repository bring **CrestApps.Core** AI capabilities into **Orchard Core**. They add feature manifests, admin editors, site settings, recipes, module startup wiring, document storage choices, and CMS-friendly runtime surfaces.

Framework documentation for shared concepts such as orchestration, tool pipelines, prompt rendering, provider abstractions, and reusable service APIs is available at **[core.crestapps.com](https://core.crestapps.com)**.

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

| Module area | Orchard docs |
| --- | --- |
| Foundational AI features | [AI Services](overview) |
| Profile-driven chat UI | [AI Chat](chat) |
| Session analytics | [AI Chat Analytics](chat-analytics) |
| Ad-hoc chat experiences | [AI Chat Interactions](chat-interactions) |
| Chat notifications | [AI Chat Notifications](chat-notifications) |
| Copilot orchestration | [Copilot Integration](copilot) |
| Claude orchestration | [Claude Integration](claude) |
| Agent profiles | [AI Agents](agent) |
| Prompt templates | [AI Prompt Templates](prompt-templates) |
| Profile templates | [AI Profile Templates](profile-templates) |
| User memory | [AI Memory](memory) |
| Workflow activities and events | [AI Workflows](workflows) |
| A2A modules | [A2A](a2a/) |
| Provider modules | [AI Providers](providers/) |
| Data source modules | [Data Sources](data-sources/) |
| Document modules | [Documents](documents/) |
| MCP modules | [MCP](mcp/) |

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

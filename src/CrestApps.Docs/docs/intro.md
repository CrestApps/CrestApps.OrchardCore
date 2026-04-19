---
sidebar_label: Introduction
sidebar_position: 1
title: CrestApps Orchard Core Modules
description: Orchard Core modules, startup apps, and integration guidance built on top of the shared CrestApps.Core framework.
---

# CrestApps Orchard Core Modules

This site documents the **Orchard Core-specific** packages in `CrestApps.OrchardCore`: modules, feature IDs, admin configuration, startup apps, samples, and CMS integration guidance.

The shared platform underneath these modules now lives in **CrestApps.Core**. When you need framework concepts such as orchestration internals, service APIs, tool pipelines, provider implementation details, or reusable .NET host guidance, use **[core.crestapps.com](https://core.crestapps.com)**.

## What this documentation covers

- Orchard Core module installation and feature enablement
- Orchard admin settings, recipes, and CMS integration surfaces
- Orchard-specific AI, MCP, A2A, Documents, Data Sources, and Omnichannel modules
- Startup applications and sample apps in this repository

## Module groups

### Artificial Intelligence Suite

The AI modules add Orchard admin experiences and feature wiring on top of CrestApps.Core AI services.

- **[Artificial Intelligence Suite](orchardcore/ai/)** - module map and Orchard-specific scope
- **[AI Services](orchardcore/ai/overview)** - foundational Orchard AI features and admin surfaces
- **[AI Chat](orchardcore/ai/chat)** - chat UI and profile-driven conversations
- **[AI Chat Interactions](orchardcore/ai/chat-interactions)** - ad-hoc chat experiences without requiring a profile
- **[AI Providers](orchardcore/ai/providers/)** - provider modules such as OpenAI, Azure OpenAI, Azure AI Inference, and Ollama
- **[AI Documents](orchardcore/ai/documents/)** - document upload, parsing, storage, and indexing modules
- **[AI Data Sources](orchardcore/ai/data-sources/)** - external knowledge source integrations
- **[MCP](orchardcore/ai/mcp/)** - Orchard Core MCP client, server, and resource modules
- **[A2A](orchardcore/ai/a2a/)** - Orchard Core client and host support for the Agent-to-Agent protocol

### Standard modules

- **[Content Access Control](orchardcore/modules/content-access-control)**
- **[Recipes](orchardcore/modules/recipes)**
- **[Resources](orchardcore/modules/resources)**
- **[Roles](orchardcore/modules/roles)**
- **[SignalR](orchardcore/modules/signalr)**
- **[Users](orchardcore/modules/users)**

### Omnichannel Communications

- **[Omnichannel overview](orchardcore/omnichannel/)**
- **[Event Grid integration](orchardcore/omnichannel/event-grid)**
- **[Management UI](orchardcore/omnichannel/management)**
- **[SMS automation](orchardcore/omnichannel/sms)**

### Samples

- **[Samples overview](orchardcore/samples/)**
- **[MCP client sample](orchardcore/samples/mcp-client)**
- **[A2A client sample](orchardcore/samples/a2a-client)**

## Start here

1. Follow **[Getting Started](getting-started)** to build the repo or add packages to your own Orchard solution.
2. Use the Orchard docs on this site to enable the right modules and configure the admin experience.
3. Jump to **[core.crestapps.com](https://core.crestapps.com)** when you need framework-level implementation guidance.

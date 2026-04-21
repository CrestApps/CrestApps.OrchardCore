---
sidebar_label: Introduction
sidebar_position: 1
title: CrestApps Orchard Core Modules
description: Orchard Core modules, startup apps, and integration guidance built on top of the shared CrestApps.Core framework.
---

# CrestApps Orchard Core Modules

This site documents the **Orchard Core-specific** packages in `CrestApps.OrchardCore`: modules, feature IDs, admin configuration, startup apps, samples, and CMS integration guidance.

The shared platform underneath these modules is documented at **[core.crestapps.com](https://core.crestapps.com)**. Use that site for framework concepts such as orchestration internals, service APIs, tool pipelines, provider implementation details, or reusable .NET host guidance.

## What this documentation covers

- Orchard Core module installation and feature enablement
- Orchard admin settings, recipes, and CMS integration surfaces
- Orchard-specific AI, MCP, A2A, Documents, Data Sources, and Omnichannel modules
- Startup applications and sample apps in this repository

## Module groups

### Artificial Intelligence Suite

The AI modules add Orchard admin experiences and feature wiring on top of CrestApps.Core AI services.

- **[Artificial Intelligence Suite](ai/)** - module map and Orchard-specific scope
- **[AI Services](ai/overview)** - foundational Orchard AI features and admin surfaces
- **[AI Chat](ai/chat)** - chat UI and profile-driven conversations
- **[AI Chat Interactions](ai/chat-interactions)** - ad-hoc chat experiences without requiring a profile
- **[AI Providers](ai/providers/)** - provider modules such as OpenAI, Azure OpenAI, Azure AI Inference, and Ollama
- **[AI Documents](ai/documents/)** - document upload, parsing, storage, and indexing modules
- **[AI Data Sources](ai/data-sources/)** - external knowledge source integrations
- **[MCP](ai/mcp/)** - Orchard Core MCP client, server, and resource modules
- **[A2A](ai/a2a/)** - Orchard Core client and host support for the Agent-to-Agent protocol

### Standard modules

- **[Content Access Control](modules/content-access-control)**
- **[Recipes](modules/recipes)**
- **[Resources](modules/resources)**
- **[Roles](modules/roles)**
- **[SignalR](modules/signalr)**
- **[Users](modules/users)**

### Omnichannel Communications

- **[Omnichannel overview](omnichannel/)**
- **[Event Grid integration](omnichannel/event-grid)**
- **[Management UI](omnichannel/management)**
- **[SMS automation](omnichannel/sms)**

### Samples

- **[Samples overview](samples/)**
- **[MCP client sample](samples/mcp-client)**
- **[A2A client sample](samples/a2a-client)**

## Start here

1. Follow **[Getting Started](getting-started)** to build the repo or add packages to your own Orchard solution.
2. Use the Orchard docs on this site to enable the right modules and configure the admin experience.
3. Jump to **[core.crestapps.com](https://core.crestapps.com)** when you need framework-level implementation guidance.

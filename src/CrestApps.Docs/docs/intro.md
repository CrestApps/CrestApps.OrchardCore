---
sidebar_label: Overview
sidebar_position: 1
title: Technical Manual
description: For developers and IT. Install the CrestApps Orchard Core modules, configure them with appsettings.json and environment variables, deploy and operate them, and build on top of them.
user_manual:
  - user-manual/index
---

# Technical Manual

This manual is for **developers and IT**: the people who install the CrestApps Orchard Core modules, configure them outside the admin screens, deploy and operate them, and extend them with code.

:::tip[Looking for how to use the screens?]
The [User Manual](user-manual/index.md) is written for the people who use the app every day: agents, supervisors, managers and the administrators who work in the admin screens. It has step-by-step instructions, screencasts, use cases and training paths. Most pages in this manual link to the matching User Manual page, and the other way around.
:::

The shared platform underneath these modules is documented at **[core.crestapps.com](https://core.crestapps.com)**. Use that site for framework concepts such as orchestration internals, service APIs, tool pipelines, provider implementation details, or reusable .NET host guidance.

## Start here

1. **[Getting Started](getting-started.md)**: add the packages to your Orchard Core solution, or build and run this repository.
2. **[Configuration](configuration.md)**: every setting the modules read from `appsettings.json` and environment variables, per tenant.
3. **[Feature ID Reference](feature-reference.md)**: the feature IDs to enable in **Tools > Features**, in recipes, or in a `[Feature]` attribute.
4. **[Release notes](changelog/index.md)**: breaking changes and what is new in each version.

## What this manual covers

- Packages, feature IDs and feature dependencies
- Configuration through `appsettings.json`, environment variables and per-tenant settings
- Recipes and deployment steps
- Architecture: how the modules fit together and how a call, a message or an AI request flows through them
- Operations: deployment topology, runbooks and production support
- Extension points: providers, tools, handlers, report sources and other services you can replace or add

## Module groups

### Artificial Intelligence Suite

The AI modules add Orchard admin experiences and feature wiring on top of CrestApps.Core AI services.

- **[Artificial Intelligence Suite](ai/index.md)**: module map and Orchard-specific scope
- **[AI Services](ai/overview.md)**: foundational Orchard AI features and admin surfaces
- **[AI Chat](ai/chat.md)**: chat UI and profile-driven conversations
- **[AI Chat Interactions](ai/chat-interactions.md)**: ad-hoc chat experiences without requiring a profile
- **[AI Providers](ai/providers/index.md)**: provider modules such as OpenAI, Azure OpenAI, Azure AI Inference, and Ollama
- **[AI Documents](ai/documents/index.md)**: document upload, parsing, storage, and indexing modules
- **[AI Data Sources](ai/data-sources/index.md)**: external knowledge source integrations
- **[AI File Sources](ai/file-sources.md)**: scheduled ingestion of a local, FTP, or SFTP folder
- **[Model Capabilities](ai/model-capabilities.md)**: what each deployment's model can do, and the slot it fills
- **[Realtime Voice](ai/realtime-voice.md)**: live spoken conversations over a provider realtime session
- **[MCP](ai/mcp/index.md)**: Orchard Core MCP client, server, and resource modules
- **[A2A](ai/a2a/index.md)**: Orchard Core client and host support for the Agent-to-Agent protocol

### Omnichannel Communications

- **[Omnichannel overview](omnichannel/index.md)**
- **[Management (CRM)](omnichannel/management.md)** and **[Leads, Accounts and Opportunities](omnichannel/crm.md)**
- **[SMS automation](omnichannel/sms.md)** and **[Cadences](omnichannel/cadences.md)**
- **[Messaging workspace](omnichannel/messaging-workspace.md)**
- **[Azure Communication Services](omnichannel/azure-communication-services.md)** and **[Event Grid integration](omnichannel/event-grid.md)**

### Telephony

- **[Telephony and the soft phone](telephony/index.md)**
- **[Telnyx provider](telephony/telnyx.md)** and **[Asterisk provider](telephony/asterisk.md)**
- **[Extension dialing](telephony/extension-dialing.md)**, **[Recording storage](telephony/recording-azure-blob-storage.md)** and **[Custom providers](telephony/custom-providers.md)**

### Contact Center

- **[Contact Center overview](contact-center/index.md)**: features, layers, events and voice provider integration
- **[Agents, Queues & Dialer](contact-center/agents-queues-dialer.md)**, **[Voice routing](contact-center/voice-routing.md)** and **[Routing and work state](contact-center/routing-work-state.md)**
- **[Configuration and deployment](contact-center/configuration-deployment.md)**, **[Runbooks](contact-center/runbooks.md)** and **[Production support](contact-center/production-support.md)**

### Standard modules

- **[Standard modules overview](modules/index.md)**
- **[Content Access Control](modules/content-access-control.md)**, **[Content Fields](modules/content-fields.md)** and **[Content Transfer](modules/content-transfer.md)**
- **[DNC Registry](modules/dnc-registry.md)** and **[Phone Number Verifications](modules/phone-number-verifications.md)**
- **[Recipes](modules/recipes.md)**, **[Reports](modules/reports.md)** and **[Resources](modules/resources.md)**
- **[Roles](modules/roles.md)**, **[Users](modules/users.md)** and **[Time Zones](modules/time-zones.md)**
- **[SignalR](modules/signalr.md)** and **[WebSockets](modules/websockets.md)**

### Samples

- **[Samples overview](samples/index.md)**: the **[MCP client](samples/mcp-client.md)** and **[A2A client](samples/a2a-client.md)** samples

---
sidebar_label: Orchard Core Agent
sidebar_position: 6
title: Orchard Core AI Agent Feature
description: Intelligent agents that perform tasks on your Orchard Core site using AI capabilities.
---

| | |
| --- | --- |
| **Feature Name** | Orchard Core AI Agent |
| **Feature ID** | `CrestApps.OrchardCore.AI.Agent` |

Use natural language to run tasks, manage content, interact with OrchardCore features, and do much more with integrated AI-powered tools.

## Overview

The **Orchard Core AI Agent** feature extends the **AI Services** feature by enabling intelligent agents that can perform tasks on your Orchard Core site using natural language. Agents leverage registered AI tools and capabilities to manage content, query data, interact with workflows, and automate common site administration tasks — all through conversational AI.

### Getting Started

1. **Install the Package**  
   Add the `CrestApps.OrchardCore.AI.Agent` package to your startup web project.

2. **Enable the Feature**  
   In the **Orchard Core Admin**, go to the Features section and enable **Orchard Core AI Agent**.

### Configuring AI Agents

Once the feature is enabled:

- Navigate to your AI Profiles.
- Create a new profile or edit an existing one.
- Under the **Capabilities** tab, assign the capabilities you want the AI Agent to perform.

This allows you to tailor each agent's abilities to suit your specific site tasks and workflows.

## Authorization Model

AI tools do **not** perform their own permission checks at invocation time. Instead, authorization is enforced at the **profile design level**:

- When an administrator configures an AI Profile or Chat Interaction, the `LocalToolRegistryProvider` verifies that the configuring user has the `AIPermissions.AccessAITool` permission for each tool being exposed.
- If the user does not have permission to a specific tool, that tool cannot be added to the profile.
- At runtime, any tool exposed through the profile is trusted and will execute without further authorization checks.

This design ensures that AI tools work correctly in all contexts, including:

- **Anonymous chat widgets** exposed to unauthenticated users
- **Background tasks** such as text extraction or post-session processing
- **MCP (Model Context Protocol) requests** from external AI agents

### Content Ownership for Anonymous Contexts

The `CreateOrUpdateContentTool` requires a user identity to set the `Owner` and `Author` fields on new content items. When no user is authenticated, the tool supports optional fallback parameters that the AI model can provide:

| Parameter | Type | Description |
|-----------|------|-------------|
| `ownerUserId` | `string` | The user ID to assign as content owner. |
| `ownerUsername` | `string` | The username to look up and assign as content owner. |
| `ownerEmail` | `string` | The email address to look up and assign as content owner. |

The tool resolves the user in priority order: `ownerUserId` > `ownerUsername` > `ownerEmail`. When a matching user is found, the content item's `Owner` and `Author` fields are set accordingly.

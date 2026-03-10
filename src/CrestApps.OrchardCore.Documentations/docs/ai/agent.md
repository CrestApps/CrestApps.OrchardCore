---
sidebar_label: Orchard Core Agent
sidebar_position: 6
title: Orchard Core AI Agent Feature
description: Intelligent agents that perform tasks on your Orchard Core site using AI capabilities, including Agent profile types for building multi-agent workflows.
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

---

## Agent Profile Type

AI Profiles support an **Agent** profile type that turns a profile into a reusable, composable agent. An Agent profile is automatically exposed as an AI tool, allowing other profiles to invoke it during orchestration.

### Creating an Agent Profile

1. Go to **Artificial Intelligence → AI Profiles** and click **Add Profile**.
2. Set the **Profile Type** to **Agent**.
3. Provide a **Description** that clearly explains what the agent does. This description is used by the AI model to determine when to invoke the agent.
4. Configure the system message, connection, tools, MCP connections, and knowledge base sources as needed.
5. Save the profile.

### How It Works

When you create an Agent profile:

- The system automatically registers it as an AI tool via the `AgentToolRegistryProvider`.
- Other AI Profiles and Chat Interactions can select which agents to include in their **Agents** section under the **Capabilities** tab.
- During orchestration, the AI model can invoke any selected agent as a tool, passing it a task description.
- The agent executes the task using its own configuration (system message, tools, connections, etc.) and returns the result.

### Agent Selection

Agents appear as a **separate checkbox section** in the Capabilities tab of:

- **AI Profiles** — Select which agents this profile can invoke
- **AI Templates** — Pre-configure agent selections for profile templates
- **Chat Interactions** — Select agents for ad-hoc chat sessions

Agents are displayed as checkboxes (not in the Tools section), making it clear which agents are available and selected.

### System Agents

An agent can be marked as a **system agent**, which means it is automatically included in every completion request regardless of user selection. System agents are useful for core capabilities like planning that should always be available.

System agents:

- Are always included by the orchestrator
- Do not appear in the agent selection UI
- Are configured via the `AgentMetadata.IsSystemAgent` property

### Built-in Agent Templates

The following agent templates are provided out of the box to help you get started:

| Template | Description |
|----------|-------------|
| **Planner Agent** | Analyzes user requests and creates structured execution plans identifying the required steps and capabilities. |
| **Research Agent** | Gathers, synthesizes, and summarizes information from available knowledge sources. |
| **Executor Agent** | Takes a plan or set of instructions and executes each step methodically using available tools. |

To use a template, create a new AI Profile and select the desired template from the template dropdown.

### Defining Agent Profiles via Code

```csharp
var profile = await _profileManager.NewAsync("Azure");

profile.Name = "ResearchAgent";
profile.DisplayText = "Research Agent";
profile.Type = AIProfileType.Agent;
profile.Description = "Gathers and synthesizes information from available knowledge sources.";

await _profileManager.CreateAsync(profile);
```

### Defining Agent Profiles via Recipes

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "ResearchAgent",
          "DisplayText": "Research Agent",
          "Type": "Agent",
          "Description": "Gathers and synthesizes information from available knowledge sources.",
          "ConnectionName": "openai-cloud",
          "SystemMessage": "You are a research agent..."
        }
      ]
    }
  ]
}
```

---

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

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
- During orchestration, the AI model decides when to invoke an agent by calling it as a tool, passing it a prompt describing what needs to be done.
- The agent executes the prompt using its own configuration (system message, tools, connections, etc.) and returns the result.

### Orchestration

The **Default Orchestrator** uses a multi-tier approach for managing tools and agents:

- When the number of available tools is small (≤30), all tools are included directly.
- When the number of available tools exceeds the **scoping threshold** (default: 30), the orchestrator uses token-based relevance scoring to select the most appropriate tools for the user's request.
- When MCP connections are present or the tool count exceeds the **planning threshold** (default: 100), the orchestrator uses a full LLM planning phase to determine which tools and agents to invoke.
- For planning behavior, include the **Planner Agent** template in your profile's agent selection. The built-in planner is also available as a system-level fallback during orchestration.

### Agent Context Injection

When agents are configured for a profile, the `AgentOrchestrationContextBuilderHandler` automatically enriches the system message with descriptions of all available agents. This follows the industry-standard pattern (used by OpenAI, LangChain, CrewAI, and Semantic Kernel) of including capability descriptions in the system prompt so the model can make informed routing decisions.

The handler:

1. Loads all Agent profiles and filters by **availability** (always-available agents are always included; on-demand agents only when explicitly selected).
2. Renders a lightweight context block listing each agent's name and description (~50 tokens per agent).
3. Appends the block to the system message, giving the model awareness of available agents.

This approach keeps token usage minimal while enabling the model to autonomously decide when to delegate to an agent. For simple prompts that don't need agents, the model naturally ignores the agent context.

### Agent Selection

Agents appear as a **separate checkbox section** in the Capabilities tab of:

- **AI Profiles** — Select which agents this profile can invoke
- **AI Templates** — Pre-configure agent selections for profile templates
- **Chat Interactions** — Select agents for ad-hoc chat sessions

Agents are displayed as checkboxes (not in the Tools section), making it clear which agents are available and selected.

### Agent Availability Modes

Each agent profile has an **Availability** setting that controls how it is included in AI requests:

#### On Demand (Default)

On-demand agents are included **only when matched** by semantic or keyword relevance scoring. Users select on-demand agents from the **Agents** section under the **Capabilities** tab.

- Minimizes token usage by including agents only when relevant
- Agents appear in the checkbox list for user selection
- Best for specialized agents that are only needed in specific contexts

#### Always Available

Always-available agents are **automatically included in every completion request**, regardless of user selection.

- Agents are always accessible to the AI model
- Do **not** appear in the Capabilities tab checkbox lists (they are auto-included)
- A warning is shown when selecting this mode: *"Always available agents are included in every AI request, which increases token usage and cost."*
- Best for core capabilities that should always be accessible (e.g., planning, routing)

Configure the availability mode when editing an Agent profile or Agent template under the **Availability** dropdown.

### Built-in Agent Templates

The following agent templates are provided out of the box to help you get started:

| Template | Category | Description |
|----------|----------|-------------|
| **Planner Agent** | Orchestration | Analyzes user requests and creates structured execution plans identifying the required steps and capabilities. |
| **Research Agent** | Research | Gathers, synthesizes, and summarizes information from available knowledge sources. |
| **Executor Agent** | Orchestration | Takes a plan or set of instructions and executes each step methodically using available tools. |
| **Writer Agent** | Content | Drafts, rewrites, and polishes written content such as articles, emails, summaries, and documentation. |
| **Reviewer Agent** | Quality | Critically reviews content, code, or plans and provides structured feedback with suggestions for improvement. |
| **Data Analyst Agent** | Analysis | Analyzes structured and unstructured data, identifies patterns and trends, and presents findings with clear explanations. |
| **Summarizer Agent** | Content | Condenses long-form content into concise, accurate summaries while preserving key information and context. |
| **Code Assistant Agent** | Development | Assists with software development tasks including writing code, debugging issues, and suggesting improvements. |

To use a template, create a new AI Profile and select the desired template from the template dropdown.

### Defining Agent Profiles via Code

```csharp
var profile = await _profileManager.NewAsync();

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
          "ChatDeploymentName": "research-agent-chat",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a research agent..."
            }
          }
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

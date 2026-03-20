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

## Browser Automation Tools

The AI Agent feature now includes a large **Playwright-powered browser automation** toolset so an AI chat can interact with your website through the real UI in a user-like way. These tools let the model open a browser session, navigate between pages, inspect the DOM, click buttons, fill forms, wait for UI state changes, capture screenshots, and gather troubleshooting diagnostics.

### Tool Categories

Browser tools are grouped in the **Capabilities** tab so you can enable the right level of browser access for each profile or chat interaction. The grouped tool picker already supports **Select All** globally and a per-category **Select All** toggle, so you can enable a whole browser logic group with one click.

The browser capability labels are localized the same way as the rest of the AI Agent tool catalog, so category names appear consistently in Orchard Core language extraction and translation workflows.

The browser automation set is organized into these categories:

| Category | Purpose |
| --- | --- |
| **Browser Sessions** | Start/close sessions, list sessions, inspect sessions, and manage tabs. |
| **Browser Navigation** | Navigate to URLs, go back/forward, reload, and scroll pages or elements. |
| **Browser Inspection** | Read page state, content, links, forms, headings, buttons, and element details. |
| **Browser Interaction** | Click, double-click, hover, and send keyboard input. |
| **Browser Forms** | Fill inputs, clear fields, select options, check/uncheck controls, and upload files. |
| **Browser Waiting** | Wait for selectors, URL changes, and load states. |
| **Browser Troubleshooting** | Capture screenshots, inspect console output, inspect network activity, and diagnose broken pages. |

The built-in browser tools also ship with normalized JSON schema metadata so AI providers receive compact parameter definitions without extra spacer lines between schema entries.

For navigation-heavy admin tasks, the browser set also includes a dedicated menu-navigation tool that can follow nested labels such as `Search >> Indexes` instead of relying only on direct URLs or generic link inspection.

### How Browser Sessions Work

Browser automation tools are **stateful** and now live behind the optional `CrestApps.OrchardCore.AI.Agent.BrowserAutomation` feature so tenants can enable the core AI Agent without automatically enabling Playwright-based browser control. Start by calling `startBrowserSession`, then keep passing the returned `sessionId` to later browser tools when you want to pin a specific session. Most browser tools also accept the special `default` session alias, which resolves to the most recently used live browser session, so the model does not need an explicit `sessionId` for common single-session navigation flows.

When no tracked session exists yet, the `default` alias now attempts to auto-start a Playwright session from the current AI Chat page URL. If the chat is rendered inside an iframe widget, the widget passes the parent page URL when available so browser navigation can start from the host page instead of the iframe shell. For same-origin pages, the current request cookies are also copied into the Playwright context so authenticated admin navigation can reuse the active Orchard Core sign-in session.

For direct page navigation requests, the chat clients also listen for a live `NavigateTo` SignalR command. When a browser navigation tool resolves a same-origin destination, the current page now redirects in the user browser as well, so commands like `go to Search >> Indexes` can move the visible Orchard Core admin page instead of only updating the mirrored Playwright session.

The chat clients now also capture a compact summary of the real visible page DOM when a prompt is sent, including the current URL, title, headings, visible links, visible buttons, and a short text preview. That live page summary is appended only to the model-facing prompt for the current invocation, so the AI can reason about the page you are actually looking at without polluting the saved user transcript.

The tools are intentionally granular. A typical browser workflow looks like this:

1. Start a browser session.
2. Navigate to a page.
3. Inspect the page state, links, forms, or specific elements.
4. Click, type, select, upload, or wait for UI changes.
5. Use troubleshooting tools when a page does not behave as expected.

### Playwright Browser Installation

The `Microsoft.Playwright` package is included with the AI Agent module, but the actual browser binaries must still be installed for the built application. After building your Orchard Core app, run the generated Playwright install script for the target output folder. For example:

```powershell
pwsh .\src\Modules\CrestApps.OrchardCore.AI.Agent\bin\Debug\net10.0\playwright.ps1 install
```

If the browsers are not installed, the browser tools return a descriptive Playwright error telling you to run the install script.

### Browser Safety and Scope

These tools expose powerful UI automation. Only enable the browser categories on profiles or chat interactions that truly need them. In most cases, it is best to create a dedicated profile for browser-driven tasks rather than making browser automation available everywhere.

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

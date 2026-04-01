---
sidebar_label: AI Agents
sidebar_position: 15
title: AI Agents
description: Delegate tasks to specialized sub-agents that the primary AI model can invoke as tools during orchestration.
---

# AI Agents

> Purpose-built AI profiles that the primary model can invoke as tools — each with its own system prompt, deployment, and capabilities.

## Quick Start

Agents are available automatically when orchestration is enabled:

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices(); // registers AgentToolRegistryProvider
```

Create an agent profile, then link it to a chat profile:

```csharp
// 1. Create an agent profile
var agent = new AIProfile
{
    Type = AIProfileType.Agent,
    Name = "code-reviewer",
    DisplayText = "Code Reviewer",
    Description = "Reviews code for bugs, security issues, and best practices.",
    ChatDeploymentName = "gpt-4o-deployment",
};
agent.Put(new AgentMetadata { Availability = AgentAvailability.OnDemand });

await profileManager.CreateAsync(agent);

// 2. Link it to a chat profile
chatProfile.Put(new AgentInvocationMetadata { Names = ["code-reviewer"] });
await profileManager.UpdateAsync(chatProfile);
```

The primary model can now call the `code-reviewer` agent as a tool during orchestration.

## Problem & Solution

A single AI profile often needs to handle diverse tasks — code review, translation, data analysis, summarization. Cramming all instructions into one system prompt leads to:

- **Conflicting instructions** — a translator prompt fights with a code review prompt
- **Model confusion** — the model struggles with broad, unfocused responsibilities
- **No isolation** — all tasks share the same deployment, token limits, and context

Agents solve this by allowing the primary model to **delegate** to specialized sub-agents:

| Concern | Without Agents | With Agents |
|---------|---------------|-------------|
| System prompt | One monolithic prompt for all tasks | Each agent has a focused prompt |
| Model selection | Single deployment for everything | Each agent can use a different deployment |
| Token budget | Shared across all capabilities | Each agent runs its own completion |
| Scope | Everything in one context | Isolated per-task context |

## How Agents Work

```
User message
    │
    ▼
┌──────────────────┐
│  Primary Model    │  ← Chat profile with tools + agents
│  (Orchestrator)   │
└────────┬─────────┘
         │ calls agent tool
         ▼
┌──────────────────┐
│  AgentProxyTool   │  ← Receives { "prompt": "Review this code..." }
└────────┬─────────┘
         │ builds agent context
         ▼
┌──────────────────┐
│  Agent Model      │  ← Agent profile (own system prompt, deployment)
│  (tools disabled) │
└────────┬─────────┘
         │ returns response
         ▼
┌──────────────────┐
│  Primary Model    │  ← Incorporates agent's response and continues
│  (continues)      │
└──────────────────┘
```

The primary model sees each agent as a regular tool with a `prompt` parameter. It decides when and how to invoke agents based on the user's request and the agent descriptions injected into the system message.

## Agent Availability

The `AgentAvailability` enum controls when an agent is included in orchestration:

| Mode | Behavior | Use Case |
|------|----------|----------|
| `OnDemand` | Included only when explicitly listed in `AgentInvocationMetadata` on the chat profile | Specialized agents (code review, translation) assigned per profile |
| `AlwaysAvailable` | Automatically included in every orchestration request | Core agents needed globally (safety checker, logging agent) |

```csharp
// On-demand: only available when a chat profile explicitly requests it
agent.Put(new AgentMetadata { Availability = AgentAvailability.OnDemand });

// Always available: included in every request automatically
agent.Put(new AgentMetadata { Availability = AgentAvailability.AlwaysAvailable });
```

**Token considerations:** `AlwaysAvailable` agents increase token usage on every request because their descriptions are always present in the system message and their tool definitions are always registered. Use `OnDemand` to minimize cost.

## Creating Agent Profiles

Agent profiles are standard `AIProfile` objects with `Type = AIProfileType.Agent`. They require a `Name` and `Description` at minimum — the description is what the primary model sees when deciding whether to invoke the agent.

```csharp
var translatorAgent = new AIProfile
{
    Type = AIProfileType.Agent,
    Name = "translator",
    DisplayText = "Translator",
    Description = "Translates text between languages. Provide the target language and text to translate.",
    ChatDeploymentName = "gpt-4o-mini-deployment",
};
translatorAgent.Put(new AgentMetadata
{
    Availability = AgentAvailability.OnDemand,
});

await profileManager.CreateAsync(translatorAgent);
```

### Required Fields

| Field | Purpose |
|-------|---------|
| `Type` | Must be `AIProfileType.Agent` |
| `Name` | Unique identifier used as the tool name (becomes `agent:{name}` in the registry) |
| `Description` | Shown to the primary model — drives its decision to invoke this agent |
| `ChatDeploymentName` | The AI deployment used for the agent's completion |

### Optional Configuration

- **System message** — Configure via templates or the profile's system message property
- **AgentMetadata** — Set availability mode (`OnDemand` or `AlwaysAvailable`)

Agents with an empty `Name` or `Description` are silently skipped during registration.

## Linking Agents to Chat Profiles

On-demand agents must be explicitly linked to a chat profile via `AgentInvocationMetadata`:

```csharp
// Make specific agents available to this chat profile
chatProfile.Put(new AgentInvocationMetadata
{
    Names = ["code-reviewer", "translator", "summarizer"],
});

await profileManager.UpdateAsync(chatProfile);
```

The `Names` array maps to agent profile names. At orchestration time, the `AgentToolRegistryProvider` reads these names from `AICompletionContext.AgentNames` and includes only matching agents.

`AlwaysAvailable` agents do **not** need to be listed here — they are included automatically regardless of `AgentInvocationMetadata`.

## Agent Execution Flow

When the primary model invokes an agent tool, the following sequence occurs inside `AgentProxyTool`:

1. **Parse input** — Extract the `prompt` string from the tool call arguments
2. **Resolve agent profile** — Look up the agent by name via `IAIProfileManager.GetAsync(AIProfileType.Agent)`
3. **Build agent context** — Call `IAICompletionContextBuilder.BuildAsync(agentProfile)` to construct the agent's own completion context (system message, settings, etc.)
4. **Disable tools** — Set `context.DisableTools = true` on the agent's context (see [Recursion Prevention](#recursion-prevention))
5. **Resolve deployment** — Find the chat deployment via `IAIDeploymentManager.ResolveOrDefaultAsync()`
6. **Send prompt** — Create a single `ChatMessage` with `ChatRole.User` containing the prompt
7. **Execute completion** — Call `IAICompletionService.CompleteAsync()` with the agent's deployment, messages, and context
8. **Return response** — Extract the assistant's response text and return it to the primary model

```csharp
// Simplified flow inside AgentProxyTool.InvokeCoreAsync:
var context = await contextBuilder.BuildAsync(agentProfile);
context.DisableTools = true;

var deployment = await deploymentManager.ResolveOrDefaultAsync(
    AIDeploymentType.Chat, deploymentName: context.ChatDeploymentName);

var messages = new List<ChatMessage>
{
    new(ChatRole.User, task),
};

var response = await completionService.CompleteAsync(
    deployment, messages, context, cancellationToken);
```

If the agent profile is not found or an error occurs, `AgentProxyTool` returns a descriptive error message to the primary model rather than throwing — allowing the orchestration to continue gracefully.

## Recursion Prevention

Without safeguards, an agent could invoke other agents (or itself), creating an infinite loop. The framework prevents this by **disabling tools on the agent's completion context**:

```csharp
context.DisableTools = true;
```

This means:

- Agents **cannot** call tools, including other agents
- Agents run a single, isolated completion with their own system prompt and the provided prompt
- The agent's response is pure text — no tool calls, no further delegation

This is a deliberate design choice that keeps agent execution predictable and bounded. If you need multi-level delegation, compose it at the chat profile level by having multiple agents available to the primary model, which can invoke them sequentially.

## System Message Enrichment

The `AgentOrchestrationContextBuilderHandler` automatically enriches the primary model's system message with descriptions of all available agents. This gives the model awareness of which agents exist and what they can do, enabling informed routing decisions.

The handler:

1. Reads all agent profiles via `IAIProfileManager`
2. Filters to agents matching the availability criteria
3. Renders agent descriptions using the `AITemplateIds.AgentAvailability` template
4. Appends the rendered text to the orchestration context's `SystemMessageBuilder`

This follows the industry-standard pattern used by orchestration frameworks where agent descriptions are included in the system prompt so the model can decide which capabilities to invoke.

## Implementing `IAIProfileManager`

The host application must provide an implementation of `IAIProfileManager` for agents to work. The agent subsystem relies on two key operations:

```csharp
public interface IAIProfileManager
{
    // Used by AgentToolRegistryProvider and AgentProxyTool to fetch agent profiles
    Task<IReadOnlyList<AIProfile>> GetAsync(AIProfileType type);

    // Used to persist agent profiles
    Task CreateAsync(AIProfile profile);
    Task UpdateAsync(AIProfile profile);
}
```

The `GetAsync(AIProfileType.Agent)` call is the primary query used by:

- **`AgentToolRegistryProvider`** — to discover agents and build tool entries
- **`AgentProxyTool`** — to resolve the target agent at invocation time
- **`AgentOrchestrationContextBuilderHandler`** — to enrich the system message with agent descriptions

Your implementation must return agent profiles with their `Properties` intact (including `AgentMetadata`) for availability filtering to work correctly.

## Services Registered

`AddOrchestrationServices()` registers the following agent-related services:

| Service | Implementation | Purpose |
|---------|---------------|---------|
| `IToolRegistryProvider` | `AgentToolRegistryProvider` | Exposes agents as tool entries |
| `IOrchestrationContextBuilderHandler` | `AgentOrchestrationContextBuilderHandler` | Enriches system message with agent descriptions |

Both are registered as **scoped** services via `TryAddEnumerable`, ensuring they participate alongside other tool providers and context handlers.

## Orchard Core Integration

The [AI Agent module](../ai/agent.md) wraps this framework with admin UI for creating and managing agent profiles, configuring availability modes, assigning agents to chat profiles, and testing agent invocations from the dashboard.

---
sidebar_label: Context Builders
sidebar_position: 10
title: Context Builders
description: Enrich AI completion and orchestration contexts with custom data, instructions, or constraints.
---

# Context Builders

> Handler pipelines that enrich the AI context before completion — inject custom instructions, data, constraints, or metadata.

## Quick Start

Register a custom context builder handler:

```csharp
builder.Services.AddScoped<IAICompletionContextBuilderHandler, MyContextHandler>();
```

## Problem & Solution

Before sending messages to an LLM, applications often need to:

- **Inject user-specific instructions** (role, permissions, preferences)
- **Add retrieval data** (RAG documents, knowledge base excerpts)
- **Set constraints** (token limits, response format, allowed tools)
- **Attach metadata** (session info, tenant context, request details)

Context builders provide a two-phase handler pipeline (`Building` → `Built`) that runs automatically.

## Two Handler Levels

The framework provides context building at two levels:

| Level | Interface | Runs During | Use Case |
|-------|-----------|------------|----------|
| **Completion** | `IAICompletionContextBuilderHandler` | `IAICompletionContextBuilder.BuildAsync()` | Low-level context enrichment |
| **Orchestration** | `IOrchestrationContextBuilderHandler` | `IOrchestrationContextBuilder.BuildAsync()` | High-level orchestration enrichment |

## `IAICompletionContextBuilderHandler`

Enriches the `AICompletionContext` during the build phase.

```csharp
public interface IAICompletionContextBuilderHandler
{
    Task BuildingAsync(AICompletionContextBuildingContext context);
    Task BuiltAsync(AICompletionContextBuiltContext context);
}
```

### Lifecycle

1. `BuildingAsync` — Called **before** the context is finalized. Add system messages, modify options, inject data.
2. `BuiltAsync` — Called **after** the context is built. Validate, log, or make final adjustments.

### Built-in Handlers

| Handler | Purpose |
|---------|---------|
| `AIProfileCompletionContextBuilderHandler` | Adds profile-level system messages and settings |
| `DataSourceAICompletionContextBuilderHandler` | Injects RAG data from configured data sources |
| `ChatInteractionCompletionContextBuilderHandler` | Adds chat history and interaction context |
| `A2AAICompletionContextBuilderHandler` | Enriches with A2A agent information |

### Example

```csharp
public sealed class TenantContextHandler : IAICompletionContextBuilderHandler
{
    private readonly ITenantService _tenantService;

    public TenantContextHandler(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        var tenant = await _tenantService.GetCurrentAsync();
        context.SystemMessages.Add($"Current tenant: {tenant.Name}. Region: {tenant.Region}.");
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
```

Register:

```csharp
builder.Services.AddScoped<IAICompletionContextBuilderHandler, TenantContextHandler>();
```

## `IOrchestrationContextBuilderHandler`

Enriches the `OrchestrationContext` at the orchestration level.

```csharp
public interface IOrchestrationContextBuilderHandler
{
    Task BuildingAsync(OrchestrationContextBuildingContext context);
    Task BuiltAsync(OrchestrationContextBuiltContext context);
}
```

### Built-in Handlers

| Handler | Purpose |
|---------|---------|
| `CompletionContextOrchestrationHandler` | Bridges completion context into orchestration |
| `PreemptiveRagOrchestrationHandler` | Pre-fetches RAG data before orchestration |
| `AIToolExecutionContextOrchestrationHandler` | Sets up tool execution context |
| `DocumentOrchestrationHandler` | Injects document processing context |
| `CopilotOrchestrationContextHandler` | Adds Copilot-specific context |

## Choosing the Right Level

| Scenario | Use |
|----------|-----|
| Add data to every completion call (even non-orchestrated) | `IAICompletionContextBuilderHandler` |
| Add data only during orchestrated conversations | `IOrchestrationContextBuilderHandler` |
| Both | Register handlers at both levels |

## Execution Order

Handlers are resolved from DI and executed in **reverse registration order** — the last-registered handler runs first. This follows the middleware pattern:

```text
Handler Registration Order:    A → B → C
Execution Order (BuildingAsync):  C → B → A
    ↓
  (context is finalized)
    ↓
Execution Order (BuiltAsync):     C → B → A
```

The two-phase lifecycle works as follows:

1. **`BuildingAsync`** is called on all handlers (in reverse registration order). This is the place to add system messages, inject data, or modify options before the context is finalized.
2. The caller's optional configuration delegate runs (if one was provided to `BuildAsync`).
3. **`BuiltAsync`** is called on all handlers (same reverse order). This is the place to validate, log, or make final adjustments to the fully-built context.

:::info
Register your handler **after** built-in handlers if you want it to run **first** (e.g., to override values they set). Register it **before** them if you want it to run **last** (e.g., to validate the final state).
:::

## `IOrchestrationContextBuilderHandler` Example

The orchestration level runs during orchestrated conversations (e.g., the chat interaction pipeline). Here is a full implementation that injects user memory and tool availability into the orchestration context:

```csharp
public sealed class UserPreferencesOrchestrationHandler(
    IHttpContextAccessor httpContextAccessor,
    ITemplateService templateService) : IOrchestrationContextBuilderHandler
{
    public Task BuildingAsync(OrchestrationContextBuildingContext context)
    {
        // BuildingAsync runs before the caller's configuration.
        // Use this phase to set defaults that the caller can override.
        return Task.CompletedTask;
    }

    public async Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        // BuiltAsync runs after the context is fully constructed.
        // Access the completion context to inject data.
        if (context.OrchestrationContext.CompletionContext is null)
        {
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Render a template with user-specific arguments
        var instructions = await templateService.RenderAsync(
            "user-preferences",
            new Dictionary<string, object>
            {
                ["user_id"] = userId,
                ["user_name"] = httpContext.User.Identity.Name,
                ["locale"] = httpContext.Request.Headers.AcceptLanguage.FirstOrDefault() ?? "en",
            });

        if (!string.IsNullOrEmpty(instructions))
        {
            context.OrchestrationContext.SystemMessageBuilder.AppendLine();
            context.OrchestrationContext.SystemMessageBuilder.Append(instructions);
        }
    }
}
```

Register:

```csharp
builder.Services.AddScoped<IOrchestrationContextBuilderHandler, UserPreferencesOrchestrationHandler>();
```

## Conditional Context Injection

Inject context only when specific conditions are met. This avoids polluting the system prompt with irrelevant information:

```csharp
public sealed class BusinessHoursContextHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        var now = DateTimeOffset.UtcNow;
        var isBusinessHours = now.Hour >= 9 && now.Hour < 17 && now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday;

        if (!isBusinessHours)
        {
            context.SystemMessages.Add(
                "It is currently outside business hours. " +
                "If the user needs urgent help, suggest they call our 24/7 emergency line at 1-800-HELP.");
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
```

### Profile-Scoped Context

Inject context only for specific AI profiles:

```csharp
public sealed class LegalDisclaimerHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        // Only inject for profiles tagged as "legal"
        if (context.Profile?.Type != "Legal")
        {
            return Task.CompletedTask;
        }

        context.SystemMessages.Add(
            "IMPORTANT: You are providing legal information, not legal advice. " +
            "Always recommend the user consult a qualified attorney for their specific situation.");

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
```

## Common Patterns

### RAG Injection

Inject retrieved documents into the context for retrieval-augmented generation:

```csharp
public sealed class RagContextHandler(
    ISearchService searchService) : IAICompletionContextBuilderHandler
{
    public async Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        // Get the latest user message as the search query
        var lastMessage = context.Messages?.LastOrDefault()?.Text;
        if (string.IsNullOrEmpty(lastMessage))
        {
            return;
        }

        var results = await searchService.SearchAsync(lastMessage, maxResults: 5);

        if (results.Any())
        {
            var ragContext = new StringBuilder();
            ragContext.AppendLine("## Retrieved Documents");
            ragContext.AppendLine("Use the following documents to answer the user's question:");
            ragContext.AppendLine();

            foreach (var result in results)
            {
                ragContext.AppendLine($"### {result.Title}");
                ragContext.AppendLine(result.Content);
                ragContext.AppendLine();
            }

            context.SystemMessages.Add(ragContext.ToString());
        }
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
```

### User Info Injection

Inject authenticated user details so the AI can personalize responses:

```csharp
public sealed class UserInfoContextHandler(
    IHttpContextAccessor httpContextAccessor) : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.SystemMessages.Add("The user is not authenticated. Do not access personalized data.");
            return Task.CompletedTask;
        }

        var info = new StringBuilder("## Current User Information");
        info.AppendLine();
        info.AppendLine($"- **Name**: {user.Identity.Name}");
        info.AppendLine($"- **Email**: {user.FindFirstValue(ClaimTypes.Email) ?? "unknown"}");

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value);
        if (roles.Any())
        {
            info.AppendLine($"- **Roles**: {string.Join(", ", roles)}");
        }

        context.SystemMessages.Add(info.ToString());

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
```

### Permission-Based Context

Restrict what the AI can access based on the user's permissions:

```csharp
public sealed class PermissionContextHandler(
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationService authorizationService) : IAICompletionContextBuilderHandler
{
    public async Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return;
        }

        var restrictions = new List<string>();

        if (!(await authorizationService.AuthorizeAsync(user, "ViewFinancialData")).Succeeded)
        {
            restrictions.Add("Do NOT provide financial data, revenue numbers, or budget information.");
        }

        if (!(await authorizationService.AuthorizeAsync(user, "ViewEmployeeData")).Succeeded)
        {
            restrictions.Add("Do NOT provide employee personal information or HR records.");
        }

        if (restrictions.Count > 0)
        {
            context.SystemMessages.Add(
                "## Access Restrictions\n" + string.Join("\n", restrictions));
        }
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
```

## Orchard Core Integration

In Orchard Core, context builders are used internally by AI modules. Custom modules can register additional handlers via their `Startup` class.

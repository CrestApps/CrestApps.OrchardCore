---
sidebar_label: A2A Client
sidebar_position: 2
title: A2A Client
description: Discover and invoke remote AI agents using the A2A protocol client — connection management, tool registry integration, authentication, and built-in discovery tools.
---

# A2A Client

> Discover and invoke remote AI agents by registering A2A connections, fetching their Agent Cards, and exposing their skills as tools in the orchestrator.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddCrestAppsA2AClient();
```

This single call registers everything needed to consume remote A2A agents: HTTP infrastructure, agent card caching, tool registry integration, authentication services, and three built-in discovery tools.

## Problem & Solution

Your AI application needs to delegate tasks to agents running in other applications — a translation service, a code-review agent, a document-summarization agent. Each remote agent has its own AI model, tools, and reasoning. You need a way to:

- **Discover** what remote agents can do (without hardcoding their capabilities)
- **Invoke** remote agents as if they were local tools
- **Authenticate** with each remote host (API keys, OAuth2, certificates)
- **Cache** agent metadata for performance

The A2A client solves all of this. It fetches Agent Cards from remote hosts, converts each advertised skill into a tool registry entry, and proxies tool calls to the remote agent transparently.

## Services Registered by `AddCrestAppsA2AClient()`

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `HttpClient` | via `IHttpClientFactory` | — | HTTP communication with remote hosts |
| `IMemoryCache` | — | Singleton | Caching infrastructure for agent cards and OAuth2 tokens |
| `IHttpContextAccessor` | `HttpContextAccessor` | Singleton | Access to the current HTTP context for scoped service resolution |
| `IAICompletionContextBuilderHandler` | `A2AAICompletionContextBuilderHandler` | Scoped | Copies A2A connection IDs from the AI profile into the completion context |
| `IToolRegistryProvider` | `A2AToolRegistryProvider` | Scoped | Discovers remote agent skills and exposes them as tool entries |
| `IA2AAgentCardCacheService` | `DefaultA2AAgentCardCacheService` | Singleton | Fetches and caches Agent Cards from remote hosts (15-minute TTL) |
| `IA2AConnectionAuthService` | `DefaultA2AConnectionAuthService` | Scoped | Builds HTTP authentication headers for each connection |

### Built-in Tools

Three system tools are registered automatically:

| Tool Name | Class | Purpose |
|-----------|-------|---------|
| `listAvailableAgents` | `ListAvailableAgentsFunction` | Lists all available agents — both local AI profiles and remote A2A agents |
| `findAgentForTask` | `FindAgentForTaskFunction` | Finds the best agents for a given task using keyword matching |
| `findToolsForTask` | `FindToolsForTaskFunction` | Discovers tools (including remote agent skills) relevant to a task |

## How It All Fits Together

```text
┌─────────────────────────────────────────────────────────────────┐
│                        Your Application                         │
│                                                                 │
│  1. AI Profile has A2AConnectionIds = ["conn-abc", "conn-xyz"]  │
│                         │                                       │
│  2. A2AAICompletionContextBuilderHandler copies connection IDs  │
│     into AICompletionContext.A2AConnectionIds                   │
│                         │                                       │
│  3. A2AToolRegistryProvider.GetToolsAsync() runs:               │
│     ┌───────────────────▼──────────────────┐                    │
│     │ For each connection ID:              │                    │
│     │  a. Load A2AConnection from store    │                    │
│     │  b. Fetch Agent Card (cached 15 min) │                    │
│     │  c. For each skill on the card:      │                    │
│     │     → Create ToolRegistryEntry       │                    │
│     │       Id: "a2a:{connId}:{skillName}" │                    │
│     │       Source: A2AAgent               │                    │
│     │       Factory: → A2AAgentProxyTool   │                    │
│     └──────────────────────────────────────┘                    │
│                         │                                       │
│  4. AI model sees remote skills as invokable tools              │
│                         │                                       │
│  5. Model calls a tool → A2AAgentProxyTool executes:            │
│     a. Load connection + auth metadata                          │
│     b. Configure HttpClient with auth headers                   │
│     c. Send AgentMessage to remote endpoint                     │
│     d. Extract text from response                               │
│     e. Return text to the AI model                              │
└─────────────────────────────────────────────────────────────────┘
```

## Connection Management

### The `A2AConnection` Model

Every remote A2A host is represented by an `A2AConnection`:

```csharp
public sealed class A2AConnection : CatalogItem, IDisplayTextAwareModel
{
    // Human-readable name for the connection (e.g., "Legal Review Agent")
    public string DisplayText { get; set; }

    // The remote A2A host's base URL (e.g., "https://agents.example.com/a2a")
    public string Endpoint { get; set; }

    // When the connection was created
    public DateTime CreatedUtc { get; set; }

    // Who created the connection
    public string Author { get; set; }

    // Owner user ID
    public string OwnerId { get; set; }
}
```

`A2AConnection` extends `CatalogItem`, which means it supports the `Properties` dictionary for extensible metadata. Authentication details are stored as an `A2AConnectionMetadata` object in this dictionary.

### Implementing `ICatalog<A2AConnection>`

The framework defines the `A2AConnection` model but does **not** include a built-in store. You must implement `ICatalog<A2AConnection>` to persist connections in your data store:

```csharp
public sealed class MyA2AConnectionStore : ICatalog<A2AConnection>
{
    private readonly IDbConnection _db;

    public MyA2AConnectionStore(IDbConnection db)
    {
        _db = db;
    }

    public Task<A2AConnection> FindByIdAsync(string id)
    {
        // Load from your database
    }

    public Task<IEnumerable<A2AConnection>> GetAllAsync()
    {
        // Return all configured connections
    }

    // ... other CRUD methods
}

// Register in DI
builder.Services.AddScoped<ICatalog<A2AConnection>, MyA2AConnectionStore>();
```

:::tip Orchard Core users
The [A2A Client module](../../ai/a2a/client.md) provides a full admin UI and YesSql-backed store for managing connections — no custom implementation needed.
:::

### Assigning Connections to AI Profiles

Connections are linked to AI profiles via `AIProfileA2AMetadata`:

```csharp
public sealed class AIProfileA2AMetadata
{
    // The IDs of A2A connections available to this profile
    public string[] ConnectionIds { get; set; }
}
```

When the orchestrator builds the completion context, `A2AAICompletionContextBuilderHandler` reads these IDs from the profile and sets them on `AICompletionContext.A2AConnectionIds`. This tells `A2AToolRegistryProvider` which remote hosts to query for tools.

```csharp
// How the handler works internally:
internal sealed class A2AAICompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is AIProfile profile &&
            profile.TryGet<AIProfileA2AMetadata>(out var a2aMetadata))
        {
            context.Context.A2AConnectionIds = a2aMetadata.ConnectionIds;
        }

        return Task.CompletedTask;
    }
}
```

## Agent Card Discovery & Caching

### What Is an Agent Card?

An Agent Card is a JSON document published by an A2A host at a well-known URL (typically `/.well-known/agent.json`). It describes:

- The agent's name and description
- A list of **skills** (capabilities the agent can perform)
- Each skill's ID, name, description, and tags
- The endpoint URL for sending messages

### `IA2AAgentCardCacheService`

The framework caches Agent Cards in memory to avoid fetching them on every request:

```csharp
public interface IA2AAgentCardCacheService
{
    /// Fetches the Agent Card for a connection, using a cached value if available.
    Task<AgentCard> GetAgentCardAsync(
        string connectionId,
        A2AConnection connection,
        CancellationToken cancellationToken = default);

    /// Removes the cached Agent Card for a connection.
    void Invalidate(string connectionId);
}
```

The default implementation (`DefaultA2AAgentCardCacheService`) caches cards for **15 minutes** using `IMemoryCache`. It:

1. Checks the cache using key `A2AAgentCard:{connectionId}`
2. On cache miss, creates an `HttpClient` and configures it with authentication headers
3. Uses `A2ACardResolver` to fetch the Agent Card from the remote host
4. Caches the result and returns it

To customize caching behavior (e.g., use distributed cache, change TTL), register your own implementation:

```csharp
builder.Services.AddSingleton<IA2AAgentCardCacheService, MyCustomAgentCardCacheService>();
```

## Tool Registry Integration

### How Remote Agents Become Tools

`A2AToolRegistryProvider` implements `IToolRegistryProvider` and is called by the orchestrator when building the tool set for a completion request.

For each connection ID in `AICompletionContext.A2AConnectionIds`:

1. **Load** the `A2AConnection` from the catalog
2. **Fetch** the Agent Card (cached)
3. **Iterate** skills on the Agent Card
4. **Create** a `ToolRegistryEntry` for each skill:

```csharp
new ToolRegistryEntry
{
    Id = $"a2a:{connectionId}:{skillName}",    // Unique tool ID
    Name = skillName,                           // Sanitized skill name
    Description = skill.Description,            // Shown to the AI model
    Source = ToolRegistryEntrySource.A2AAgent,   // Identifies this as an A2A tool
    SourceId = connectionId,                     // Links back to the connection
    CreateAsync = _ => new A2AAgentProxyTool(...) // Factory for the proxy tool
}
```

The tool name is sanitized to contain only letters, digits, and underscores — ensuring compatibility with AI model function-calling requirements.

## Agent Proxy Execution

When the AI model decides to invoke a remote agent skill, `A2AAgentProxyTool` handles the execution.

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "message": {
      "type": "string",
      "description": "The message or task to send to the remote agent for processing."
    },
    "contextId": {
      "type": "string",
      "description": "An optional context identifier to maintain conversation continuity with the remote agent."
    }
  },
  "required": ["message"]
}
```

### Execution Flow

```text
1. Validate input — "message" is required
2. Create HttpClient via IHttpClientFactory
3. Load connection from ICatalog<A2AConnection>
4. Read A2AConnectionMetadata from connection properties
5. Configure HttpClient with authentication headers
6. Create A2AClient pointing at the remote endpoint
7. Build AgentMessage:
   - Role: User
   - MessageId: new GUID
   - ContextId: provided or new GUID
   - Parts: [TextPart with the message]
   - Metadata: { "agentName": skill name }
8. Send message via client.SendMessageAsync()
9. Extract text from response:
   - AgentMessage → join TextParts
   - AgentTask → check Artifacts, then Status.Message
10. Return text to the AI model
```

### Response Handling

The proxy tool handles two types of A2A responses:

- **`AgentMessage`** — Direct response with text parts (synchronous completion)
- **`AgentTask`** — Task-based response where text may be in artifacts or status messages (async workflows)

If no text can be extracted, the tool returns: `"The remote agent did not produce a text response."`

If communication fails, the error is logged and a user-friendly message is returned (the exception is not propagated to the AI model).

## Built-in Discovery Tools

The A2A client registers three system tools that help the AI model discover available agents and tools at runtime.

### `ListAvailableAgentsFunction`

Lists **all** available agents — both local AI Agent profiles and remote agents from A2A connections.

- **Tool name**: `listAvailableAgents`
- **Parameters**: None
- **Returns**: JSON array of agents with `name`, `id`, `description`, `source` ("local" or "remote"), and optionally `host` and `tags`

```text
AI Model: "What agents are available?"
→ Calls listAvailableAgents
→ Returns:
[
  { "name": "Code Reviewer", "id": "code-reviewer", "source": "local" },
  { "name": "Legal Analyst", "id": "legal-analyst", "source": "remote", "host": "Legal Team" }
]
```

### `FindAgentForTaskFunction`

Finds the most relevant agents for a given task using keyword and semantic matching.

- **Tool name**: `findAgentForTask`
- **Parameters**:
  - `taskDescription` (string, required) — What the task is about
  - `maxResults` (integer, optional) — Maximum agents to return (default: 5)
- **Returns**: JSON array of agents ranked by relevance score

The function tokenizes the task description and scores each agent based on keyword overlap between the query and the agent's name + description + tags. Both forward and reverse matching are used to balance precision and recall.

### `FindToolsForTaskFunction`

Discovers tools (including remote agent skills) relevant to a given task.

- **Tool name**: `findToolsForTask`
- **Parameters**:
  - `taskDescription` (string, required) — What the task is about
  - `maxResults` (integer, optional) — Maximum tools to return (default: 10)
- **Returns**: JSON array of tools with `name`, `description`, and `source`

This function delegates to `IToolRegistry.SearchAsync()` to use the same scoring logic as the orchestrator's tool-scoping system. It automatically includes all A2A connections when building the search context.

## Authentication

### `IA2AConnectionAuthService`

The authentication service builds HTTP headers for each connection based on its configured authentication type:

```csharp
public interface IA2AConnectionAuthService
{
    /// Builds authentication headers from connection metadata.
    Task<Dictionary<string, string>> BuildHeadersAsync(
        A2AConnectionMetadata metadata,
        CancellationToken cancellationToken = default);

    /// Configures an HttpClient with authentication headers.
    Task ConfigureHttpClientAsync(
        HttpClient httpClient,
        A2AConnectionMetadata metadata,
        CancellationToken cancellationToken = default);
}
```

### Supported Authentication Types

Authentication metadata is stored in `A2AConnectionMetadata`:

| Type | Enum Value | How It Works |
|------|-----------|--------------|
| **Anonymous** | `Anonymous` | No authentication headers added |
| **API Key** | `ApiKey` | Sends the key in a configurable header (default: `Authorization`) with optional prefix (e.g., `Bearer`) |
| **Basic** | `Basic` | Base64-encodes `username:password` and sends as `Authorization: Basic {encoded}` |
| **OAuth2 Client Credentials** | `OAuth2ClientCredentials` | Exchanges `client_id` + `client_secret` for a bearer token at the token endpoint |
| **OAuth2 Private Key JWT** | `OAuth2PrivateKeyJwt` | Creates a signed JWT assertion using an RSA private key and exchanges it for a token |
| **OAuth2 Mutual TLS** | `OAuth2Mtls` | Uses a client certificate for mutual TLS authentication when requesting a token |
| **Custom Headers** | `CustomHeaders` | Sends arbitrary key-value pairs as HTTP headers |

### `A2AConnectionMetadata` Properties

```csharp
public sealed class A2AConnectionMetadata
{
    // Which authentication type to use
    public A2AClientAuthenticationType AuthenticationType { get; set; }

    // API Key authentication
    public string ApiKeyHeaderName { get; set; }   // Default: "Authorization"
    public string ApiKeyPrefix { get; set; }        // e.g., "Bearer"
    public string ApiKey { get; set; }              // The key (encrypted via DataProtection)

    // Basic authentication
    public string BasicUsername { get; set; }
    public string BasicPassword { get; set; }       // Encrypted via DataProtection

    // OAuth 2.0 Client Credentials
    public string OAuth2TokenEndpoint { get; set; }
    public string OAuth2ClientId { get; set; }
    public string OAuth2ClientSecret { get; set; }  // Encrypted via DataProtection
    public string OAuth2Scopes { get; set; }

    // OAuth 2.0 Private Key JWT
    public string OAuth2PrivateKey { get; set; }    // PEM-encoded RSA private key (encrypted)
    public string OAuth2KeyId { get; set; }

    // OAuth 2.0 Mutual TLS (mTLS)
    public string OAuth2ClientCertificate { get; set; }          // Base64 PKCS#12 (encrypted)
    public string OAuth2ClientCertificatePassword { get; set; }  // Encrypted

    // Custom headers
    public Dictionary<string, string> AdditionalHeaders { get; set; }
}
```

### Credential Protection

All sensitive fields (API keys, passwords, secrets, private keys, certificates) are encrypted using ASP.NET Core Data Protection with the purpose string `"A2AClientConnection"`. The `DefaultA2AConnectionAuthService` automatically decrypts values before use.

OAuth2 tokens are cached in `IMemoryCache` with a TTL based on the token's `expires_in` minus a 60-second buffer.

### Custom Authentication

To implement a custom authentication scheme, register your own `IA2AConnectionAuthService`:

```csharp
public sealed class MyA2AAuthService : IA2AConnectionAuthService
{
    public Task<Dictionary<string, string>> BuildHeadersAsync(
        A2AConnectionMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Your custom logic — e.g., fetch tokens from a vault
        headers["Authorization"] = "Bearer " + GetTokenFromVault();

        return Task.FromResult(headers);
    }

    public async Task ConfigureHttpClientAsync(
        HttpClient httpClient,
        A2AConnectionMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var headers = await BuildHeadersAsync(metadata, cancellationToken);

        foreach (var header in headers)
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}

// Register (replaces the default)
builder.Services.AddScoped<IA2AConnectionAuthService, MyA2AAuthService>();
```

## Configuration Examples

### Minimal Setup (anonymous remote agent)

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddCrestAppsA2AClient();

// Register your connection store
builder.Services.AddScoped<ICatalog<A2AConnection>, MyA2AConnectionStore>();
```

### With API Key Authentication

```csharp
// When creating a connection, store the metadata:
var connection = new A2AConnection
{
    DisplayText = "Partner Translation Service",
    Endpoint = "https://translate.partner.com/a2a",
};

var metadata = new A2AConnectionMetadata
{
    AuthenticationType = A2AClientAuthenticationType.ApiKey,
    ApiKeyHeaderName = "Authorization",
    ApiKeyPrefix = "Bearer",
    ApiKey = protector.Protect("sk-partner-key-12345"),
};

connection.Put(metadata);

await connectionStore.CreateAsync(connection);
```

### With OAuth2 Client Credentials

```csharp
var metadata = new A2AConnectionMetadata
{
    AuthenticationType = A2AClientAuthenticationType.OAuth2ClientCredentials,
    OAuth2TokenEndpoint = "https://auth.partner.com/oauth2/token",
    OAuth2ClientId = "my-app-client-id",
    OAuth2ClientSecret = protector.Protect("my-client-secret"),
    OAuth2Scopes = "a2a.invoke",
};
```

## Orchard Core Integration

The [A2A Client module](../../ai/a2a/client.md) provides:

- Admin UI for creating and managing A2A connections
- Built-in `ICatalog<A2AConnection>` backed by YesSql
- Authentication configuration forms for all supported types
- Connection assignment to AI profiles via the profile editor
- Agent card preview and cache invalidation

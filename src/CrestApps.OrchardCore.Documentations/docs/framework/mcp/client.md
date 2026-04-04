---
sidebar_label: MCP Client
sidebar_position: 2
title: MCP Client
description: Connect to remote MCP servers to discover and use their tools, prompts, and resources in AI orchestration.
---

# MCP Client

> Connect to remote MCP servers, discover their capabilities, and make their tools available to the AI orchestrator.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddCrestAppsMcpClient();
```

This registers transport providers, OAuth2 support, the core `McpService` that manages connections to remote MCP servers, and the shared AI-profile completion-context handler that flows selected MCP connection IDs into the completion request.

## Problem & Solution

AI applications often need to call tools hosted on external servers — code execution sandboxes, data retrieval APIs, domain-specific utilities. The [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) standardizes how clients discover and invoke those remote capabilities.

The MCP client framework:

- Connects to remote MCP servers over **SSE** (HTTP) or **StdIO** (local process)
- Discovers tools, prompts, and resources from each server
- Makes discovered tools available in the AI orchestrator's tool registry
- Proxies tool calls transparently to the correct remote server
- Supports multiple authentication methods for secure connections

## Registered Services

`AddCrestAppsMcpClient()` registers these services:

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `McpService` | — | Scoped | Creates MCP clients for configured connections |
| `IOAuth2TokenService` | `DefaultOAuth2TokenService` | Scoped | OAuth2 token acquisition and caching |
| `IMcpClientTransportProvider` | `SseClientTransportProvider` | Scoped | Server-Sent Events transport |
| `IMcpClientTransportProvider` | `StdioClientTransportProvider` | Scoped | Standard I/O transport |
| `IAICompletionContextBuilderHandler` | `McpAICompletionContextBuilderHandler` | Scoped | Copies selected MCP connection IDs from AI profile metadata into the completion context |

Two transport types are automatically registered in `McpClientAIOptions`:

| Type Key | Display Name | Description |
|----------|-------------|-------------|
| `sse` | Server-Sent Events | Uses a remote MCP server over HTTP |
| `stdIo` | Standard Input/Output | Uses a local MCP process over standard input/output |

## Transport Types

### SSE (Server-Sent Events)

Use SSE to connect to remote MCP servers over HTTP. The `SseMcpConnectionMetadata` model configures the connection:

```csharp
public sealed class SseMcpConnectionMetadata
{
    public Uri Endpoint { get; set; }
    public McpClientAuthenticationType AuthenticationType { get; set; }

    // API Key
    public string ApiKeyHeaderName { get; set; }
    public string ApiKeyPrefix { get; set; }
    public string ApiKey { get; set; }

    // Basic Auth
    public string BasicUsername { get; set; }
    public string BasicPassword { get; set; }

    // OAuth2 Client Credentials
    public string OAuth2TokenEndpoint { get; set; }
    public string OAuth2ClientId { get; set; }
    public string OAuth2ClientSecret { get; set; }
    public string OAuth2Scopes { get; set; }

    // OAuth2 Private Key JWT
    public string OAuth2PrivateKey { get; set; }
    public string OAuth2KeyId { get; set; }

    // OAuth2 Mutual TLS
    public string OAuth2ClientCertificate { get; set; }
    public string OAuth2ClientCertificatePassword { get; set; }

    // Custom Headers
    public Dictionary<string, string> AdditionalHeaders { get; set; }
}
```

The transport provider reads this metadata from the `McpConnection` and builds the appropriate HTTP headers before opening the SSE stream. Credentials stored via Data Protection are decrypted at connection time.

### StdIO (Standard I/O)

Use StdIO to communicate with locally installed MCP server processes via stdin/stdout. The `StdioMcpConnectionMetadata` model configures the process:

```csharp
public sealed class StdioMcpConnectionMetadata
{
    public string Command { get; set; }
    public string[] Arguments { get; set; }
    public string WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; }
}
```

**Example** — connect to a local Python MCP server:

```csharp
// The StdIO transport spawns the process and communicates over stdin/stdout.
var metadata = new StdioMcpConnectionMetadata
{
    Command = "python",
    Arguments = ["-m", "my_mcp_server"],
    WorkingDirectory = "/opt/tools",
    EnvironmentVariables = new Dictionary<string, string>
    {
        ["MY_API_KEY"] = "sk-..."
    }
};
```

## Authentication

The SSE transport supports these authentication types via the `McpClientAuthenticationType` enum:

| Type | Description |
|------|-------------|
| `Anonymous` | No authentication |
| `ApiKey` | Custom API key sent via a configurable header and prefix |
| `Basic` | HTTP Basic authentication (username + password) |
| `OAuth2ClientCredentials` | OAuth2 client credentials grant |
| `OAuth2PrivateKeyJwt` | OAuth2 with private key JWT assertion (RS256) |
| `OAuth2Mtls` | OAuth2 with mutual TLS client certificate |
| `CustomHeaders` | Arbitrary custom headers for legacy or proprietary auth |

### OAuth2 Token Service

The `IOAuth2TokenService` handles token acquisition for all OAuth2 flows:

```csharp
public interface IOAuth2TokenService
{
    Task<string> AcquireTokenAsync(
        string tokenEndpoint, string clientId,
        string clientSecret, string scopes = null,
        CancellationToken cancellationToken = default);

    Task<string> AcquireTokenWithPrivateKeyJwtAsync(
        string tokenEndpoint, string clientId,
        string privateKeyPem, string keyId = null,
        string scopes = null,
        CancellationToken cancellationToken = default);

    Task<string> AcquireTokenWithMtlsAsync(
        string tokenEndpoint, string clientId,
        byte[] clientCertificateBytes,
        string certificatePassword = null,
        string scopes = null,
        CancellationToken cancellationToken = default);
}
```

The default implementation (`DefaultOAuth2TokenService`) caches tokens in `IMemoryCache` with a 60-second expiration buffer to prevent token expiry during active requests.

## Connection Management

### McpConnection

MCP connections are stored as catalog entries via `McpConnection`:

```csharp
public sealed class McpConnection : SourceCatalogEntry, IDisplayTextAwareModel
{
    public string DisplayText { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Author { get; set; }
    public string OwnerId { get; set; }
}
```

The `Source` property (inherited from `SourceCatalogEntry`) indicates the transport type — `"sse"` or `"stdIo"`. Transport-specific metadata (e.g., `SseMcpConnectionMetadata`) is stored in the `Properties` bag and retrieved using `connection.As<SseMcpConnectionMetadata>()`.

### McpService

`McpService` is the central service for creating MCP clients from connections:

```csharp
public sealed class McpService
{
    public async Task<McpClient> GetOrCreateClientAsync(McpConnection connection);
}
```

It iterates through registered `IMcpClientTransportProvider` implementations to find one that supports the connection's transport type, then creates an `IClientTransport` and returns an `McpClient` from the [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

### IMcpClientTransportProvider

Transport providers implement this interface to support different connection mechanisms:

```csharp
public interface IMcpClientTransportProvider
{
    bool CanHandle(McpConnection connection);
    Task<IClientTransport> GetAsync(McpConnection connection);
}
```

The framework registers `SseClientTransportProvider` and `StdioClientTransportProvider` by default. You can add custom transport providers by registering additional `IMcpClientTransportProvider` implementations.

## Server Metadata Caching

### IMcpServerMetadataCacheProvider

Before tools can be discovered, the framework queries MCP servers for their capabilities and caches the results:

```csharp
public interface IMcpServerMetadataCacheProvider
{
    Task<McpServerCapabilities> GetCapabilitiesAsync(McpConnection connection);
    Task InvalidateAsync(string connectionId);
}
```

The `McpServerCapabilities` model holds the complete set of capabilities from a server:

```csharp
public sealed class McpServerCapabilities
{
    public string ConnectionId { get; set; }
    public string ConnectionDisplayText { get; set; }
    public IReadOnlyList<McpServerCapability> Tools { get; set; }
    public IReadOnlyList<McpServerCapability> Prompts { get; set; }
    public IReadOnlyList<McpServerCapability> Resources { get; set; }
    public IReadOnlyList<McpServerCapability> ResourceTemplates { get; set; }
    public DateTime FetchedUtc { get; set; }
    public bool IsHealthy { get; set; }
}
```

Each `McpServerCapability` captures a tool's name, description, input schema, or a resource's URI and MIME type.

## Tool Discovery & Invocation

In Orchard Core, two additional components bridge MCP connections into the orchestrator:

### McpToolRegistryProvider

Implements `IToolRegistryProvider` to discover tools from configured MCP connections:

1. Reads `McpConnectionIds` from the current `AICompletionContext`
2. Loads matching `McpConnection` entries from the catalog
3. Fetches cached capabilities via `IMcpServerMetadataCacheProvider`
4. Creates a `ToolRegistryEntry` for each tool, with a factory that produces an `McpToolProxyFunction`

Each tool entry is identified as `mcp:{connectionId}:{toolName}` and tagged with `ToolRegistryEntrySource.McpServer`.

### McpToolProxyFunction

An `AIFunction` proxy that transparently routes AI tool calls to the remote MCP server:

```csharp
internal sealed class McpToolProxyFunction : AIFunction
{
    public override string Name => _name;
    public override string Description => _description;
    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the McpConnection from the catalog
        // 2. Create an McpClient via McpService
        // 3. Call the remote tool: client.CallToolAsync(name, args)
        // 4. Return serialized result (or error JSON on failure)
    }
}
```

### McpAICompletionContextBuilderHandler

Wires MCP connection IDs from the AI profile into the completion context:

```csharp
internal sealed class McpAICompletionContextBuilderHandler
    : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        // Reads AIProfileMcpMetadata.ConnectionIds from the profile
        // Sets context.Context.McpConnectionIds
    }
}
```

This ensures the orchestrator knows which MCP connections to query for tools when processing a request.

## Capability Resolution

For profiles with many MCP connections, the framework can filter capabilities using semantic similarity before invoking tools.

### IMcpCapabilityResolver

```csharp
public interface IMcpCapabilityResolver
{
    Task<McpCapabilityResolutionResult> ResolveAsync(
        string prompt,
        string providerName,
        string connectionName,
        string[] mcpConnectionIds,
        CancellationToken cancellationToken = default);
}
```

This uses embedding vectors to find capabilities semantically relevant to the user's prompt, returning only the top matches. Configuration is available via `McpCapabilityResolverOptions`:

| Property | Default | Description |
|----------|---------|-------------|
| `SimilarityThreshold` | `0.3` | Minimum cosine similarity score for embedding-based matching |
| `KeywordMatchThreshold` | `0.2` | Minimum keyword match score for fallback matching |
| `TopK` | `5` | Maximum number of top matching capabilities to return |
| `IncludeAllThreshold` | `20` | Total capability count below which all capabilities are included without filtering |

### IMcpCapabilityEmbeddingCacheProvider

Caches embedding vectors for capability metadata, recomputing them when the underlying metadata cache is invalidated:

```csharp
public interface IMcpCapabilityEmbeddingCacheProvider
{
    Task<IReadOnlyList<McpCapabilityEmbeddingEntry>> GetOrCreateEmbeddingsAsync(
        IReadOnlyList<McpServerCapabilities> capabilities,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        CancellationToken cancellationToken = default);

    void Invalidate(string connectionId);
}
```

## Configuration

### McpClientAIOptions

Register additional transport types beyond the built-in SSE and StdIO:

```csharp
services.Configure<McpClientAIOptions>(options =>
{
    options.AddTransportType("grpc", entry =>
    {
        entry.DisplayName = new LocalizedString("gRPC", "gRPC");
        entry.Description = new LocalizedString("gRPC Desc", "Connects via gRPC transport.");
    });
});
```

### Custom Transport Provider

```csharp
public sealed class GrpcClientTransportProvider : IMcpClientTransportProvider
{
    public bool CanHandle(McpConnection connection)
        => connection.Source == "grpc";

    public Task<IClientTransport> GetAsync(McpConnection connection)
    {
        // Build and return a gRPC-based IClientTransport
    }
}

// Register in Startup
services.AddScoped<IMcpClientTransportProvider, GrpcClientTransportProvider>();
```

## Orchard Core Integration

The [MCP Client module](../../ai/mcp/client.md) adds a full admin UI for managing MCP server connections, including:

- Connection CRUD with SSE and StdIO configuration
- Authentication setup (API key, Basic, OAuth2 flows)
- Assigning MCP connections to AI profiles
- Viewing discovered capabilities from connected servers

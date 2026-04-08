---
sidebar_label: MCP Server
sidebar_position: 3
title: MCP Server
description: Expose your application's tools, prompts, and resources as an MCP server for external AI clients.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/mcp/server)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# MCP Server

> Expose your registered AI tools, prompts, and resources to external MCP clients over HTTP.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddCrestAppsMcpServer()
    .AddFtpMcpResourceServices()
    .AddSftpMcpResourceServices();
```

`AddCrestAppsMcpServer()` registers the shared prompt and resource services. FTP and SFTP resource handlers now live in the optional `CrestApps.Core.AI.Ftp` and `CrestApps.Core.AI.Sftp` packages, so hosts opt into those transport dependencies explicitly.

## Problem & Solution

External AI clients — IDE assistants, chat agents, orchestration frameworks — need a standardized way to discover and call your application's tools, read your prompts, and access your resources. The [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) provides that standard.

The MCP server framework:

- Exposes your registered AI tools so external clients can invoke them
- Serves prompts from the catalog and from code-registered `McpServerPrompt` instances
- Serves resources (files, data, templates) via pluggable resource type handlers
- Supports OpenID Connect, API key, or no-auth for development
- Uses the official [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) for HTTP transport

## Registered Services

`AddCrestAppsMcpServer()` registers these services:

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `IMcpServerPromptService` | `DefaultMcpServerPromptService` | Scoped | Lists and retrieves server prompts |
| `IMcpServerResourceService` | `DefaultMcpServerResourceService` | Scoped | Lists, templates, and reads server resources |

### Optional transport resource packages

| Extension | Package | Purpose |
|-----------|---------|---------|
| `AddFtpMcpResourceServices()` | `CrestApps.Core.AI.Ftp` | Registers the FTP/FTPS MCP resource type handler |
| `AddSftpMcpResourceServices()` | `CrestApps.Core.AI.Sftp` | Registers the SFTP MCP resource type handler |

## Server Endpoint Setup

Use the [ModelContextProtocol SDK](https://github.com/modelcontextprotocol/csharp-sdk) to map the SSE endpoint and wire in your tool, prompt, and resource handlers:

```csharp
app.MapMcpSse(mcpBuilder =>
{
    mcpBuilder.WithHttpTransport(options =>
    {
        options.ServerInfo = new() { Name = "My MCP Server", Version = "1.0" };
    });

    mcpBuilder.WithListToolsHandler(async (context, ct) =>
    {
        // Return your registered AI tools
    });

    mcpBuilder.WithCallToolHandler(async (context, ct) =>
    {
        // Invoke a tool by name, delegate to your tool registry
    });

    mcpBuilder.WithListPromptsHandler(async (context, ct) =>
    {
        var service = context.Services.GetRequiredService<IMcpServerPromptService>();
        return new ListPromptsResult { Prompts = await service.ListAsync() };
    });

    mcpBuilder.WithGetPromptHandler(async (context, ct) =>
    {
        var service = context.Services.GetRequiredService<IMcpServerPromptService>();
        return await service.GetAsync(context, ct);
    });

    mcpBuilder.WithListResourcesHandler(async (context, ct) =>
    {
        var service = context.Services.GetRequiredService<IMcpServerResourceService>();
        return new ListResourcesResult { Resources = await service.ListAsync() };
    });

    mcpBuilder.WithListResourceTemplatesHandler(async (context, ct) =>
    {
        var service = context.Services.GetRequiredService<IMcpServerResourceService>();
        return new ListResourceTemplatesResult
        {
            ResourceTemplates = await service.ListTemplatesAsync()
        };
    });

    mcpBuilder.WithReadResourceHandler(async (context, ct) =>
    {
        var service = context.Services.GetRequiredService<IMcpServerResourceService>();
        return await service.ReadAsync(context, ct);
    });
});
```

See the [MVC Example](../mvc-example.md) for a complete working server setup.

## Prompt Serving

### IMcpServerPromptService

Serves prompts from two sources:

1. **Catalog prompts** — `McpPrompt` entries stored in the named catalog (`INamedCatalog<McpPrompt>`)
2. **SDK prompts** — `McpServerPrompt` instances registered directly in DI

```csharp
public interface IMcpServerPromptService
{
    Task<IList<Prompt>> ListAsync();
    Task<GetPromptResult> GetAsync(
        RequestContext<GetPromptRequestParams> request,
        CancellationToken cancellationToken = default);
}
```

The `DefaultMcpServerPromptService` merges both sources, with catalog prompts taking precedence when names collide.

### McpPrompt Model

```csharp
public sealed class McpPrompt : CatalogItem, INameAwareModel
{
    public string Name { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Author { get; set; }
    public string OwnerId { get; set; }
    public Prompt Prompt { get; set; }  // MCP SDK Prompt with arguments
}
```

## Resource Serving

### IMcpServerResourceService

Serves resources and resource templates from the catalog and code-registered `McpServerResource` instances:

```csharp
public interface IMcpServerResourceService
{
    Task<IList<Resource>> ListAsync();
    Task<IList<ResourceTemplate>> ListTemplatesAsync();
    Task<ReadResourceResult> ReadAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken = default);
}
```

**How resource reading works:**

1. The service checks if any registered `McpServerResource` matches the URI
2. If not, it parses the URI to extract the resource `ItemId` (from the path segment after `://`)
3. Looks up the `McpResource` catalog entry and resolves the `IMcpResourceTypeHandler` by the entry's `Source` (type key)
4. For template URIs, extracts variables using `McpResourceUri.TryMatch()` and passes them to the handler

### McpResource Model

```csharp
public sealed class McpResource : SourceCatalogEntry, IDisplayTextAwareModel
{
    public string DisplayText { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Author { get; set; }
    public string OwnerId { get; set; }
    public Resource Resource { get; set; }  // MCP SDK Resource (URI, name, description, MIME type)
}
```

### URI Templates

Resources can use URI templates with `{variable}` placeholders. The `McpResourceUri` utility handles matching and variable extraction:

```
ftp://my-resource/{path}
recipe-step-schema://my-resource/{stepName}
```

When a client requests `ftp://my-resource/reports/2024/sales.csv`, the framework matches it against the template and extracts `{ "path": "reports/2024/sales.csv" }`. The last variable in a template can match multi-segment paths.

## Resource Type Handlers

Optional transport resource handlers:

| Type | Handler | Description |
|------|---------|-------------|
| `ftp` | `FtpResourceTypeHandler` | Registered by `AddFtpMcpResourceServices()` from `CrestApps.Core.AI.Ftp` |
| `sftp` | `SftpResourceTypeHandler` | Registered by `AddSftpMcpResourceServices()` from `CrestApps.Core.AI.Sftp` |

### Registering Custom Resource Types

```csharp
builder.Services.AddMcpResourceType<BlobStorageResourceHandler>("blob", entry =>
{
    entry.DisplayName = new LocalizedString("Blob Storage", "Azure Blob Storage");
    entry.Description = new LocalizedString("Blob Desc", "Reads content from Azure Blob Storage.");
    entry.SupportedVariables =
    [
        new McpResourceVariable("path")
        {
            Description = new LocalizedString("Blob Path", "The blob path within the container.")
        },
    ];
});
```

For a full guide on implementing custom resource type handlers, see [Resource Types](./resource-types.md).

## Authentication

### McpServerOptions

Configure server authentication via `McpServerOptions`:

```csharp
services.Configure<McpServerOptions>(options =>
{
    options.AuthenticationType = McpServerAuthenticationType.OpenId;
    options.RequireAccessPermission = true;
});
```

### McpServerAuthenticationType

| Type | Description |
|------|-------------|
| `OpenId` | **(Default)** Uses OpenID Connect authentication via the `"Api"` scheme. Most secure option for production. |
| `ApiKey` | Uses a predefined API key provided via the `Authorization` header. Configure the key in `McpServerOptions.ApiKey`. |
| `None` | Disables authentication. **Only for local development.** |

### Permission Control

When using `OpenId` authentication, the `RequireAccessPermission` property (default: `true`) controls whether the `AccessMcpServer` permission is checked. When set to `false`, any authenticated user can access the MCP server.

**Example** — API key authentication:

```csharp
services.Configure<McpServerOptions>(options =>
{
    options.AuthenticationType = McpServerAuthenticationType.ApiKey;
    options.ApiKey = builder.Configuration["Mcp:ServerApiKey"];
});
```

## Tool Exposure

When your application acts as an MCP server, registered AI tools are exposed to external clients. The server endpoint's `WithListToolsHandler` and `WithCallToolHandler` callbacks delegate to your tool registry:

1. **List tools** — Returns metadata (name, description, JSON schema) for all registered tools
2. **Call tool** — Resolves the tool by name from the registry and invokes it with the provided arguments

Tools registered via `AddAITool<T>()` (see [Custom Tools](../tools.md)) are automatically available to MCP clients.

## Server Metadata

### IMcpServerMetadataProvider

Implementations provide server metadata for the MCP handshake:

```csharp
public interface IMcpServerMetadataCacheProvider
{
    Task<McpServerCapabilities> GetCapabilitiesAsync(McpConnection connection);
    Task InvalidateAsync(string connectionId);
}
```

### McpServerMetadata

```csharp
public sealed class McpServerMetadata
{
    public bool UseLocalServer { get; set; }
}
```

## File Provider Integration

### IMcpFileProviderResolver

Allows resource handlers to resolve `IFileProvider` instances by name, enabling resources served from media libraries, web roots, or custom file stores:

```csharp
public interface IMcpFileProviderResolver
{
    IFileProvider Resolve(string providerName);
}
```

## Resource Export

### IMcpResourceHandler

Hook into resource export to strip sensitive data:

```csharp
public interface IMcpResourceHandler
{
    void Exporting(ExportingMcpResourceContext context);
}

public sealed class ExportingMcpResourceContext
{
    public readonly McpResource Resource;
    public readonly JsonObject ExportData;  // Modify to remove credentials
}
```

## Metadata Prompt Generation

### IMcpMetadataPromptGenerator

Generates structured system prompts describing MCP server capabilities so the AI model can reason about when to invoke them:

```csharp
public interface IMcpMetadataPromptGenerator
{
    string Generate(IReadOnlyList<McpServerCapabilities> capabilities);
}
```

The generated prompt is injected into the model context, giving the AI awareness of available MCP capabilities without manually listing each tool in the system prompt.

## Configuration

### McpOptions

`McpOptions` holds the registry of resource types. Resource types are added via `AddMcpResourceType<T>()`:

```csharp
services.Configure<McpOptions>(options =>
{
    // Inspect registered resource types
    foreach (var (type, entry) in options.ResourceTypes)
    {
        Console.WriteLine($"{type}: {entry.DisplayName}");
    }
});
```

Each `McpResourceTypeEntry` provides:

| Property | Description |
|----------|-------------|
| `Type` | Unique type identifier string |
| `DisplayName` | Localized display name |
| `Description` | Localized description |
| `SupportedVariables` | Array of `McpResourceVariable` describing URI template variables |

## Orchard Core Integration

The [MCP Server module](../../ai/mcp/server.md) adds a full admin UI for:

- Configuring server authentication (OpenID, API key, or none)
- Managing prompts and resources through the admin dashboard
- Registering and configuring resource types
- Viewing server capabilities and health status

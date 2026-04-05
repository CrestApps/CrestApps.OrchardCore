---
sidebar_label: Resource Types
sidebar_position: 4
title: MCP Resource Types
description: Create custom MCP resource type handlers for FTP, SFTP, databases, or any protocol.
---

# MCP Resource Types

> Register custom resource type handlers to expose files, data, or content from any protocol as MCP resources.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsMcpServer()
    .AddFtpMcpResourceServices()
    .AddSftpMcpResourceServices()
    .AddMcpResourceType<MyDatabaseResourceHandler>("database");
```

## Problem & Solution

MCP resources represent files, URLs, or data that clients can read. FTP and SFTP handlers are available as optional packages (`CrestApps.AI.Ftp` and `CrestApps.AI.Sftp`), and applications often need additional resource types for databases, APIs, blob storage, or custom protocols. Resource type handlers provide a pluggable extension point.

## Built-in Resource Types

| Type | Handler | Protocol |
|------|---------|----------|
| `ftp` | `FtpResourceTypeHandler` | FTP/FTPS |
| `sftp` | `SftpResourceTypeHandler` | SFTP |

Register them explicitly with `AddFtpMcpResourceServices()` and `AddSftpMcpResourceServices()`.

## Registration

```csharp
builder.Services.AddMcpResourceType<MyHandler>("my-type", entry =>
{
    entry.DisplayName = "My Resource Type";
    entry.Description = "Reads resources from my custom source";
});
```

### `AddMcpResourceType<THandler>(type, configure?)`

| Parameter | Description |
|-----------|-------------|
| `type` | Unique type identifier string |
| `configure` | Optional action to set display name and description |

## Implementing a Resource Type Handler

```csharp
public sealed class BlobStorageResourceHandler : McpResourceTypeHandlerBase
{
    private readonly BlobServiceClient _blobClient;

    public BlobStorageResourceHandler(BlobServiceClient blobClient)
    {
        _blobClient = blobClient;
    }

    protected override async Task<McpResourceReadResult> GetResultAsync(
        McpResourceReadContext context,
        CancellationToken cancellationToken)
    {
        var containerClient = _blobClient.GetBlobContainerClient(context.ContainerName);
        var blobClient = containerClient.GetBlobClient(context.ResourcePath);

        var download = await blobClient.DownloadContentAsync(cancellationToken);
        var content = download.Value.Content.ToString();

        return new McpResourceReadResult
        {
            Contents = [new TextResourceContents
            {
                Text = content,
                Uri = context.Uri,
                MimeType = "text/plain",
            }],
        };
    }
}
```

## Key Interfaces

### `IMcpResourceTypeHandler`

```csharp
public interface IMcpResourceTypeHandler
{
    Task<McpResourceReadResult> ReadAsync(
        McpResourceReadContext context,
        CancellationToken cancellationToken = default);
}
```

### `McpResourceTypeHandlerBase`

A convenience base class — implement `GetResultAsync()` instead:

```csharp
protected abstract Task<McpResourceReadResult> GetResultAsync(
    McpResourceReadContext context,
    CancellationToken cancellationToken);
```

## Orchard Core Integration

The [MCP Resource modules](../../ai/mcp/server.md) add admin UI for managing resources and their type configurations. Custom resource types can be registered in Orchard Core module `Startup` classes.

# Model Context Protocol (MCP) Client

Offers core services and a user interface for connecting to Model Context Protocol (MCP) servers, enabling AI models to leverage additional capabilities and resources.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Model Context Protocol (MCP) Client | `CrestApps.OrchardCore.AI.Mcp` | Offers core services and a user interface for connecting to Model Context Protocol (MCP) servers, enabling AI models to leverage additional capabilities and resources. |
| Model Context Protocol (MCP) Local Client | `CrestApps.OrchardCore.AI.Mcp.Stdio` | Extends the Model Context Protocol Client with standard input/output (STDIO) transport for connecting to local MCP servers. Default tenant only; creating or changing a local connection needs `ManageMcpStdioConnections`, and the host can limit the programs under `CrestApps:AI:Mcp:Stdio:AllowedCommands`. |
| Model Context Protocol (MCP) Server | `CrestApps.OrchardCore.AI.Mcp.Server` | Exposes Orchard Core AI tools through the MCP protocol, enabling external MCP-compatible clients to connect and invoke AI capabilities. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.AI`](../CrestApps.OrchardCore.AI/README.md)
- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)

## Content resources

- The content item and content type resources return only published items the MCP caller may view (`ViewContent` per item); a draft or older version answers "not found".
- The content type resource lists 50 items a page; pass the page in a `{page}` variable of the URI template.

## Documentation

See [MCP Client Integration](../../CrestApps.Docs/docs/ai/mcp/client.md) in the technical manual.

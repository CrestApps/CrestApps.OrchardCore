# Orchard Core MCP Practical Examples

## Recipe: Full MCP Client Setup with SSE Connection

Enable the MCP Client feature and add a remote SSE connection in a single recipe:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Mcp",
        "CrestApps.OrchardCore.OpenAI"
      ]
    },
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "OpenAI",
          "Name": "default",
          "IsDefault": true,
          "DisplayText": "OpenAI Default",
          "Deployments": [
            { "Name": "gpt-4o", "Type": "Chat", "IsDefault": true }
          ],
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.openai.com/v1",
              "ApiKey": "{{YourApiKey}}"
            }
          }
        }
      ]
    },
    {
      "name": "McpConnection",
      "connections": [
        {
          "DisplayText": "Remote Tools Server",
          "Properties": {
            "SseMcpConnectionMetadata": {
              "Endpoint": "https://mcp-tools.example.com/",
              "AdditionalHeaders": {}
            }
          }
        }
      ]
    }
  ]
}
```

## Recipe: MCP Client with Local Docker-based Server (Stdio)

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Mcp",
        "CrestApps.OrchardCore.AI.Mcp.Local",
        "CrestApps.OrchardCore.OpenAI"
      ]
    },
    {
      "name": "McpConnection",
      "connections": [
        {
          "DisplayText": "Global Time Capabilities",
          "Properties": {
            "StdioMcpConnectionMetadata": {
              "Command": "docker",
              "Arguments": [
                "run",
                "-i",
                "--rm",
                "mcp/time"
              ]
            }
          }
        }
      ]
    }
  ]
}
```

## Recipe: MCP Client with Multiple Connections

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Mcp",
        "CrestApps.OrchardCore.AI.Mcp.Local",
        "CrestApps.OrchardCore.OpenAI"
      ]
    },
    {
      "name": "McpConnection",
      "connections": [
        {
          "DisplayText": "Remote Database Tools",
          "Properties": {
            "SseMcpConnectionMetadata": {
              "Endpoint": "https://db-tools.example.com/mcp",
              "AdditionalHeaders": {
                "X-Api-Key": "{{DbToolsApiKey}}"
              }
            }
          }
        },
        {
          "DisplayText": "Local File System Tools",
          "Properties": {
            "StdioMcpConnectionMetadata": {
              "Command": "docker",
              "Arguments": [
                "run",
                "-i",
                "--rm",
                "mcp/filesystem",
                "/data"
              ]
            }
          }
        }
      ]
    }
  ]
}
```

## Recipe: Enable MCP Server with AI Agent

Enable MCP Server alongside the AI Agent module to expose Orchard Core management tools:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Agent",
        "CrestApps.OrchardCore.AI.Mcp.Server",
        "CrestApps.OrchardCore.OpenAI"
      ]
    }
  ]
}
```

## MCP Server Configuration: ApiKey Authentication

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "McpServer": {
        "AuthenticationType": "ApiKey",
        "ApiKey": "your-long-secure-random-api-key"
      }
    }
  }
}
```

Store the API key securely:

```bash
dotnet user-secrets set "OrchardCore:CrestApps_AI:McpServer:ApiKey" "your-long-secure-random-api-key"
```

## MCP Server Configuration: OpenId Authentication

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "McpServer": {
        "AuthenticationType": "OpenId",
        "RequireAccessPermission": true
      }
    }
  }
}
```

Remember to:
1. Enable the OpenID Server feature for token-based authentication.
2. Configure OAuth client applications.
3. Grant the `AccessMcpServer` permission to appropriate roles.

## Connecting VS Code to Orchard Core MCP Server

Configure VS Code's MCP client to connect to your Orchard Core instance:

```json
{
  "mcpServers": {
    "orchard-core-site": {
      "transport": {
        "type": "sse",
        "url": "https://your-orchard-site.com/mcp/sse",
        "headers": {
          "Authorization": "ApiKey your-secure-api-key"
        }
      }
    }
  }
}
```

## Registering a Custom MCP Resource Type

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    private readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMcpResourceType<ApiEndpointResourceHandler>("api", entry =>
        {
            entry.DisplayName = S["API Endpoint"];
            entry.Description = S["Fetch data from REST API endpoints."];
            entry.UriPatterns = ["api://{itemId}/{path}"];
        });
    }
}
```

Implement the resource type handler:

```csharp
public sealed class ApiEndpointResourceHandler : IMcpResourceTypeHandler
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiEndpointResourceHandler(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Type => "api";

    public async Task<ReadResourceResult> ReadAsync(
        McpResource resource,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(resource.Resource.Uri);
        var client = _httpClientFactory.CreateClient();

        var response = await client.GetStringAsync(
            uri.AbsolutePath,
            cancellationToken);

        return new ReadResourceResult
        {
            Contents = [new TextResourceContents
            {
                Text = response,
                MimeType = resource.Resource.MimeType ?? "application/json",
                Uri = resource.Resource.Uri,
            }],
        };
    }
}
```

## Recipe: Create MCP Resources

```json
{
  "steps": [
    {
      "name": "McpResource",
      "Resources": [
        {
          "Source": "file",
          "DisplayText": "Site Configuration",
          "Resource": {
            "Uri": "file://main-config/app/appsettings.json",
            "Name": "site-config",
            "Description": "Main application configuration",
            "MimeType": "application/json"
          }
        },
        {
          "Source": "content",
          "DisplayText": "Blog Articles",
          "Resource": {
            "Uri": "content://blog-articles/BlogPost/list",
            "Name": "blog-articles",
            "Description": "List of all blog articles",
            "MimeType": "application/json"
          }
        }
      ]
    }
  ]
}
```

## Extending Content Resources with a Custom Strategy

```csharp
public sealed class SearchContentResourceStrategy : IContentResourceStrategyProvider
{
    private readonly IContentManager _contentManager;

    public SearchContentResourceStrategy(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public string[] UriPatterns => ["content://{itemId}/{contentType}/search"];

    public bool CanHandle(Uri uri)
    {
        return uri.Segments.Length >= 4 &&
               uri.Segments[^1].TrimEnd('/') == "search";
    }

    public async Task<ReadResourceResult> ReadAsync(
        McpResource resource,
        Uri uri,
        CancellationToken cancellationToken)
    {
        var contentType = uri.Segments[2].TrimEnd('/');

        var items = await _contentManager
            .Query(contentType)
            .ListAsync();

        var json = JsonSerializer.Serialize(items);

        return new ReadResourceResult
        {
            Contents = [new TextResourceContents
            {
                Text = json,
                MimeType = "application/json",
                Uri = resource.Resource.Uri,
            }],
        };
    }
}
```

Register in `Startup.cs`:

```csharp
services.AddContentResourceStrategy<SearchContentResourceStrategy>();
```

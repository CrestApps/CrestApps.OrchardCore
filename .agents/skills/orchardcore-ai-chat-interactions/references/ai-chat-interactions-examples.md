# Orchard Core AI Chat Interactions Practical Examples

## Recipe: Full Chat Interactions Setup with Document RAG (Azure AI Search)

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Chat.Interactions.Documents.AzureAI",
        "CrestApps.OrchardCore.AI.Chat.Interactions.Pdf",
        "CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml",
        "OrchardCore.Search.AzureAI",
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
    }
  ]
}
```

After running this recipe:
1. Navigate to **Search → Indexing** and create a new index (e.g., "ChatDocuments") using Azure AI Search as the provider.
2. Navigate to **Settings → Chat Interaction** and select the new index as the default document index.
3. Navigate to **Artificial Intelligence → Chat Interactions** and start a new chat session.

## Recipe: Chat Interactions with Elasticsearch

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Elasticsearch",
        "CrestApps.OrchardCore.AI.Chat.Interactions.Pdf",
        "OrchardCore.Search.Elasticsearch",
        "CrestApps.OrchardCore.OpenAI"
      ]
    }
  ]
}
```

## Configuration: Full Provider Setup for Chat Interactions

Configure all deployment types for chat, embeddings, intent detection, and images:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "DefaultConnectionName": "default",
          "Connections": {
            "default": {
              "Deployments": [
                { "Name": "gpt-4o", "Type": "Chat", "IsDefault": true },
                { "Name": "text-embedding-3-small", "Type": "Embedding", "IsDefault": true },
                { "Name": "gpt-4o-mini", "Type": "Utility", "IsDefault": true },
                { "Name": "dall-e-3", "Type": "Image", "IsDefault": true }
              ]
            }
          }
        }
      }
    }
  }
}
```

## Implementing a Custom Document Processing Strategy

Create a strategy that translates document content:

```csharp
using CrestApps.OrchardCore.AI.Chat.Interactions;

public sealed class TranslateDocumentStrategy : IPromptProcessingStrategy
{
    public string Intent => "TranslateDocument";

    public async Task<PromptProcessingResult?> ProcessAsync(
        PromptProcessingContext context,
        CancellationToken cancellationToken)
    {
        if (context.Documents == null || !context.Documents.Any())
        {
            return null;
        }

        var documentContent = string.Join("\n\n", context.Documents.Select(d => d.Content));

        return new PromptProcessingResult
        {
            SystemPrompt = $"Translate the following document content as requested by the user:\n\n{documentContent}",
        };
    }
}
```

Register the intent and strategy in `Startup.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPromptProcessingIntent(
            "TranslateDocument",
            "The user wants to translate the document content to another language, such as 'translate this to Spanish' or 'convert to French'.");

        services.AddPromptProcessingStrategy<TranslateDocumentStrategy>();
    }
}
```

## Recipe: Enable Chat Interactions with Image Generation

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
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
          "DisplayText": "OpenAI",
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
    }
  ]
}
```

Then configure image generation in `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "Deployments": [
                { "Name": "gpt-4o", "Type": "Chat", "IsDefault": true },
                { "Name": "dall-e-3", "Type": "Image", "IsDefault": true }
              ]
            }
          }
        }
      }
    }
  }
}
```

## Storing API Keys Securely

```bash
dotnet user-secrets set "OrchardCore:CrestApps_AI:Providers:OpenAI:Connections:default:ApiKey" "sk-your-api-key"
```

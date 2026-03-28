# Orchard Core AI Chat Practical Examples

## Recipe: Full AI Chat Setup with OpenAI

Enable the AI Chat feature, add an OpenAI provider connection, and create a chat profile in a single recipe:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
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
      "name": "AIProfile",
      "profiles": [
        {
          "Source": "OpenAI",
          "Name": "general-assistant",
          "DisplayText": "General Assistant",
          "WelcomeMessage": "Hi! I'm your AI assistant. How can I help?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "ConnectionName": "",
          "ChatDeploymentId": "",
          "UtilityDeploymentId": "",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a helpful assistant. Provide clear and concise answers.",
              "Temperature": 0.5,
              "MaxTokens": 2048,
              "PastMessagesCount": 10
            }
          }
        }
      ]
    }
  ]
}
```

## Recipe: AI Chat with Azure OpenAI

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.OpenAI.Azure.Standard"
      ]
    },
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "AzureOpenAI",
          "Name": "azure-default",
          "IsDefault": true,
          "DisplayText": "Azure OpenAI",
          "Deployments": [
            { "Name": "gpt-4o", "Type": "Chat", "IsDefault": true }
          ],
          "Properties": {
            "AzureOpenAIConnectionMetadata": {
              "Endpoint": "https://your-resource.openai.azure.com/",
              "ApiKey": "{{YourAzureApiKey}}"
            }
          }
        }
      ]
    }
  ]
}
```

## Recipe: Enable AI Agent with Chat

Enable the AI Agent module alongside AI Chat so profiles can perform site tasks:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Agent",
        "CrestApps.OrchardCore.OpenAI"
      ]
    }
  ]
}
```

After enabling, navigate to your AI profile, open the **Capabilities** tab, and assign the desired capabilities (content management, feature management, user management, etc.).

## Defining a Chat Profile with Custom Tools in Code

```csharp
public sealed class SupportChatMigrations : DataMigration
{
    private readonly IAIProfileManager _profileManager;

    public SupportChatMigrations(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _profileManager.NewAsync("OpenAI");

        profile.Name = "support-chat";
        profile.DisplayText = "Support Chat";
        profile.Type = AIProfileType.Chat;
        profile.FunctionNames = ["lookup_order", "check_inventory"];

        profile.WithSettings(new AIProfileSettings
        {
            LockSystemMessage = true,
            IsRemovable = false,
            IsListable = true,
        });

        profile.WithSettings(new AIChatProfileSettings
        {
            IsOnAdminMenu = true,
        });

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "You are a support agent. Help customers with orders, inventory, and product questions. Use the available tools to look up information.",
            Temperature = 0.2f,
            MaxTokens = 4096,
            PastMessagesCount = 15,
        });

        await _profileManager.SaveAsync(profile);

        return 1;
    }
}
```

## Registering Multiple Custom AI Tools

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<LookupOrderFunction>(LookupOrderFunction.TheName, options =>
        {
            options.Title = "Order Lookup";
            options.Description = "Retrieves order details by order ID.";
            options.Category = "Commerce";
        });

        services.AddAITool<CheckInventoryFunction>(CheckInventoryFunction.TheName, options =>
        {
            options.Title = "Inventory Checker";
            options.Description = "Checks product inventory levels.";
            options.Category = "Commerce";
        });
    }
}
```

## Configuration: AI Settings in appsettings.json

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "DefaultParameters": {
        "Temperature": 0.5,
        "MaxOutputTokens": 2048,
        "TopP": 1,
        "FrequencyPenalty": 0,
        "PresencePenalty": 0,
        "PastMessagesCount": 10
      },
      "Providers": {
        "OpenAI": {
          "DefaultConnectionName": "default",
          "Connections": {
            "default": {
              "Deployments": [
                { "Name": "gpt-4o", "Type": "Chat", "IsDefault": true }
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

Use user secrets during development:

```bash
dotnet user-secrets set "OrchardCore:CrestApps_AI:Providers:OpenAI:Connections:default:ApiKey" "sk-your-api-key"
```

For Azure OpenAI:

```bash
dotnet user-secrets set "OrchardCore:CrestApps_AI:Providers:AzureOpenAI:Connections:azure-default:ApiKey" "your-azure-key"
```

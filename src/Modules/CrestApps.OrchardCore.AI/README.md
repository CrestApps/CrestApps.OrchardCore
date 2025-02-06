## AI Services Feature

The **AI Services** feature provides the necessary services to interact with AI models. Once enabled, a new **AI Services** menu item appears in the admin menu, offering options to manage AI model deployments.

### Configuration

Before using any AI features, ensure that the appropriate settings are configured. You can do this using various setting providers. Below is an example of how to configure the services within the `appsettings.json` file:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "<!-- Provider name goes here -->": {
          "DefaultConnectionName": "<!-- The default connection name -->",
          "DefaultDeploymentName": "<!-- The default deployment name -->",
          "Connections": {
            "<!-- Connection name goes here -->": {
              "DefaultDeploymentName": "<!-- The default deployment name for this connection -->"
              // Provider-specific settings go here
            }
          }
        }
      }
    }
  }
}
```

### AI Deployments Feature

The **AI Deployments** feature builds on the **AI Services** feature by AI Model deployments capabilities.

### AI Chat Feature

The **AI Chat** feature builds on the **AI Services** feature by adding AI chat capabilities. After enabling this feature, a new **Profiles** menu item will appear under the **Artificial Intelligence** section in the admin menu, allowing you to manage chat profiles.

A **Profile** defines how the AI chatbot interacts with users. This includes configuring the chatbot's welcome message, system message, and response behavior.

**Note**: This feature does not provide chat service implementations, such as OpenAI, DeepSeek, or others. It only manages chat profiles. To enable chat capabilities, you must integrate an AI chat provider, such as:

- **Azure OpenAI Chat** (`CrestApps.OrchardCore.AI.Azure.Standard`): Provides AI services using Azure OpenAI models.
- **Azure OpenAI Chat with Your Data** (`CrestApps.OrchardCore.AI.Azure.AISearch`): AI-powered chat using Azure OpenAI models combined with Azure AI Search data.
- **DeepSeek Cloud AI Chat** (`CrestApps.OrchardCore.DeepSeek.Chat.Cloud`): AI-powered chat using Azure DeepSeek cloud service.

For detailed information on Azure OpenAI, refer to the [Azure OpenAI documentation](../CrestApps.OrchardCore.AI.Azure/README.md).

For detailed information on DeepSeek, refer to the [DeepSeek documentation](../CrestApps.OrchardCore.DeepSeek/README.md).

---

### Defining Chat Profiles Using Code

To define chat profiles programmatically, you can create a migration class. Here's an example that demonstrates how to create a new chat profile using code:

```csharp
public sealed class SystemDefinedAIProfileMigrations : DataMigration
{
    private readonly IAIChatProfileManager _chatProfileManager;

    public SystemDefinedAIProfileMigrations(IAIChatProfileManager chatProfileManager)
    {
        _chatProfileManager = chatProfileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _chatProfileManager.NewAsync("Azure");

        profile.Name = "UniqueTechnicalName";
        profile.DisplayText = "A Display name for the profile";
        profile.Type = AIChatProfileType.Chat;

        profile.WithSettings(new AIChatProfileSettings
        {
            IsRemovable = false, 
            IsListable = false, 
            IsOnAdminMenu = true,
        });

        profile.WithSettings(new OpenAIChatProfileSettings
        {
            LockSystemMessage = true, 
        });

        profile.Put(new OpenAIChatProfileMetadata
        {
            SystemMessage = "some system message",
            Temperature = 0.3f,
            MaxTokens = 4096,
        });

        await _chatProfileManager.SaveAsync(profile);

        return 1;
    }
}
```

> **Note**: If a profile with the same name already exists, creating a new profile through a migration class will update the existing one. Always use a unique name for new profiles to avoid conflicts.

---

### Extending AI Chat with Custom Functions

The module allows for extending the AI chat's capabilities by adding custom functions. To implement a custom function, derive a class from `AIFunction` and register it as a service. Here's an example of a custom function to fetch weather information based on user location:

```csharp
public sealed class GetWeatherFunction : AIFunction
{
    private const string LocationProperty = "Location";

    public override AIFunctionMetadata Metadata { get; }

    public GetWeatherFunction()
    {
        Metadata = new AIFunctionMetadata("get_weather")
        {
            Description = "Retrieves weather information for a specified location.",
            Parameters =
            [
                new AIFunctionParameterMetadata(LocationProperty)
                {
                    Description = "The geographic location for which the weather information is requested.",
                    IsRequired = true,
                    ParameterType = typeof(string),
                },
            ],
            ReturnParameter = new AIFunctionReturnParameterMetadata
            {
                Description = "The weather",
                ParameterType = typeof(string),
            },
        };
    }

    protected override Task<object> InvokeCoreAsync(IEnumerable<KeyValuePair<string, object>> arguments, CancellationToken cancellationToken)
    {
        var location = arguments.First(x => x.Key == LocationProperty).Value as string;
        return Task.FromResult<object>(Random.Shared.NextDouble() > 0.5 ? $"It's sunny in {location}" : $"It's raining in {location}");
    }
}
```

#### Registering the Custom Function

To register the function, add it as a service in the `Startup` class:

```csharp
services.AddAITool<GetWeatherFunction>();
```

You can then access the function via `IAIToolsService` in your module.

---

### Configuring Chat Profiles with Custom Functions

Once the custom function is registered, it can be added to chat profiles. When creating or editing a profile, the custom function will appear in the list of available functions, and you can enable or disable it as needed.

---

### Implementing Custom AI Sources

If you need to integrate custom AI sources, you can implement the `IAIChatProfileSource` interface. For example:

```csharp
public sealed class AzureProfileSource : IAIChatProfileSource
{
    public const string Key = "Azure";

    public AzureProfileSource(IStringLocalizer<AzureProfileSource> localizer)
    {
        DisplayName = localizer["Azure OpenAI"];
        Description = localizer["Provides AI services using Azure OpenAI models."];
    }

    public string TechnicalName => Key;

    public string ProviderName => "Azure";

    public string TechnologyName => "OpenAI";

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
```

Register the custom source in the `Startup` class:

```csharp
public sealed class StandardStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIChatProfileSource<AzureProfileSource>(AzureProfileSource.Key);
    }
}
```

---

### Adding AI Chat Profiles via Recipes

If you're using the Recipes module, you can create or update AI chat profiles using the following recipe:

```json
{
  "steps": [
    {
      "name": "AIChatProfile",
      "profiles": [
        {
          "Source": "CustomSource",
          "Name": "ExampleProfile",
          "DisplayText": "Example Profile","
          "WelcomeMessage": "What do you want to know?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "ConnectionName":"<!-- The connection name to use for the deployment; if left blank, the default connection will be used. -->",
          "DeploymentId":"<!-- A deployment ID for the deployment; if left blank, the default deployment will be used. -->",
          "SystemMessage": "You are an AI assistant that helps people find information.",
          "Properties": {
            "OpenAIChatProfileMetadata": {
              "Temperature": null,
              "TopP": null,
              "FrequencyPenalty": null,
              "PresencePenalty": null,
              "MaxTokens": null,
              "PastMessagesCount": null
            }
          }
        }
      ]
    }
  ]
}
```

you can also create or update AI deployment using the following recipe:

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "deployments": [
        {
          "Name": "<!-- The name of the deployment as specified at the vendor side. -->",
          "ProviderName": "<!-- The provider name (e.g., OpenAI, DeepSeek). -->",
          "ConnectionName": "<!-- The connection name you have used to configure the provider. -->"
        }
      ]
    }
  ]
}
```

---

### AI Chat with Workflows

When combined with the **Workflows** feature, the **AI Chat** feature introduces a new activity for interacting with AI chat services:

#### Chat Utility Completion Task

This activity allows you to send a message to the AI chat service and store the generated response in a workflow property. To use it, search for the **Chat Utility Completion** task in your workflow and specify a unique **Result Property Name**. This property will store the response generated by the AI chat service.

For example, if the **Result Property Name** is `AI-CrestApps-Step1`, you can access the response later using:

```liquid
{{ Workflow.Output["AI-CrestApps-Step1"].Content }}
```

If you want the response in HTML format, enable the `Include HTML Content` option, then access it using:

```liquid
{{ Workflow.Output["AI-CrestApps-Step1"].HtmlContent }}
```

To avoid conflicts with other workflow tasks, it's recommended to prefix the **Result Property Name** with `AI-`.

---

### Deployments with AI Chat

The **AI Chat** feature integrates with the **Deployments** module, allowing you to deploy chat profiles to different environments through Orchard Core's Deployment UI.

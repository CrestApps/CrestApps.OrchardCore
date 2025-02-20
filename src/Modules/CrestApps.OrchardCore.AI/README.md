## AI Services Feature

The **AI Services** feature enables interaction with AI models by providing essential services. Once activated, a new **Artificial Intelligence** menu item appears in the admin menu, offering options to manage AI profiles.

An **AI Profile** defines how the AI chatbot engages with users, including configuring the chatbot's welcome message, system message, and response behavior.

Note: This feature does not provide any completion client implementations (e.g., OpenAI, DeepSeek, etc.). It only provide user interface to manager manages profiles along with the key services.

### Configuration

Before utilizing any AI features, ensure the necessary settings are configured. This can be done using various setting providers. Below is an example of how to configure the services in the `appsettings.json` file:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "DefaultParameters": {
        "Temperature": 0,
        "MaxOutputTokens": 800,
        "TopP": 1,
        "FrequencyPenalty": 0,
        "PresencePenalty": 0,
        "PastMessagesCount": 10,
        "MaximumIterationsPerRequest": 1,
        "EnableOpenTelemetry": false,
        "EnableDistributedCaching": true
      },
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

The **AI Deployments** feature extends the **AI Services** feature by enabling AI model deployment capabilities.

### AI Chat Feature

The **AI Chat** feature builds upon the **AI Services** feature by adding AI chat capabilities. Once enabled, any chat-type AI profile with the "Show On Admin Menu" option will appear under the **Artificial Intelligence** section in the admin menu, allowing you to interact with your chat profiles. If the Widgets feature is enabled, a widget will also be available to add to your content.

**Note**: This feature does not provide chat service implementations (e.g., OpenAI, DeepSeek, etc.). It only manages chat profiles. To enable chat capabilities, you must integrate an AI chat provider, such as:

- **OpenAI AI Chat** (`CrestApps.OrchardCore.OpenAI`): AI-powered chat using Azure OpenAI service.
- **Azure OpenAI Chat** (`CrestApps.OrchardCore.OpenAI.Azure.Standard`): AI services using Azure OpenAI models.
- **Azure OpenAI Chat with Your Data** (`CrestApps.OrchardCore.OpenAI.Azure.AISearch`): AI chat using Azure OpenAI models combined with Azure AI Search data.
- **Azure AI Inference Chat** (`CrestApps.OrchardCore.AzureAIInference`): AI services using Azure AI Inference (GitHub models) models.
- **DeepSeek AI Chat** (`CrestApps.OrchardCore.DeepSeek`): AI-powered chat using Azure DeepSeek cloud service.
- **Ollama AI Chat** (`CrestApps.OrchardCore.Ollama`): AI-powered chat using Azure Ollama service.


![Screenshot of the admin chat](../../docs/images/admin-ui-sample.gif)

### AI Chat WebAPI

The **AI Chat WebAPI** feature extends the **AI Services** feature by enabling a REST WebAPI endpoints to allow you to interact with the models.

---

### Defining Chat Profiles Using Code

To define chat profiles programmatically, create a migration class. Here’s an example demonstrating how to create a new chat profile:

```csharp
public sealed class SystemDefinedAIProfileMigrations : DataMigration
{
    private readonly IAIProfileManager _profileManager;

    public SystemDefinedAIProfileMigrations(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _profileManager.NewAsync("Azure");

        profile.Name = "UniqueTechnicalName";
        profile.DisplayText = "A Display name for the profile";
        profile.Type = AIProfileType.Chat;

        profile.WithSettings(new AIProfileSettings
        {
            LockSystemMessage = true, 
            IsRemovable = false, 
            IsListable = false, 
        });

        profile.WithSettings(new AIChatProfileSettings
        {
            IsOnAdminMenu = true,
        });

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "some system message",
            Temperature = 0.3f,
            MaxTokens = 4096,
        });

        await _profileManager.SaveAsync(profile);

        return 1;
    }
}
```

> **Note**: If a profile with the same name already exists, creating a new profile through a migration class will update the existing one. Always use a unique name for new profiles to avoid conflicts.

---

### Extending AI Chat with Custom Functions

You can extend AI chat functionality by adding custom functions. To create a custom function, inherit from `AIFunction` and register it as a service. Here’s an example of a custom function that retrieves weather information based on the user’s location:

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
            Parameters = [
                new AIFunctionParameterMetadata(LocationProperty)
                {
                    Description = "The geographic location for which the weather information is requested.",
                    IsRequired = true,
                    ParameterType = typeof(string),
                }
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

To register the custom function, add it as a service in the `Startup` class:

```csharp
services.AddAITool<GetWeatherFunction>();
```

You can then access the function via `IAIToolsService` in your module.

---

### Configuring AI Profiles with Custom Functions

Once the custom function is registered, you can add it to any AI profile. The custom function will be available in the list of functions when creating or editing a profile, and you can enable or disable it as needed.

---

### Implementing Custom AI Sources

To integrate custom AI sources, implement the `IAIProfileSource` interface. Here’s an example:

```csharp
public sealed class CustomProfileSource : IAIProfileSource
{
    public const string ProviderTechnicalName = "ThirdPartyProviderName";
    public const string ImplementationName = "Custom";

    public CustomProfileSource(IStringLocalizer<CustomProfileSource> localizer)
    {
        DisplayName = localizer["Azure OpenAI"];
        Description = localizer["Provides AI services using Azure OpenAI models."];
    }

    public string TechnicalName => ImplementationName;

    public string ProviderName => ProviderTechnicalName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
```

You'll also need to register a custom completion client for the source. Below is an example implementation:

```csharp
public sealed class CustomCompletionClient : NamedAICompletionClient
{
    public CustomCompletionClient(
       ILoggerFactory loggerFactory,
       IDistributedCache distributedCache,
       IOptions<AIProviderOptions> providerOptions,
       IAIToolsService toolsService,
       IOptions<DefaultAIOptions> defaultOptions,
       IAIDeploymentStore deploymentStore
    ) : base(CustomProfileSource.ImplementationName, distributedCache, loggerFactory, providerOptions.Value, defaultOptions.Value, toolsService, deploymentStore)
    {
    }

    protected override string ProviderName => CustomProfileSource.ProviderTechnicalName;

    protected override IChatClient GetChatClient(AIProviderConnection connection, AICompletionContext context, string deploymentName)
    {
        return new OpenAIClient(connection.GetApiKey())
            .AsChatClient(connection.GetDefaultDeploymentName());
    }
}
```

> **Note:** The `CustomCompletionClient` above inherits from `NamedAICompletionClient`. If the provider supports multiple deployments, you can instead inherit from `DeploymentAwareAICompletionClient`.

Finally, register the custom source and completion client in the `Startup` class:

```csharp
public sealed class StandardStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfile<CustomProfileSource, CustomCompletionClient>(CustomProfileSource.ImplementationName);
    }
}
```

> **Important:** Ensure that both the profile source and the completion client share the same registration key.

---

### Adding AI Profiles via Recipes

You can create or update AI chat profiles via the Recipes module using the following recipe:

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Source": "CustomSource",
          "Name": "ExampleProfile",
          "DisplayText": "Example Profile",
          "WelcomeMessage": "What do you want to know?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "ConnectionName":"<!-- Connection name for the deployment; leave blank for default. -->",
          "DeploymentId":"<!-- Deployment ID for the deployment; leave blank for default. -->",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are an AI assistant that helps people find information.",
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

You can also create or update AI deployments using the following recipe:

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "deployments": [
        {
          "Name": "<!-- Deployment name as specified by the vendor -->",
          "ProviderName": "<!-- Provider name (e.g., OpenAI, DeepSeek) -->",
          "ConnectionName": "<!-- Connection name used to configure the provider -->"
        }
      ]
    }
  ]
}
```

---

### AI Chat with Workflows

When used with the **Workflows** feature, the **AI Services** feature introduces a new activity to interact with AI chat services:

#### Chat Utility Completion Task

This activity allows you to send a message to the AI chat service and store the response in a workflow property. To use it, search for the **Chat Utility Completion** task in your workflow and specify a unique **Result Property Name**. The generated response will be stored in this property.

For example, if the **Result Property Name** is `AI-CrestApps-Step1`, access the response later with:

```liquid
{{ Workflow.Output["AI-CrestApps-Step1"].Content }}
```

If you want the response in HTML format, enable the `Include HTML Content` option, and access it with:

```liquid
{{ Workflow.Output["AI-CrestApps-Step1"].HtmlContent }}
```

To avoid conflicts with other workflow tasks, it's recommended to prefix the **Result Property Name** with `AI-`.

---

### Deployments with AI Chat

The **AI Services** feature integrates with the **Deployments** module, allowing profiles to be deployed to various environments through Orchard Core's Deployment UI.

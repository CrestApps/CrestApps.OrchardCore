## Table of Contents

- [AI Services Feature](#ai-services-feature)
  - [Configuration](#configuration)
  - [Provider Configuration](#provider-configuration)
  - [Microsoft.AI.Extensions](#microsoftaiextensions)
  - [AI Deployments Feature](#ai-deployments-feature)
  - [AI Chat Services Feature](#ai-chat-services-feature)
  - [AI Chat WebAPI](#ai-chat-webapi)
  - [AI Connection Management](#ai-connection-management)
  - [AI Data Source Management](#ai-data-source-management)
  - [Defining Chat Profiles Using Code](#defining-chat-profiles-using-code)
- [AI Tool Management Feature](#ai-tool-management-feature)
  - [Extending AI Chat with Custom Functions](#extending-ai-chat-with-custom-functions)
  - [Using AI Tool Sources](#using-ai-tool-sources)
  - [Configuring AI Profiles with Custom Functions](#configuring-ai-profiles-with-custom-functions)
- [Adding Custom AI Profile Sources](#adding-custom-ai-profile-sources)
  - [Implementing a Custom Completion Client](#implementing-a-custom-completion-client)
  - [Supporting Multiple Deployments](#supporting-multiple-deployments)
  - [Adding AI Profiles via Recipes](#adding-ai-profiles-via-recipes)
  - [Deleting AI Deployments via Recipes](#deleting-ai-deployments-via-recipes)
- [AI Chat with Workflows](#ai-chat-with-workflows)
  - [AI Completion using Profile Task](#ai-completion-using-profile-task)
  - [AI Completion using Direct Config Task](#ai-completion-using-direct-config-task)
- [Deployments with AI Chat](#deployments-with-ai-chat)
- [Compatibility](#compatibility)

## AI Services Feature

The **AI Services** feature provides the foundational infrastructure for interacting with AI models through configurable profiles and service integrations.

Once enabled, a new **Artificial Intelligence** menu item appears in the admin dashboard, allowing administrators to create and manage **AI Profiles**.

An **AI Profile** defines how the AI system interacts with users — including its welcome message, system message, and response behavior.

> **Note:** This feature does **not** include any AI completion client implementations such as **OpenAI**. It only provides the **user interface** and **core services** for managing AI profiles. You must install and configure a compatible provider module (e.g., `OpenAI`, `Azure`, `AzureAIInference`, or `Ollama`) separately.

---

### Configuration

Before using the AI Services feature, ensure the required settings are properly configured. This can be done through the `appsettings.json` file or other configuration sources.

Below is an example configuration:

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
        "<!-- Provider name goes here (valid values: 'OpenAI', 'Azure', 'AzureAIInference', or 'Ollama') -->": {
          "DefaultConnectionName": "<!-- The default connection name to use from the Connections list -->",
          "DefaultDeploymentName": "<!-- The default deployment name -->",
          "DefaultEmbeddingDeploymentName": "<!-- The default embedding deployment name (optional, for embedding services) -->",
          "DefaultSpeechToTextDeploymentName": "<!-- The default speech-to-text deployment name (optional, for speech-to-text service)-->",
          "Connections": {
            "<!-- Connection name goes here -->": {
              "DefaultDeploymentName": "<!-- The default deployment name for this connection -->"
              "Type": "Chat", // Valid values are 'Chat', 'Embedding' or 'SpeechToText'
              // Provider-specific settings go here
            }
          }
        }
      }
    }
  }
}
```

---

### Provider Configuration

The following providers are supported **out of the box**:

* **OpenAI** — [View configuration guide](../CrestApps.OrchardCore.OpenAI/README.md)
* **Azure** — [View configuration guide](../CrestApps.OrchardCore.OpenAI.Azure/README.md)
* **AzureAIInference** — [View configuration guide](../CrestApps.OrchardCore.AzureAIInference/README.md)
* **Ollama** — [View configuration guide](../CrestApps.OrchardCore.Ollama/README.md)

> **Tip:** Most modern AI providers offer APIs that follow the **OpenAI API standard**.
> For these providers, use the **`OpenAI`** provider type when configuring their connections and endpoints.

Each provider can define multiple connections, and the `DefaultConnectionName` determines which one is used when multiple connections are available.

---

### Connection Types

When configuring provider connections, you can specify the connection type using the `Type` property. This allows you to define connections for different purposes within the same provider.

**Available Connection Types:**

- **`Chat`** (default) - For chat/completion models
- **`Embedding`** - For embedding models  
- **`SpeechToText`** - For speech-to-text models (voice input)

If no `Type` is specified, `Chat` is used as the default.

**Usage Example:**

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "<!-- Provider name goes here (valid values: 'OpenAI', 'Azure', 'AzureAIInference', or 'Ollama') -->": {
          "Connections": {
            "ChatConnection": {
              "Type": "Chat",
              "DefaultDeploymentName": "gpt-4",
              "Endpoint": "https://api.openai.com/v1",
              "ApiKey": "your-api-key"
            },
            "EmbeddingConnection": {
              "Type": "Embedding",
              "DefaultDeploymentName": "text-embedding-3-small",
              "Endpoint": "https://api.openai.com/v1",
              "ApiKey": "your-api-key"
            },
            "WhisperConnection": {
              "Type": "SpeechToText",
              "DefaultDeploymentName": "whisper-1",
              "Endpoint": "https://api.openai.com/v1",
              "ApiKey": "your-api-key"
            }
          }
        }
      }
    }
  }
}
```

For provider-specific configuration examples, see:
- [OpenAI Configuration Guide](../CrestApps.OrchardCore.OpenAI/README.md)
- [Azure OpenAI Configuration Guide](../CrestApps.OrchardCore.OpenAI.Azure/README.md)

---

### Provider Configuration

The following providers are supported **out of the box**:

* **OpenId** — [View configuration guide](../CrestApps.OrchardCore.OpenAI/README.md)
* **Azure** — [View configuration guide](../CrestApps.OrchardCore.OpenAI.Azure/README.md)
* **AzureAIInference** — [View configuration guide](../CrestApps.OrchardCore.OpenAI.AzureAIInference/README.md)
* **Ollama** — [View configuration guide](../CrestApps.OrchardCore.OpenAI.Ollama/README.md)

Each provider requires its own connection and deployment settings. The `DefaultConnectionName` determines which connection is used when multiple connections are configured.

---

### Microsoft.AI.Extensions

The AI module is built on top of [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI), making it easy to integrate AI services into your application. We provide the `IAIClientFactory` service, which allows you to easily create standard services such as `IChatClient`,  `IEmbeddingGenerator` and `ISpeechToTextClient` for any of your configured providers and connections.

Simply inject `IAIClientFactory` into your service and use the `CreateChatClientAsync`, `CreateEmbeddingGeneratorAsync` or `CreateISpeechToTextClientAsync` methods to obtain the required client.

### AI Deployments Feature

The **AI Deployments** feature extends the **AI Services** feature by enabling AI model deployment capabilities.

### AI Chat Services Feature

The **AI Chat Services** feature builds upon the **AI Services** feature by adding AI chat capabilities. This feature is enabled on demand by other modules that provide AI completion clients.

### AI Chat WebAPI

The **AI Chat WebAPI** feature extends the **AI Chat Services** feature by enabling a REST WebAPI endpoints to allow you to interact with the models.

### AI Connection Management  

The **AI Connection Management** feature enhances **AI Services** by providing a user interface to manage provider connections.  

#### Setting Up a Connection  

1. **Navigate to AI Settings**  
   - Go to **"Artificial Intelligence"** in the admin menu.  
   - Click **"Provider Connections"** to configure a new connection.  

2. **Add a New Connection**  
   - Click **"Add Connection"**, select a provider, and enter the required details.  
   - Example configurations are in the next section.

#### Example Configurations for Common Providers

> [!IMPORTANT]  
> You need to use a paid plan for all of these even when using models that are free from the web. Otherwise, you'll get various errors along the lines of `insufficient_quota`.

- DeepSeek
  - **Deployment name** (the model to use): e.g. `deepseek-chat`.
  - **Endpoint**: `https://api.deepseek.com/v1/`.
  - **API Key**: Generate one in [DeepSeek Platform](https://platform.deepseek.com).
- Google Gemini
  - **Deployment name**: e.g. `gemini-2.0-flash`.
  - **Endpoint**: `https://generativelanguage.googleapis.com/v1beta/openai/`.
  - **API Key**: Generate one in [Google AI Studio](https://aistudio.google.com).
- OpenAI
  - **Deployment name**: e.g. `gpt-4o-mini`.
  - **Endpoint**: `https://api.openai.com/v1/`.
  - **API Key**: Generate one in [OpenAI Platform](https://platform.openai.com/account/api-keys).

#### Creating AI Profiles  

After setting up a connection, you can create **AI Profiles** to interact with the configured model.  

#### Recipes

You can add or update a connection using **recipes**. Below is a recipe for adding or updating a connection to the **DeepSeek** service:  

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "OpenAI",
          "Name": "deepseek",
          "IsDefault": false,
          "DefaultDeploymentName": "deepseek-chat",
          "DisplayText": "DeepSeek",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.deepseek.com/v1",
              "ApiKey": "<!-- DeepSeek API Key -->"
            }
          }
        }
      ]
    }
  ]
}
```  

This recipe ensures that a **DeepSeek** connection is added or updated within the AI provider settings. Replace `<!-- DeepSeek API Key -->` with a valid API key to authenticate the connection.  

If a connection with the same `Name` and `Source` already exists, the recipe updates its properties. Otherwise, it creates a new connection.

### AI Data Source Management 

The **AI Data Source Management** feature enhances **AI Services** by offering a user-friendly interface for managing data sources accessible to AI models. To add data sources, you must first enable at least one feature that supplies a data source. For instance, the **Azure AI Search-Powered Data Source** feature provides access to data stored in Azure AI Search, enabling AI-powered conversational capabilities with that data.

#### Creating Data Source via Recipe

You can add or update data-source using recipe. Here is an example or creating a data-source


```
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "ProfileSource": "AzureAISearch",
          "Type": "azure_search",
          "DisplayText": "Articles (Azure AI Search)",
          "Properties": {
            "AzureAIProfileAISearchMetadata": {
              "IndexName": "articles"
            }
          }
        }
      ]
    }
  ]
}
```

### Defining Chat Profiles Using Code

To define chat profiles programmatically, create a migration class. Here's an example demonstrating how to create a new chat profile:

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

## AI Tool Management Feature

### Extending AI Chat with Custom Functions

You can enhance the AI chat functionality by adding custom functions. To create a custom function, inherit from `AIFunction` and register it as a service. Below is an example of a custom function that retrieves weather information based on the user's location:

```csharp
public sealed class GetWeatherFunction : AIFunction
{
    public const string TheName = "get_weather";

    public GetWeatherFunction()
    {
        Name = TheName;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
         {
           "type": "object",
           "properties": {
             "Location": {
               "type": "string",
               "description": "The geographic location for which the weather information is requested."
             }
           },
           "additionalProperties": false,
           "required": ["Location"]
         }
        """);
    }

    public override string Name { get; }

    public override string Description => "Retrieves weather information for a specified location.";

    public override JsonElement JsonSchema { get; }

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetValue("Location", out var prompt) || prompt is null) 
        {
            return ValueTask.FromResult<object>("Location is required.");
        }

        string location = null;

        if (prompt is JsonElement jsonElement)
        {
            location = jsonElement.GetString();
        }
        else
        {
            location = prompt?.ToString();
        }

        var weather = Random.Shared.NextDouble() > 0.5 ? $"It's sunny in {location}." : $"It's raining in {location}.";

        return ValueTask.FromResult<object>(weather);
    }
}
```

#### Registering the Custom Function

To register the custom function, add it as a service in the `Startup` class:

```csharp
services.AddAITool<GetWeatherFunction>(GetWeatherFunction.TheName);
```

Alternatively, you can register it with configuration options:

```csharp
services.AddAITool<GetWeatherFunction>(GetWeatherFunction.TheName, options =>
{
    options.Title = "Weather Getter";
    options.Description = "Retrieves weather information for a specified location.";
    options.Category = "Service";
});
```

Once registered, the function can be accessed via `IAIToolsService` in your module.

---

### Using AI Tool Sources

AI tool sources allow you to define additional parameters for AI tools through a user interface. For example, let's create a tool source that enables invoking different AI profiles.

#### Creating a Custom AI Tool Source

To create a custom tool source, implement the `IAIToolSource` interface. Below is an example:

```csharp
public sealed class ProfileAwareAIToolSource : IAIToolSource
{
    public const string ToolSource = "ProfileAware";

    private readonly ILogger<ProfileAwareAIToolSource> _logger;
    private readonly IAICompletionService _completionService;
    private readonly IAIProfileStore _profileStore;

    public ProfileAwareAIToolSource(
        ILogger<ProfileAwareAIToolSource> logger,
        IAICompletionService completionService,
        IAIProfileStore profileStore,
        IStringLocalizer<ProfileAwareAIToolSource> localizer)
    {
        _logger = logger;
        _completionService = completionService;
        _profileStore = profileStore;
        DisplayName = localizer["Profile Invoker"];
        Description = localizer["Provides a function that calls another profile."];
    }

    public string Name => ToolSource;
    public AIToolSourceType Type => AIToolSourceType.Function;
    public LocalizedString DisplayName { get; }
    public LocalizedString Description { get; }

    public async Task<AITool> CreateAsync(AIToolInstance instance)
    {
        if (!instance.TryGet<AIProfileFunctionMetadata>(out var metadata) || string.IsNullOrEmpty(metadata.ProfileId))
        {
            return new ProfileInvoker(_completionService, instance, null, _logger);
        }

        var profile = await _profileStore.FindByIdAsync(metadata.ProfileId);
        return new ProfileInvoker(_completionService, instance, profile, _logger);
    }

    private sealed class ProfileInvoker : AIFunction
    {
        private const string PromptProperty = "Prompt";
        private readonly IAICompletionService _completionService;
        private readonly ILogger _logger;
        private readonly AIProfile _profile;

        public override AIFunctionMetadata Metadata { get; }

        public ProfileInvoker(
            IAICompletionService completionService,
            AIToolInstance instance,
            AIProfile profile,
            ILogger logger)
        {
            _completionService = completionService;
            _profile = profile;
            _logger = logger;

            var funcMetadata = instance.As<InvokableToolMetadata>();

            Metadata = new AIFunctionMetadata(instance.Id)
            {
                Description = string.IsNullOrEmpty(funcMetadata.Description)
                    ? "Provides a way to call another model."
                    : funcMetadata.Description,
                Parameters =
                [
                    new AIFunctionParameterMetadata(PromptProperty)
                    {
                        Description = "The user's prompt.",
                        IsRequired = true,
                        ParameterType = typeof(string),
                    }
                ],
                ReturnParameter = new AIFunctionReturnParameterMetadata
                {
                    Description = "The model's response to the user's prompt.",
                    ParameterType = typeof(string),
                },
            };
        }

        protected override async Task<object> InvokeCoreAsync(IEnumerable<KeyValuePair<string, object>> arguments, CancellationToken cancellationToken)
        {
            if (_profile is null)
            {
                return "The profile does not exist.";
            }

            try
            {
                var promptObject = arguments.First(x => x.Key == PromptProperty).Value;
                var promptString = promptObject switch
                {
                    JsonElement jsonElement => jsonElement.GetString(),
                    JsonNode jsonNode => jsonNode.ToJsonString(),
                    string str => str,
                    _ => null
                };

                var context = new AICompletionContext
                {
                    Profile = _profile,
                    DisableTools = true,
                };

                return await _completionService.CompleteAsync(
                    _profile.Source,
                    [new ChatMessage(ChatRole.User, promptString)],
                    context,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invoking profile '{ProfileId}' from source '{Source}'.", _profile.Id, _profile.Source);
                return "Unable to get a response from the profile.";
            }
        }
    }
}
```

#### Registering the AI Tool Source

To register the source, add the following line to your `Startup` class:

```csharp
services.AddAIToolSource<ProfileAwareAIToolSource>(ProfileAwareAIToolSource.ToolSource);
```

After registering, navigate to **Artificial Intelligence** in the admin menu. You will find a new menu item called **Tools**, where you can create multiple instances of the newly registered **Profile Invoker** tool source.

### Configuring AI Profiles with Custom Functions

Once the custom function is registered, you can add it to any AI profile. The custom function will be available in the list of functions when creating or editing a profile, and you can enable or disable it as needed.

---

## Adding Custom AI Profile Sources  

To integrate custom AI sources, implement the `IAICompletionClient` interface or use the `NamedAICompletionClient` base class.  

### Implementing a Custom Completion Client  

Below is an example of a custom AI completion client that extends `NamedAICompletionClient`:  

```csharp
public sealed class CustomCompletionClient : NamedAICompletionClient
{
    public CustomCompletionClient(
           IAIClientFactory aIClientFactory,
           ILoggerFactory loggerFactory,
           IDistributedCache distributedCache,
           IOptions<AIProviderOptions> providerOptions,
           IEnumerable<IAICompletionServiceHandler> handlers,
           IOptions<DefaultAIOptions> defaultOptions
           ) : base(
               CustomProfileSource.ImplementationName,
               aIClientFactory, distributedCache,
               loggerFactory,
               providerOptions.Value,
               defaultOptions.Value,
               handlers)
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

> **Note:**  
> The `CustomCompletionClient` class inherits from `NamedAICompletionClient`. If the provider supports multiple deployments, consider inheriting from `DeploymentAwareAICompletionClient` instead.  

Next, you'll need to implement `IAIClientProvider` interface. You may look at the codebase for an implementation example. Finally, register the services

```
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAIClientProvider, CustomAIClientProvider>()
            .AddAIProfile<CustomCompletionClient>(CustomProfileSource.ImplementationName, CustomProfileSource.ProviderName, o =>
            {
                o.DisplayName = S["Custom Profile Provider"];
                o.Description = S["Provides AI profiles using custom source."];
            });
    }
}
```

#### Supporting Multiple Deployments  

If your custom AI provider supports multiple deployments or models, register a deployment provider as follows:  

```csharp
public sealed class Startup : StartupBase
{
    private readonly IStringLocalizer _localizer;

    public Startup(IStringLocalizer<Startup> localizer)
    {
        _localizer = localizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIDeploymentProvider("CustomAI", options =>
        {
            options.DisplayName = _localizer["CustomAI"];
            options.Description = _localizer["CustomAI deployments."];
        });
    }
}
```

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

#### Deleting AI Deployments via Recipes

You can delete model deployments using the `DeleteAIDeployments` recipe step. This step supports deleting specific deployments by name or deleting all deployments.

- Delete all deployments:

```json
{
  "steps": [
    {
      "name": "DeleteAIDeployments",
      "IncludeAll": true
    }
  ]
}
```

- Delete specific deployments by name:

```json
{
  "steps": [
    {
      "name": "DeleteAIDeployments",
      "DeploymentNames": [
        "gpt-4o-mini",
        "my-custom-deployment"
      ]
    }
  ]
}
```

Notes:
- Deployment names are matched case-insensitively.
- If `IncludeAll` is `true`, all deployments will be removed and `DeploymentNames` is ignored.
- Ensure the `AI Deployments` feature and the `OrchardCore.Recipes` feature are enabled.

---

### AI Chat with Workflows

When combined with the **Workflows** feature, the **AI Services** module introduces new activities that allow workflows to interact directly with AI chat services.

#### AI Completion using Profile Task

This activity lets you request AI completions using an existing **AI Profile**, and store the response in a workflow property.
To use it, search for the **AI Completion using Profile** task in your workflow and specify a unique **Result Property Name**.
The generated response will be saved in this property.

For example, if the **Result Property Name** is `AI-CrestApps-Step1`, you can access the response later using:

```liquid
{{ Workflow.Output["AI-CrestApps-Step1"].Content }}
```

To prevent naming conflicts with other workflow tasks, it's recommended to prefix your **Result Property Name** with `AI-`.

#### AI Completion using Direct Config Task

This activity allows you to request AI completions by defining the configuration directly within the workflow, without relying on a predefined AI Profile.
To use it, search for the **AI Completion using Direct Config** task in your workflow and specify a unique **Result Property Name**.
The generated response will be saved in this property.

For example, if the **Result Property Name** is `AI-CrestApps-Step1`, you can access the response later using:

```liquid
{{ Workflow.Output["AI-CrestApps-Step1"].Content }}
```

As with other AI tasks, it's recommended to prefix your **Result Property Name** with `AI-` to avoid conflicts.

---

### Deployments with AI Chat

The **AI Services** feature integrates with the **Deployments** module, allowing profiles to be deployed to various environments through Orchard Core's Deployment UI.

---

## Compatibility  

This module is fully compatible with OrchardCore v2.1 and later. However, if you are using OrchardCore versions between `v2.1` and `3.0.0-preview-18562`, you must install the [CrestApps.OrchardCore.Resources module](../CrestApps.OrchardCore.Resources/README.md) module into your web project. Then, enable the `CrestApps.OrchardCore.Resources` feature to ensure all required resource dependencies are available.  

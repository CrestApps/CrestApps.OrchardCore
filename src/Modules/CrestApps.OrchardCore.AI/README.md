## AI Services Feature

The **AI Services** feature enables interaction with AI models by providing essential services. Once activated, a new **Artificial Intelligence** menu item appears in the admin menu, offering options to manage AI profiles.

An **AI Profile** defines how the AI chatbot engages with users, including configuring the chatbot's welcome message, system message, and response behavior.

Note: This feature does not provide any completion client implementations (e.g., OpenAI, DeepSeek, etc.). It only provides a user interface to manage AI profiles along with the core services.

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

### AI Chat Services Feature

The **AI Chat Services** feature builds upon the **AI Services** feature by adding AI chat capabilities. This feature is enabled on demand by other modules that provide AI completion clients.

### AI Chat WebAPI

The **AI Chat WebAPI** feature extends the **AI Chat Services** feature by enabling a REST WebAPI endpoints to allow you to interact with the models.

### AI Connection Management  

The **AI Connection Management** feature enhances **AI Services** by providing a user interface to manage provider connections.  

#### Setting Up a Connection  

1. **Navigate to AI Settings**  
   - Go to **"Artificial Intelligence"** in the admin menu.  
   - Click **"Connections"** to configure a new connection.  

2. **Add a New Connection**  
   - Click **"Add Connection"**, select a provider, and enter the required details.  
   - Example: Connecting to **Google Gemini**  
     - Generate an **API Key** from [Google AI Studio](https://aistudio.google.com).  
     - Enter the **Endpoint**:  
       ```
       https://generativelanguage.googleapis.com/v1beta/openai/
       ```  
     - Specify the **Model**, e.g., **gemini-2.0-flash**.  

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
    private const string _locationProperty = "Location";
    public const string TheName = "get_weather";

    public GetWeatherFunction()
    {
        Name = TheName;
        Description = "Retrieves weather information for a specified location.";

        var metadata = new JsonObject()
        {
            {"type", "object"},
            {"properties", new JsonObject()
                {
                    { _locationProperty, new JsonObject()
                        {
                            {"type", "string" },
                            {"description", "The geographic location for which the weather information is requested." },
                        }
                    }
                }
            },
            {"required", new JsonArray(_locationProperty)},
            {"return_type", new JsonObject()
                {
                    {"type", "string"},
                    {"description", "The weather condition at the specified location."},
                }
            },
        };

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(metadata);
    }

    public override string Name { get; }

    public override string Description { get; }

    public override JsonElement JsonSchema { get; }

    protected override Task<object> InvokeCoreAsync(IEnumerable<KeyValuePair<string, object>> arguments, CancellationToken cancellationToken)
    {
        var prompt = arguments.First(x => x.Key == _locationProperty).Value;

        string location = null;

        if (prompt is JsonElement jsonElement)
        {
            location = jsonElement.GetString();
        }
        else if (prompt is JsonNode jsonNode)
        {
            location = jsonNode.ToJsonString();
        }
        else if (prompt is string str)
        {
            location = str;
        }
        else
        {
            location = prompt?.ToString();
        }

        var weather = Random.Shared.NextDouble() > 0.5 ? $"It's sunny in {location}." : $"It's raining in {location}.";

        return Task.FromResult<object>(weather);
    }
}
```

#### Registering the Custom Function

To register the custom function, add it as a service in the `Startup` class:

```csharp
services.AddAITool<GetWeatherFunction>(GetWeatherFunction.Name);
```

Alternatively, you can register it with configuration options:

```csharp
services.AddAITool<GetWeatherFunction>(GetWeatherFunction.Name, options =>
{
    options.Title = "Weather Getter";
    options.Description = "Retrieves weather information for a specified location.";
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

Here’s an improved version of your documentation with better structure, clarity, and consistency:  

---

## Adding Custom AI Profile Sources  

To integrate custom AI sources, implement the `IAICompletionClient` interface or use the `NamedAICompletionClient` base class.  

### Implementing a Custom Completion Client  

Below is an example of a custom AI completion client that extends `NamedAICompletionClient`:  

```csharp
public sealed class CustomCompletionClient : NamedAICompletionClient
{
    public CustomCompletionClient(
       ILoggerFactory loggerFactory,
       IDistributedCache distributedCache,
       IOptions<AIProviderOptions> providerOptions,
       IAIToolsService toolsService,
       IOptions<DefaultAIOptions> defaultOptions
    ) : base(CustomProfileSource.ImplementationName, distributedCache, loggerFactory, providerOptions.Value, defaultOptions.Value, toolsService)
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

---

### Registering the Custom Completion Client  

Once you've implemented the custom client, register it as an AI profile source in `Startup` file:  

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
        services.AddAIProfile<CustomCompletionClient>("CustomAIDefaultImplementation", "CustomAI", options =>
        {
            options.DisplayName = _localizer["CustomAI"];
            options.Description = _localizer["Provides AI profiles using the CustomAI provider."];
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

## OpenAI Chat Management

The **OpenAI Chat** feature allows you to manage OpenAI Chat profiles. Once enabled, a new **OpenAI** menu item will appear on the admin menu, providing options to manage your chat profiles.

### Requirements for Managing Profiles

To manage chat profiles, you must enable at least one feature that provides an AI chat profile source. CrestApps offers the following features to enable profile sources:

- **Azure OpenAI** (`CrestApps.OrchardCore.OpenAI.Azure.Standard`): AI-powered chat using Azure OpenAI models.
- **Azure OpenAI with Azure AI Search** (`CrestApps.OrchardCore.OpenAI.Azure.AISearch`): AI-powered chat using Azure OpenAI models combined with data from Azure AI Search.

For detailed documentation on Azure OpenAI features, [click here](../CrestApps.OrchardCore.OpenAI.Azure/README.md).

### Defining Chat Profiles Using Code

Sometimes you may need to define chat profiles using code. You can do this using a migration class. Here's an example of how to create a chat profile using a migration class:

```csharp
public sealed class SystemDefinedOpenAIProfileMigrations : DataMigration
{
    private readonly IOpenAIChatProfileManager _openAIChatProfileManager;
    private readonly IOpenAIDeploymentManager _openAIDeploymentManager;

    public SystemDefinedOpenAIProfileMigrations(
        IOpenAIChatProfileManager openAIChatProfileManager,
        IOpenAIDeploymentManager openAIDeploymentManager)
    {
        _openAIChatProfileManager = openAIChatProfileManager;
        _openAIDeploymentManager = openAIDeploymentManager;
    }

    public async Task<int> CreateAsync()
    {
        var deployments = await _openAIDeploymentManager.GetAllAsync();

        if (deployments.Any())
        {
            var profile = await _openAIChatProfileManager.NewAsync("Azure");

            profile.Name = "UniqueTechnicalName";
            profile.Type = OpenAIChatProfileType.Chat;
            profile.DeploymentId = deployments.First().Id;
            profile.SystemMessage = "some system message";
            // Set other properties as needed.

            profile.WithSettings(new OpenAIChatProfileSettings
            {
                LockSystemMessage = true, // prevent the user from changing the system message.
                IsRemovable = false, // prevent the user from removing the profile.
                IsListable = false, // prevent the user from listing the profile on the UI.
                IsOnAdminMenu = true, // show the profile on the admin menu. This option only when the profile of type chat.
            });

            await _openAIChatProfileManager.SaveAsync(profile);
        }

        return 1;
    }
}
```

> **Note**: If a profile with the same name already exists, creating a profile through a migration class will update the existing profile instead of creating a new one. To avoid conflicts, always use a unique name when defining a new profile.

### Default Parameters

By default, a set of parameters is available for configuration in each chat profile. These parameters can be adjusted using any supported settings provider. For example, here's how you can modify the parameters using the `appsettings.json` file:

```json
{
  "OrchardCore":{
    "CrestApps_OpenAI":{
      "DefaultParameters":{
        "Temperature":0,
        "TopP":1,
        "FrequencyPenalty":0,
        "PresencePenalty":0,
        "MaxOutputTokens":800,
        "PastMessagesCount":10
      }
    }
  }
}
```

### OpenAI Chat Tools

The module offers the flexibility to extend OpenAI's capabilities by adding custom functions, enabling the model to provide more tailored and accurate responses. If you need to implement a custom function, simply implement the `AIFunction` abstract class and register it as a service.

For example, to create a function that allows OpenAI to provide a response based on the user's location, you can implement the following:

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
        // Here you can access the arguments that were defined in Metadata.Parameters above.

        var location = arguments.First(x => x.Key == LocationProperty).Value as string;

        return Task.FromResult<object>(Random.Shared.NextDouble() > 0.5 ? $"It's sunny in {location}" : $"It's raining in {location}");
    }
}
```

#### Registering the Function

To register this function, you can use the `AddOpenAITool` extension method within your `Startup` class:

```csharp
services.AddOpenAITool<GetWeatherFunction>();
```

If you need to access the tools in your module, you can use the `IAIToolsService` interface to access the registered functions.

This process allows you to extend the functionality of OpenAI services by integrating your custom logic into a tool that can be utilized within the OpenAI ecosystem.

#### Configuring Chat Profiles

Once the function is registered, you can add it to your chat profiles. When creating or editing a profile, the new function will appear in the list of available functions, allowing you to enable or disable it for specific profiles.

### Custom Source Implementation

The OpenAI feature provides the necessary infrastructure and an extensible UI to support custom sources. You can add additional OpenAI sources by implementing the `IAIChatProfileSource` interface. For example:

```csharp
public sealed class AzureProfileSource : IOpenAIChatProfileSource
{
    public const string Key = "Azure";

    public AzureProfileSource(IStringLocalizer<AzureProfileSource> S)
    {
        DisplayName = S["Azure OpenAI"];
        Description = S["AI-powered chat using Azure OpenAI models."];
    }

    public string TechnicalName => Key;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
```

After implementing the source, register your custom implementation as follows:

```csharp
public sealed class StandardStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOpenAIChatProfileSource<AzureProfileSource>(AzureProfileSource.Key);
    }
}
```

### Recipes

If you're using the Recipes module, you can add AI chat profiles using the following recipe:

```json
{
  "steps":[
    {
      "name":"OpenAIChatProfile",
      "profiles":[
        {
          "Source":"CustomSource",
          "Name":"Example Profile",
          "WelcomeMessage": "What do you want to know?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "DeploymentId":"<!-- The deployment id for the deployment. -->",
          "SystemMessage":"You are an AI assistant that helps people find information.",
          "Properties": 
          {
              "OpenAIChatProfileMetadata": 
              {
                  "Temperature":null,
                  "TopP":null,
                  "FrequencyPenalty":null,
                  "PresencePenalty":null,
                  "MaxTokens":null,
                  "PastMessagesCount":null
              }
          }
        }
      ]
    }
  ]
}
```

### Workflows

When the OpenAI Chat feature is enabled alongside Workflows, the following workflow activities become available:

#### Chat Utility Completion Task

This activity enables interaction with the OpenAI chat service. You can use it to send a message to the chat service and store the generated response in a workflow property.

To include this activity in your workflow, search for the **Chat Utility Completion** task and add it. One of the required fields is the **Result Property Name**, which must be a unique identifier for this task. This identifier allows you to retrieve the generated response later using the `Workflow.Output` instance. Each **Chat Utility Completion** task must have a distinct name to differentiate the responses generated at each step. 

For example, if you set the **Result Property Name** to `OpenAI-Step1`, you can access the response in subsequent workflow steps using the following syntax:

```liquid
{{ Workflow.Output["OpenAI-Step1"].Content }}
```

If you need the response to be in HTML format, enable the `Include HTML Content` option. Then you can access the HTML content using the following syntax:
```liquid
{{ Workflow.Output["OpenAI-Step1"].HtmlContent }}
```

To avoid conflicts with other tasks that utilize the `Workflow.Output` accessor, it is recommended to prefix each **Result Property Name** in **Chat Utility Completion** task with `OpenAI-`.

### Deployments

When using the OpenAI Chat feature with the Deployments module, you can deploy chat profiles to different environments through Orchard Core's Deployment UI.

## OpenAI Chat Management

The **OpenAI Chat** feature allows you to manage OpenAI Chat profiles. Once enabled, a new **OpenAI** menu item will appear on the admin menu, providing options to manage your chat profiles.

### Requirements for Managing Profiles

To manage chat profiles, you must enable at least one feature that provides an AI chat profile source. CrestApps offers the following features to enable profile sources:

- **Azure OpenAI** (`CrestApps.OrchardCore.OpenAI.Azure.Standard`): AI-powered chat using Azure OpenAI models.
- **Azure OpenAI with Azure AI Search** (`CrestApps.OrchardCore.OpenAI.Azure.AISearch`): AI-powered chat using Azure OpenAI models combined with data from Azure AI Search.

For detailed documentation on Azure OpenAI features, [click here](../CrestApps.OrchardCore.OpenAI.Azure/README.md).

### OpenAI Chat Functions

We offer the flexibility to extend OpenAI's capabilities by adding custom functions, enabling the model to provide more tailored and accurate responses. If you need to implement a custom function, simply implement the `IOpenAIChatFunction` interface, which inherits from the `OpenAIChatFunctionBase` class.

For example, to create a function that allows OpenAI to provide a response based on the user's location, you can implement the following:

```csharp
public sealed class GetWeatherFunction : OpenAIChatFunctionBase
{
    public const string Key = "get_current_weather";

    public override string Name => Key;

    public override string Description => "Fetches the current weather for a specified location.";

    public GetWeatherFunction()
    {
        DefineProperty("location", new StringFunctionProperty
        {
            Description = "The city and state, e.g., San Francisco, CA.",
            IsRequired = true,
        });

        DefineProperty("unit", new EnumFunctionProperty<TempScale>
        {
            Description = "The temperature scale (Fahrenheit or Celsius) to use.",
            IsRequired = false,
        });
    }

    public override Task<string> InvokeAsync(JsonObject arguments)
    {
        var value = arguments.ToObject<GetWeatherArguments>();

        // In a real implementation, you would call a weather API here.
        // For simplicity, we're returning a static value.

        return Task.FromResult("Temperature: 80°F, Condition: Sunny");
    }
}

public enum TempScale
{
    Fahrenheit,
    Celsius,
}

public sealed class GetWeatherArguments
{
    public string Location { get; set; }

    public TempScale Unit { get; set; }
}
```

#### Registering the Function

To register this function, you can use the `AddAIChatFunction` extension method within your `Startup` class:

```csharp
services.AddAIChatFunction<GetWeatherFunction>(GetWeatherFunction.Key);
```

When defining the properties of the function, you have the following types to use as function properties:

- `StringFunctionProperty`: Represents a string property.
- `EnumFunctionProperty<TEnum>`: Represents an enumeration property.
- `BooleanFunctionProperty`: Represents a boolean property.
- `NumberFunctionProperty`: Represents a number property.
- `ObjectFunctionProperty`: Represents an object property.
- `ArrayFunctionProperty`: Represents an array property.

#### Configuring Chat Profiles

Once the function is registered, you can add it to your chat profiles. When creating or editing a profile, the new function will appear in the list of available functions, allowing you to enable or disable it for specific profiles.

### Custom Source Implementation

The OpenAI feature provides the necessary infrastructure and an extensible UI to support custom sources. You can add additional OpenAI sources by implementing the `IAIChatProfileSource` interface. For example:

```csharp
public sealed class AzureProfileSource : IAIChatProfileSource
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
        services.AddAIChatProfileSource<AzureProfileSource>(AzureProfileSource.Key);
    }
}
```

### Recipes

If you're using the Recipes module, you can add AI chat profiles using the following recipe:

```json
{
  "steps":[
    {
      "name":"AIChatProfile",
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
          "Temperature":null,
          "TopP":null,
          "FrequencyPenalty":null,
          "PresencePenalty":null,
          "TokenLength":null,
          "PastMessagesCount":null
        }
      ]
    }
  ]
}
```

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
        DefineInputProperty(nameof(GetWeatherArguments.Location), new StringFunctionProperty
        {
            Description = "The city and state, e.g., San Francisco, CA.",
            IsRequired = true,
        });

        DefineInputProperty(nameof(GetWeatherArguments.Unit), new EnumFunctionProperty<TempScale>
        {
            Description = "The temperature scale (Fahrenheit or Celsius) to use.",
            IsRequired = false,
        });
    }

    public override Task<object> InvokeAsync(JsonObject arguments)
    {
        var value = arguments.ToObject<GetWeatherArguments>();

        // In a real implementation, you would call a weather API here.
        // For simplicity, we're returning a static value.
        // Here we return a string. But you may provide a complex type.
        // If you are returning a complex type, you should define the return type by setting the ReturnType.

        return Task.FromResult<object>("Temperature: 80°F, Condition: Sunny");
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

To register this function, you can use the `AddOpenAIChatFunction` extension method within your `Startup` class:

```csharp
services.AddOpenAIChatFunction<GetWeatherFunction>(GetWeatherFunction.Key);
```

### Tools

When working with OpenAI Services that support the tool functionality, you can create custom tools by mapping functions to the tool interface. Here's an example of how to define and register a tool:

#### Define a custom Tool

To define a custom tool, subclass `OpenAIChatFunctionTool` and implement the `IOpenAIChatTool` interface. For example, if you're building a weather-fetching tool, you can create a class like this:

```csharp
public sealed class GetWeatherOpenAITool : OpenAIChatFunctionTool, IOpenAIChatTool
{
    // Constructor takes an instance of the function you're integrating with the tool
    public GetWeatherOpenAITool(GetWeatherOpenAIFunction function)
        : base(function)
    {
    }
}
```

In this code:
- `GetWeatherOpenAITool` is a tool that wraps around a weather-related function (`GetWeatherOpenAIFunction`).
- The constructor accepts a function and passes it to the base class to ensure proper integration.

#### Register the Tool

Once the tool class is defined, you can register it with your service container so that it can be used in your application. Use dependency injection to register the tool like this:

```csharp
services.AddOpenAIChatTool<GetWeatherOpenAITool, GetWeatherOpenAIFunction>();
```

In this step:
- `AddOpenAIChatTool` registers the `GetWeatherOpenAITool` and its associated function (`GetWeatherOpenAIFunction`) with the services collection.
- This enables your application to recognize and utilize the tool seamlessly.

### Summary
1. **Define the Tool**: Create a class that wraps around the function you want to expose as a tool, inheriting from `OpenAIChatFunctionTool` and implementing `IOpenAIChatTool`.
2. **Register the Tool**: Use the `AddOpenAIChatTool` method to register the tool with your dependency injection container.

This process allows you to extend the functionality of OpenAI services by integrating your custom logic into a tool that can be utilized within the OpenAI ecosystem.

When defining the properties of the function, you have the following types to use as function properties:

- `StringFunctionProperty`: Represents a string property. You can define formatted strings using common formats such as the following:
  - `StringFunctionProperty.DateTime`: Represents a date-time property.
  - `StringFunctionProperty.Uri`: Represents a uri property.
  - `StringFunctionProperty.Hostname`: Represents a hostname property.
  - `StringFunctionProperty.Ipv4`: Represents a ipv4 property.
  - `StringFunctionProperty.Ipv6`: Represents a ipv6 property.
  - `StringFunctionProperty.UUID`: Represents a uuid property.
  - `StringFunctionProperty.Phone`: Represents a phone property.
  - `StringFunctionProperty.CreditCard`: Represents a credit-card property.
  - `StringFunctionProperty.Password`: Represents a password property.
- `NumberFunctionProperty`: Represents a number property. You can define formatted numbers using common formats such as the following:
  - `NumberFunctionProperty.Integer`: Represents an integer property.
  - `NumberFunctionProperty.Long`: Represents an big-integer property.
  - `NumberFunctionProperty.Float`: Represents an float property.
  - `NumberFunctionProperty.Decimal`: Represents an decimal property.
- `ObjectFunctionProperty`: Represents an object property.
- `ArrayFunctionProperty`: Represents an array property.
- `BooleanFunctionProperty`: Represents a boolean property.
- `EnumFunctionProperty<TEnum>`: Represents an enumeration property.

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

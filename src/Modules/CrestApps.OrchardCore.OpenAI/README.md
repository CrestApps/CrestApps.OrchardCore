## OpenAI Chat Management

The **OpenAI Chat** feature allows you to manage OpenAI Chat profiles. Once enabled, a new **OpenAI** menu item will appear on the admin menu, providing options to manage your chat profiles.

### Requirements for Managing Profiles

To manage chat profiles, you must enable at least one feature that provides an AI chat profile source. CrestApps offers the following features to enable profile sources:

- **Azure OpenAI** (`CrestApps.OrchardCore.OpenAI.Azure.Standard`): AI-powered chat using Azure OpenAI models.
- **Azure OpenAI with Azure AI Search** (`CrestApps.OrchardCore.OpenAI.Azure.AISearch`): AI-powered chat using Azure OpenAI models combined with data from Azure AI Search.

For detailed documentation on Azure OpenAI features, [click here](../CrestApps.OrchardCore.OpenAI.Azure/README.md).

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
          "Source":"Azure",
          "Name":"Example Profile",
          "DeploymentName":"<!-- Your Azure model deployment name goes here -->",
          "SystemMessage":"You are an AI assistant that helps people find information."
        }
      ]
    }
  ]
}
```

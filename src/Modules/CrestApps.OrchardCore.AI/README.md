## Table of Contents

> 📖 **Full documentation is available at [orchardcore.crestapps.com](https://orchardcore.crestapps.com/docs/ai/ai-services).**

- [AI Services Feature](#ai-services-feature)
  - [Configuration](#configuration)
  - [Provider Configuration](#provider-configuration)
  - [Microsoft.AI.Extensions](#microsoftaiextensions)
  - [AI Deployments Feature](#ai-deployments-feature)
  - [AI Chat Services Feature](#ai-chat-services-feature)
  - [AI Chat WebAPI](#ai-chat-webapi)
  - [AI Connection Management](#ai-connection-management)
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
        "MaximumIterationsPerRequest": 10,
        "EnableOpenTelemetry": false,
        "EnableDistributedCaching": true
      },
      "Providers": {
        "<!-- Provider name goes here (valid values: 'OpenAI', 'Azure', 'AzureAIInference', or 'Ollama') -->": {
          "DefaultConnectionName": "<!-- The default connection name to use from the Connections list -->",
          "DefaultDeploymentName": "<!-- The default deployment name -->",
          "DefaultUtilityDeploymentName": "<!-- Optional: a lightweight model for auxiliary tasks like query rewriting and planning -->",
          "DefaultEmbeddingDeploymentName": "<!-- The default embedding deployment name (optional, for embedding services) -->",
          "DefaultImagesDeploymentName": "<!-- The default deployment name for image generation (optional, e.g., 'dall-e-3') -->",
          "Connections": {
            "<!-- Connection name goes here -->": {
              "DefaultDeploymentName": "<!-- The default deployment name for this connection -->",
              "DefaultUtilityDeploymentName": "<!-- Optional: a lightweight model for auxiliary tasks -->",
              "DefaultImagesDeploymentName": "<!-- The image generation deployment name (optional, e.g., 'dall-e-3') -->"
              // Provider-specific settings go here
            }
          }
        }
      }
    }
  }
}
```

#### Default Parameters

| Setting | Description | Default |
|---------|-------------|---------|
| `Temperature` | Controls randomness. Lower values produce more deterministic results. | `0` |
| `MaxOutputTokens` | Maximum number of tokens in the response. | `800` |
| `TopP` | Controls diversity via nucleus sampling. | `1` |
| `FrequencyPenalty` | Reduces repetition of token sequences. | `0` |
| `PresencePenalty` | Encourages the model to explore new topics. | `0` |
| `PastMessagesCount` | Number of previous messages included as conversation context. | `10` |
| `MaximumIterationsPerRequest` | Maximum number of tool-call round-trips the model can make per request. Set to a higher value (e.g., `10`) to enable agentic behavior where the model can call tools, evaluate results, and call additional tools as needed. A value of `1` limits the model to a single tool call with no follow-up. | `10` |
| `EnableOpenTelemetry` | Enables OpenTelemetry tracing for AI requests. | `false` |
| `EnableDistributedCaching` | Enables distributed caching for AI responses. | `true` |

#### Deployment Name Settings

| Setting | Description | Required |
|---------|-------------|----------|
| `DefaultDeploymentName` | The default model for chat completions | Yes |
| `DefaultUtilityDeploymentName` | A lightweight model for auxiliary tasks such as query rewriting, planning, and chart generation. Falls back to `DefaultDeploymentName` when not set. | No |
| `DefaultEmbeddingDeploymentName` | The model for generating embeddings (for RAG/vector search) | No |
| `DefaultImagesDeploymentName` | The model for image generation (e.g., `dall-e-3`). Required for image generation features. | No |

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

### Microsoft.AI.Extensions

The AI module is built on top of [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI), making it easy to integrate AI services into your application. We provide the `IAIClientFactory` service, which allows you to easily create standard services such as `IChatClient` and `IEmbeddingGenerator` for any of your configured providers and connections.

Simply inject `IAIClientFactory` into your service and use the `CreateChatClientAsync` or `CreateEmbeddingGeneratorAsync` methods to obtain the required client.

### AI Deployments Feature

The **AI Deployments** feature extends the **AI Services** feature by enabling AI model deployment capabilities.

### AI Chat Services Feature

The **AI Chat Services** feature builds upon the **AI Services** feature by adding AI chat capabilities. This feature is enabled on demand by other modules that provide AI completion clients.

### AI Chat WebAPI

The **AI Chat WebAPI** feature extends the **AI Chat Services** feature by enabling a REST WebAPI endpoints to allow you to interact with the models.

### AI Connection Management  

The **AI Connection Management** feature enhances **AI Services** by providing a user interface to manage provider connections.  

---

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
          "DefaultUtilityDeploymentName": "deepseek-chat",
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

Data source (RAG/Knowledge Base) documentation is in the `CrestApps.OrchardCore.AI.DataSources` module: [README](../CrestApps.OrchardCore.AI.DataSources/README.md).

### Defining AI Profiles Using Code

To define AI profiles programmatically, create a migration class. Here's an example demonstrating how to create a new chat profile:

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

### AI Profile Types

An **AI Profile** describes *how* the system should interact with an AI model (or tool) and how it should behave in the UI.

The following profile types are supported:

| Profile Type | Description | When to use |
|---|---|---|
| `Chat` | A conversational profile that persists a chat session and appends user/assistant messages over time. | The default for chat experiences (assistants, Q&A bots, RAG chat, etc.). |
| `Utility` | A stateless profile intended for single-shot tasks. It does not save a chat session and is treated as a one-off completion. | Quick actions like rewriting text, extracting keywords, small transformations, or other “tools” that shouldn’t create chat history. |
| `TemplatePrompt` | A profile that **generates a prompt using a Liquid template** (for example from the current session messages) and then sends that generated prompt to a model. The response is saved in the chat session as a generated prompt message. | Actions that need structured prompts and access to the current session context, such as “summarize”, “draft an email from this conversation”, “extract decisions”, etc. |

> Note: In the UI, `TemplatePrompt` profiles are commonly exposed as "tools" (predefined actions). When invoked, the system renders the profile's Liquid `PromptTemplate` using the current session as input.

---

### Example: Template Prompt (Generated Prompt) — Chat Session Summarizer

Below is an example of a **Template Prompt** profile that summarizes the current chat session.

- **Title**: Chat session summarizer
- **Technical name**: `ChatSessionSummarizer`
- **Type**: `TemplatePrompt`
- **Prompt Subject**: Summary

**Prompt template:**

```liquid
{% for prompt in Session.Prompts %}
  {% unless prompt.IsGeneratedPrompt %}
Role: {{ prompt.Role }}
Message: {{ prompt.Content }}

  {% endunless %}
{% endfor %}
```

**System Instruction:**

You are a summarization assistant.

Your task is to read a conversation and produce a clear, concise summary that captures:
- The main topics discussed
- Key decisions, conclusions, or outcomes
- Important questions, requests, or action items

Guidelines:
- Be factual and neutral
- Do not add new information or assumptions
- Remove small talk, repetition, and irrelevant details
- Preserve important technical terms and names
- Use plain language

Output format:
- A short paragraph summary
- Followed by a bullet list of key points or action items (if any)

```

> **Note**: If a profile with the same name already exists, creating a new profile through a migration class will update the existing one. Always use a unique name for new profiles to avoid conflicts.

---

## AI Tool Management Feature

### Extending AI Chat with Custom Functions

You can enhance the AI chat functionality by adding custom functions. To create a custom function, inherit from `AIFunction` and register it as a service. AI tools are registered as singletons, so dependencies must be resolved at execution time using `arguments.Services`.

Below is an example of a custom function that retrieves weather information based on the user's location:

```csharp
public sealed class GetWeatherFunction : AIFunction
{
    public const string TheName = "get_weather";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
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

    public override string Name => TheName;

    public override string Description => "Retrieves weather information for a specified location.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        // For dependencies, resolve them at runtime via arguments.Services:
        // var someService = arguments.Services.GetRequiredService<ISomeService>();

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

To register the custom function, add it as a service in the `Startup` class. AI tools use a fluent builder pattern for registration:

```csharp
services.AddAITool<GetWeatherFunction>(GetWeatherFunction.TheName)
    .WithTitle("Weather Getter")
    .WithDescription("Retrieves weather information for a specified location.")
    .WithCategory("Service")
    .Selectable();
```

##### Builder Methods

| Method | Description |
| --- | --- |
| `.WithTitle(string)` | Sets the display title for the tool. |
| `.WithDescription(string)` | Sets the description shown in the UI and used by the orchestrator for planning. |
| `.WithCategory(string)` | Sets the category for grouping in the UI. |
| `.WithPurpose(string)` | Tags the tool with a purpose identifier (e.g., `AIToolPurposes.DocumentProcessing`). The orchestrator uses purpose tags to dynamically discover tools by function. |
| `.Selectable()` | Makes the tool visible in the UI for user selection. **By default, tools are system tools** (hidden from the UI and managed by the orchestrator). Call `.Selectable()` to allow users to select the tool in Chat Interactions or AI Profiles. |

##### System Tools vs. Selectable Tools

- **System tools** (default): Automatically included by the orchestrator based on context. Not shown in the UI. Ideal for document processing, content generation, or other infrastructure tools.
- **Selectable tools**: Visible in the UI for users to choose per Chat Interaction or AI Profile. Use `.Selectable()` when the tool represents a user-facing capability.

```csharp
// System tool (hidden from UI, orchestrator-managed)
services.AddAITool<ListDocumentsTool>(ListDocumentsTool.TheName)
    .WithTitle("List Documents")
    .WithDescription("Lists all documents attached to the current chat session.")
    .WithPurpose(AIToolPurposes.DocumentProcessing);

// Selectable tool (visible in UI for user selection)
services.AddAITool<SearchForContentsTool>(SearchForContentsTool.TheName)
    .WithTitle("Search Content Items")
    .WithDescription("Provides a way to search for content items.")
    .WithCategory("Content Management")
    .Selectable();
```

##### Well-Known Purpose Constants

The `AIToolPurposes` class provides well-known purpose identifiers:

| Constant | Value | Description |
| --- | --- | --- |
| `AIToolPurposes.DocumentProcessing` | `"document_processing"` | Tools that process, read, search, or manage documents attached to a chat session. |
| `AIToolPurposes.ContentGeneration` | `"content_generation"` | Tools that generate content such as images or charts. |

You can also define custom purpose strings for domain-specific tool grouping.

Once registered, the function can be accessed via `IAIToolsService` in your module, which resolves tools by their name using keyed service resolution.

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

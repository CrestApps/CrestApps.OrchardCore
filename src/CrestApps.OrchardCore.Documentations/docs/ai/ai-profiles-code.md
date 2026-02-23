---
sidebar_label: AI Profiles (Code)
sidebar_position: 9
title: Defining AI Profiles via Code & Recipes
description: How to define AI profiles programmatically, via recipes, and manage deployments.
---

# Defining AI Profiles via Code & Recipes

## Defining AI Profiles Using Code

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

## AI Profile Types

An **AI Profile** describes *how* the system should interact with an AI model (or tool) and how it should behave in the UI.

The following profile types are supported:

| Profile Type | Description | When to use |
|---|---|---|
| `Chat` | A conversational profile that persists a chat session and appends user/assistant messages over time. | The default for chat experiences (assistants, Q&A bots, RAG chat, etc.). |
| `Utility` | A stateless profile intended for single-shot tasks. It does not save a chat session and is treated as a one-off completion. | Quick actions like rewriting text, extracting keywords, small transformations, or other "tools" that shouldn't create chat history. |
| `TemplatePrompt` | A profile that **generates a prompt using a Liquid template** (for example from the current session messages) and then sends that generated prompt to a model. The response is saved in the chat session as a generated prompt message. | Actions that need structured prompts and access to the current session context, such as "summarize", "draft an email from this conversation", "extract decisions", etc. |

> Note: In the UI, `TemplatePrompt` profiles are commonly exposed as "tools" (predefined actions). When invoked, the system renders the profile's Liquid `PromptTemplate` using the current session as input.

---

## Example: Template Prompt — Chat Session Summarizer

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

```
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

---

## Adding AI Profiles via Recipes

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

## Managing AI Deployments via Recipes

You can create or update AI deployments using the following recipe:

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

### Deleting AI Deployments via Recipes

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
            .AsChatClient(connection.GetChatDeploymentOrDefaultName());
    }
}
```

> **Note:**
> The `CustomCompletionClient` class inherits from `NamedAICompletionClient`. If the provider supports multiple deployments, consider inheriting from `DeploymentAwareAICompletionClient` instead.

Next, implement the `IAIClientProvider` interface. You may look at the codebase for an implementation example. Finally, register the services:

```csharp
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

### Supporting Multiple Deployments

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

---
sidebar_label: AI Tools
sidebar_position: 10
title: AI Tool Management
description: How to create, register, and manage custom AI tools in Orchard Core.
---

# AI Tool Management

This section is part of the **AI Services** (`CrestApps.OrchardCore.AI`) feature.

## Extending AI Chat with Custom Functions

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

## Registering the Custom Function

To register the custom function, add it as a service in the `Startup` class. AI tools use a fluent builder pattern for registration:

```csharp
services.AddAITool<GetWeatherFunction>(GetWeatherFunction.TheName)
    .WithTitle("Weather Getter")
    .WithDescription("Retrieves weather information for a specified location.")
    .WithCategory("Service")
    .Selectable();
```

### Builder Methods

| Method | Description |
| --- | --- |
| `.WithTitle(string)` | Sets the display title for the tool. |
| `.WithDescription(string)` | Sets the description shown in the UI and used by the orchestrator for planning. |
| `.WithCategory(string)` | Sets the category for grouping in the UI. |
| `.WithPurpose(string)` | Tags the tool with a purpose identifier (e.g., `AIToolPurposes.DocumentProcessing`). The orchestrator uses purpose tags to dynamically discover tools by function. |
| `.Selectable()` | Makes the tool visible in the UI for user selection. **By default, tools are system tools** (hidden from the UI and managed by the orchestrator). Call `.Selectable()` to allow users to select the tool in Chat Interactions or AI Profiles. |

### System Tools vs. Selectable Tools

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

### Well-Known Purpose Constants

The `AIToolPurposes` class provides well-known purpose identifiers:

| Constant | Value | Description |
| --- | --- | --- |
| `AIToolPurposes.DocumentProcessing` | `"document_processing"` | Tools that process, read, search, or manage documents attached to a chat session. |
| `AIToolPurposes.ContentGeneration` | `"content_generation"` | Tools that generate content such as images or charts. |

You can also define custom purpose strings for domain-specific tool grouping.

Once registered, the function can be accessed via `IAIToolsService` in your module, which resolves tools by their name using keyed service resolution.

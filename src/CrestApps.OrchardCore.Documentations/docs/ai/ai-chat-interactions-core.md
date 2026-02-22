---
sidebar_label: Chat Interactions Core
sidebar_position: 4
title: AI Chat Interactions Core
description: Shared core types, helpers, and intent-aware document processing infrastructure for AI Chat Interactions.
---

> **Developer Reference**: This is a core library consumed by the `AI Chat Interactions` module and its extensions. It is not a standalone Orchard Core feature.

Shared core types and helpers used by AI Chat Interactions modules.

## Purpose

- Defines index profile metadata and common constants
- Provides handlers and base classes for indexing and document integration
- Provides intent-aware, strategy-based document processing infrastructure

## Document Processing Architecture

This module provides an extensible architecture for processing documents in chat interactions based on detected user intent. Multiple strategies can contribute context to a single request.

### Key Components

#### Intent Detection

The `IPromptIntentDetector` interface allows classification of user intent for a chat message. Intents are string-based, allowing easy extensibility. Well-known intents are defined in `DocumentIntents`.

**Document-related intents:**
- `DocumentIntents.DocumentQnA` - Question answering over documents (RAG)
- `DocumentIntents.SummarizeDocument` - Document summarization
- `DocumentIntents.AnalyzeTabularData` - CSV/tabular data analysis
- `DocumentIntents.AnalyzeTabularDataByRow` - Row-by-row tabular processing (heavy)
- `DocumentIntents.ExtractStructuredData` - Data extraction
- `DocumentIntents.CompareDocuments` - Document comparison
- `DocumentIntents.TransformFormat` - Content transformation/reformatting
- `DocumentIntents.GeneralChatWithReference` - General chat with document reference

**Image generation intents:**
- `DocumentIntents.GenerateImage` - Generate an image from a text description
- `DocumentIntents.GenerateImageWithHistory` - Generate an image using conversation context
- `DocumentIntents.GenerateChart` - Generate charts and graphs

**External capability intents:**
- `DocumentIntents.LookingForExternalCapabilities` - The user needs tools, resources, or data from connected MCP servers

#### Processing Strategies

Prompt processing is implemented using strategies. All strategies implement `IPromptProcessingStrategy`.

##### First-Phase Strategies

First-phase strategies are resolved via DI as `IEnumerable<IPromptProcessingStrategy>` and called in sequence for every request:

- Each strategy decides internally whether it should contribute context based on the detected intent
- Multiple strategies can add context to the same request
- Register with `.WithStrategy<T>()` on the intent builder

See: `IPromptProcessingStrategy` and `DocumentProcessingStrategyBase`.

##### Heavy Strategies

Heavy strategies are strategies that are expensive in time/cost:

- May perform many LLM calls per user message (e.g., batching)
- May process large datasets row-by-row
- Should be gated behind configuration to avoid unexpected API costs

Register heavy strategies using the `.WithHeavyStrategy<T>()` builder method, which combines intent marking and strategy registration:

```csharp
services.AddPromptProcessingIntent("MyHeavyIntent", "description")
    .WithHeavyStrategy<MyHeavyStrategy>();
```

The default strategy provider filters these at runtime:

- If `PromptProcessingOptions.EnableHeavyProcessingStrategies` is `false` (default), heavy strategies are skipped and heavy intents are excluded from AI detection.
- If `true`, heavy strategies run normally.

Example heavy strategy:
- `RowLevelTabularAnalysisDocumentProcessingStrategy` (batch processes `.xlsx` / `.csv` row-by-row)

#### Processing Pipeline

The `DefaultPromptProcessingStrategyProvider` orchestrates the processing pipeline:

1. **Intent Detection** - The AI classifier selects an intent from all registered intents.
2. **Strategy Execution** - All registered `IPromptProcessingStrategy` implementations run in sequence. Each strategy self-filters by checking the detected intent.

#### Processing Result

The `IntentProcessingResult` is part of the `IntentProcessingContext` and allows multiple strategies to contribute:

- `AdditionalContexts` - List of context entries from all contributing strategies
- `GetCombinedContext()` - Gets the combined context from all strategies
- `HasContext` - Whether any strategy has contributed context
- `ToolNames` - List of AI tool names to register for the completion call
- `GeneratedImages` - Contains generated images when image generation intents are processed
- `HasGeneratedImages` - Whether any images were generated
- `IsImageGenerationIntent` - Whether the request was for image generation

### Built-in Strategies

**Document Processing Strategies:**
- `SummarizationDocumentProcessingStrategy` - Full document content for summarization
- `TabularAnalysisDocumentProcessingStrategy` - Structured data parsing for analysis
- `RowLevelTabularAnalysisDocumentProcessingStrategy` - Row-by-row tabular processing (heavy)
- `ExtractionDocumentProcessingStrategy` - Content for data extraction
- `ComparisonDocumentProcessingStrategy` - Multi-document content for comparison
- `TransformationDocumentProcessingStrategy` - Content for format transformation
- `GeneralReferenceDocumentProcessingStrategy` - General document reference

**Image/Chart Generation Strategies:**
- `ImageGenerationDocumentProcessingStrategy` - Generates images using AI image generation models
- `ChartGenerationDocumentProcessingStrategy` - Generates Chart.js configurations

**Second-Phase Strategies:**
- `McpCapabilitiesProcessingStrategy` - Resolves MCP server capabilities via a lightweight AI call

## Configuration

### `CrestApps_AI:Chat`

This module binds `PromptProcessingOptions` from the configuration section:

- Section: `CrestApps_AI:Chat`

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableHeavyProcessingStrategies` | `bool` | `false` | When `false`, strategies registered via `.WithHeavyStrategy<T>()` are skipped and heavy intents are excluded from AI detection. When `true`, heavy strategies and heavy intents are allowed. |

Example `appsettings.json`:

```json
{
  "CrestApps_AI": {
    "Chat": {
      "EnableHeavyProcessingStrategies": false
    }
  }
}
```

Notes:
- Row-level batching settings are configured separately (see the consuming module documentation).
- For caching, the system uses `IDistributedCache`. Configure a provider such as Redis or SQL Server for production.

## Usage

This project is a library consumed by the `AI Chat Interactions` module and its extension modules.

### Registering Services

```csharp
services
    .AddPromptRoutingServices()
    .AddDefaultPromptProcessingStrategies()
    .AddDefaultDocumentPromptProcessingStrategies();
```

### Adding Custom Intents and Strategies

Use the fluent builder pattern returned by `AddPromptProcessingIntent()`:

```csharp
// Register an intent with a strategy
services.AddPromptProcessingIntent("MyIntent", "Description for AI classifier")
    .WithStrategy<MyCustomStrategy>();

// Register a heavy intent with a strategy (filtered when heavy processing is disabled)
services.AddPromptProcessingIntent("MyHeavyIntent", "Description for AI classifier")
    .WithHeavyStrategy<MyHeavyStrategy>();

// Register an intent without a strategy (e.g., handled by an existing strategy)
services.AddPromptProcessingIntent("MyIntent", "Description for AI classifier");
```

### Builder Methods

| Method | Description |
|--------|-------------|
| `.WithStrategy<T>()` | Registers a strategy (`IPromptProcessingStrategy`). Runs on every request; strategies self-filter by intent. |
| `.WithHeavyStrategy<T>()` | Marks the intent as heavy and registers a strategy. Excluded from AI detection and strategy execution when `EnableHeavyProcessingStrategies` is `false`. |
| `.AsHeavy()` | Marks only the intent as heavy (without registering a strategy). Excluded from AI detection when `EnableHeavyProcessingStrategies` is `false`. |

### Implementing a Custom Strategy

```csharp
public class MyCustomStrategy : DocumentProcessingStrategyBase
{
    public override Task ProcessAsync(IntentProcessingContext context)
    {
        // Check if we should handle this intent
        if (!CanHandle(context, "MyIntent"))
        {
            return Task.CompletedTask;
        }

        // Add context to the result
        context.Result.AddContext(
            "Processed content",
            "Context prefix:",
            usedVectorSearch: false);

        return Task.CompletedTask;
    }
}

// Heavy strategy - registered via .WithHeavyStrategy<MyHeavyStrategy>()
public class MyHeavyStrategy : DocumentProcessingStrategyBase
{
    public override async Task ProcessAsync(IntentProcessingContext context)
    {
        if (!CanHandle(context, "MyHeavyIntent"))
        {
            return;
        }

        // This will only execute when EnableHeavyProcessingStrategies is true
        // ... expensive processing ...
    }
}
```

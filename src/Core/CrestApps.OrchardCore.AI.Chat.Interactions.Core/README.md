# AI Chat Interactions Core

Shared core types and helpers used by AI Chat Interactions modules.

## Purpose

- Defines index profile metadata and common constants
- Provides handlers and base classes for indexing and document integration
- Provides intent-aware, strategy-based document processing infrastructure

## Document Processing Architecture

This module provides an extensible architecture for processing documents in chat interactions based on detected user intent. Multiple strategies can contribute context to a single request.

### Key Components

#### Intent Detection

The `IDocumentIntentDetector` interface allows classification of user intent when documents are attached. Intents are string-based, allowing easy extensibility. Well-known intents are defined in `DocumentIntents`:

- `DocumentIntents.DocumentQnA` - Question answering over documents (RAG)
- `DocumentIntents.SummarizeDocument` - Document summarization
- `DocumentIntents.AnalyzeTabularData` - CSV/tabular data analysis
- `DocumentIntents.ExtractStructuredData` - Data extraction
- `DocumentIntents.CompareDocuments` - Document comparison
- `DocumentIntents.TransformFormat` - Content transformation/reformatting
- `DocumentIntents.GeneralChatWithReference` - General chat with document reference

#### Processing Strategies

The `IDocumentProcessingStrategy` interface enables custom document processing based on intent:

- All strategies are called in sequence for every request
- Each strategy decides internally whether to contribute context based on the intent
- Multiple strategies can add context to the same result
- Strategies can bypass vector search when appropriate
- Extensible via DI for custom strategies

#### Processing Result

The `DocumentProcessingResult` is part of the `DocumentProcessingContext` and allows multiple strategies to contribute:

- `AdditionalContexts` - List of context entries from all contributing strategies
- `GetCombinedContext()` - Gets the combined context from all strategies
- `HasContext` - Whether any strategy has contributed context

### Built-in Strategies

- `SummarizationDocumentProcessingStrategy` - Full document content for summarization
- `TabularAnalysisDocumentProcessingStrategy` - Structured data parsing for analysis
- `ExtractionDocumentProcessingStrategy` - Content for data extraction
- `ComparisonDocumentProcessingStrategy` - Multi-document content for comparison
- `TransformationDocumentProcessingStrategy` - Content for format transformation
- `GeneralReferenceDocumentProcessingStrategy` - General document reference

## Usage

This project is a library consumed by the `AI Chat Interactions` module and its extension modules.

### Registering Services

```csharp
services.AddDocumentProcessingServices()
    .AddDefaultDocumentProcessingStrategies();
```

### Adding Custom Strategies

```csharp
// Register a custom strategy - it decides internally which intents to handle
services.AddDocumentProcessingStrategy<MyCustomStrategy>();
```

### Implementing a Custom Strategy

```csharp
public class MyCustomStrategy : DocumentProcessingStrategyBase
{
    public override Task ProcessAsync(DocumentProcessingContext context)
    {
        // Check if we should handle this intent
        if (!string.Equals(context.IntentResult?.Intent, "MyCustomIntent", StringComparison.OrdinalIgnoreCase))
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
```

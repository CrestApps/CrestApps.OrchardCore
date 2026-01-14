# AI Chat Interactions Core

Shared core types and helpers used by AI Chat Interactions modules.

## Purpose

- Defines index profile metadata and common constants
- Provides handlers and base classes for indexing and document integration
- Provides intent-aware, strategy-based document processing infrastructure

## Document Processing Architecture

This module provides an extensible architecture for processing documents in chat interactions based on detected user intent.

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

- Strategies are called in sequence until one handles the request
- Each strategy decides internally whether to handle the intent by returning `Handled = true` or `Handled = false`
- Strategies can bypass vector search when appropriate
- Multiple strategies can potentially handle the same intent
- Extensible via DI for custom strategies

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
    public override Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context)
    {
        // Check if we should handle this intent
        if (!string.Equals(context.IntentResult?.Intent, "MyCustomIntent", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(DocumentProcessingResult.NotHandled());
        }

        // Process and return result
        return Task.FromResult(DocumentProcessingResult.Success(
            "Processed content",
            "Context prefix"));
    }
}
```

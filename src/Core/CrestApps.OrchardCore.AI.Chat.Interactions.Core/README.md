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

- Each strategy handles specific intents (string-based matching)
- Strategies can bypass vector search when appropriate
- Extensible via DI for custom strategies
- Fallback intent is configurable via `ChatInteractionDocumentOptions`

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
// Register a strategy with a custom intent
services.AddDocumentProcessingStrategy<MyCustomStrategy>("MyCustomIntent");

// Register a strategy for a well-known intent
services.AddDocumentProcessingStrategy<MyRagStrategy>(DocumentIntents.DocumentQnA);
```

### Configuring Fallback Intent

```csharp
services.Configure<ChatInteractionDocumentOptions>(options =>
{
    options.FallbackIntent = DocumentIntents.GeneralChatWithReference;
});
```

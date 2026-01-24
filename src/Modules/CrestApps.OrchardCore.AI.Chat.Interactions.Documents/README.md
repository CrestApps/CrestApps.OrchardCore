# AI Chat Interactions - Documents

This feature extends the AI Chat Interactions module with document upload and intelligent, intent-aware document processing capabilities, enabling users to chat against their own uploaded documents with optimized responses based on their intent.

## Features

- **Document Upload**: Upload PDF, Word, Excel, PowerPoint, and text-based documents
- **Drag and Drop**: Easy file upload via drag-and-drop or file browser
- **Text Extraction**: Automatic text extraction from uploaded documents
- **Intent Detection**: Automatic classification of user intent (Q&A, summarization, analysis, etc.)
- **Strategy-Based Processing**: Optimized document handling based on detected intent
- **Document Embedding**: Content is chunked and embedded for semantic search (RAG)
- **RAG Integration**: Relevant document chunks are retrieved and used as context for AI responses
- **Document Management**: View, manage, and remove uploaded documents

## Intent-Aware Document Processing

When documents are attached to a chat interaction, the system automatically detects the user's intent and routes the request to an appropriate processing strategy.

### Supported Intents

| Intent | Description | Example Prompts |
|--------|-------------|-----------------|
| **Document Q&A** | Question answering using RAG | "What does this document say about X?" |
| **Summarize** | Document summarization | "Summarize this document", "Give me a brief overview" |
| **Tabular Analysis** | CSV/Excel data analysis | "Calculate the total sales", "Show me the average" |
| **Extract Data** | Structured data extraction | "Extract all email addresses", "List all names" |
| **Compare Documents** | Multi-document comparison | "Compare these documents", "What are the differences?" |
| **Transform Format** | Content reformatting | "Convert to bullet points", "Make it a table" |
| **General Reference** | General chat with document context | Default fallback |

### Processing Strategies

Each intent is handled by a specialized strategy:

- **RAG Strategy**: Uses vector search to find relevant chunks (for Q&A)
- **Summarization Strategy**: Provides full document content (bypasses vector search)
- **Tabular Analysis Strategy**: Parses structured data for calculations
- **Extraction Strategy**: Focuses on content extraction
- **Comparison Strategy**: Provides multi-document content
- **Transformation Strategy**: Provides content for reformatting

### Benefits

- **More Accurate Responses**: AI receives context tailored to the user's actual intent
- **Lower Token Costs**: Avoids unnecessary vector search when not needed
- **Faster Processing**: Optimal strategy selection reduces overhead
- **Extensible**: Add custom intents and strategies via the plugin architecture

## Getting Started

### Prerequisites

1. **Indexing Provider**: This feature supports Elasticsearch and Azure AI Search as embedding/search providers. Enable the provider feature that matches your environment:
   - For Elasticsearch: enable the `Elasticsearch` feature
   - For Azure AI Search: enable the `OrchardCore.Search.AzureAI` (Azure AI Search) feature
2. **Create an Index**: Navigate to **Search > Indexing** and create a new index for storing user documents. You can name it anything you want.
3. **Configure Settings**: Navigate to **Settings > Chat Interaction** and select your new index as the default index for document embedding.

### Setup Steps

1. Enable the appropriate search feature in Orchard Core admin (Elasticsearch or Azure AI Search)
2. Navigate to **Search > Indexing** and add a new index
   - Give it a descriptive name (e.g., "ChatDocuments")
   - Select the provider (Elasticsearch or Azure AI Search) as the provider
3. Navigate to **Settings > Chat Interaction**
4. Select your new index as the default document index
5. Enable the `AI Chat Interactions - Documents` feature
6. Start using the Documents tab in your chat interactions!

## Supported Document Formats

| Format | Extension | Notes |
|--------|-----------|-------|
| PDF | .pdf | Requires `CrestApps.OrchardCore.AI.Chat.Interactions.Pdf` feature |
| Word | .docx | Requires `CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml` feature |
| Excel | .xlsx | Requires `CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml` feature |
| PowerPoint | .pptx | Requires `CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml` feature |
| Text | .txt | Built-in support |
| CSV | .csv | Built-in support |
| Markdown | .md | Built-in support |
| JSON | .json | Built-in support |
| XML | .xml | Built-in support |
| HTML | .html, .htm | Built-in support |
| YAML | .yml, .yaml | Built-in support |

> **Note**: Legacy Office formats (.doc, .xls, .ppt) are not supported. Please convert them to the newer formats (.docx, .xlsx, .pptx).

## Configuration

### Intent Detection Model

The AI intent detector uses a chat model to classify user intent. By default, it uses the same model configured for chat (`DefaultDeploymentName`). However, since intent detection is a simple classification task, you can configure a separate, cost-effective model specifically for intent detection.

To configure the intent detection model, set the `DefaultIntentDeploymentName` in your provider connection settings. This can be done via:

**Option 1: Admin UI**

Navigate to **Artificial Intelligence > Provider Connections**, edit your connection, and set the **Intent deployment name** field.

**Option 2: Configuration (appsettings.json)**

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "DefaultDeploymentName": "gpt-4o",
              "DefaultEmbeddingDeploymentName": "text-embedding-3-small",
              "DefaultIntentDeploymentName": "gpt-4o-mini",
              "DefaultImagesDeploymentName": "dall-e-3"
            }
          }
        }
      }
    }
  }
}
```

#### Deployment Name Settings

| Setting | Description | Required |
|---------|-------------|----------|
| `DefaultDeploymentName` | The default model for chat completions | Yes |
| `DefaultEmbeddingDeploymentName` | The model for generating embeddings (for RAG/vector search) | No |
| `DefaultIntentDeploymentName` | A lightweight model for intent classification (e.g., `gpt-4o-mini`) | No |
| `DefaultImagesDeploymentName` | The model for image generation (e.g., `dall-e-3`) | No |

> **Recommendation**: Use a lightweight, cost-effective model for intent detection such as `gpt-4o-mini`, `gpt-4.1-mini`, or `gpt-4.1-nano`. Intent classification is a simple task that doesn't require the full capabilities of larger models, and using a smaller model significantly reduces costs and improves response times.

If `DefaultIntentDeploymentName` is not configured, the system falls back to:
1. The `DefaultDeploymentName` (chat model) for the connection
2. Keyword-based intent detection (if no AI model is available)

### Documents Tab Settings

| Setting | Description | Default |
|---------|-------------|---------|
| Top N Results | Number of top matching document chunks to include as context | 3 |

### Site Settings

Navigate to **Settings > Chat Interaction** to configure:

| Setting | Description |
|---------|-------------|
| Index Profile | The index to use for document embedding and search |

## How It Works

1. **Upload**: User uploads a document through the Documents tab
2. **Extract**: Text is extracted from the document using the appropriate extractor
3. **Chunk**: Text is split into overlapping chunks (approximately 500 tokens each)
4. **Embed**: Each chunk is converted to a vector embedding using the AI provider
5. **Store**: Embeddings are stored in the chosen index with the session ID and document ID
6. **Search**: When chatting, the user's query is embedded and similar chunks are retrieved
7. **Context**: Top N matching chunks are added to the AI prompt as context

## Dependencies

This feature requires:
- `CrestApps.OrchardCore.AI.Chat.Interactions`
- `OrchardCore.Indexing`

For PDF support:
- `CrestApps.OrchardCore.AI.Chat.Interactions.Pdf`

For Office document support:
- `CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml`

For Azure AI Search support:
- `OrchardCore.Search.AzureAI` feature

## Permissions

| Permission | Description |
|------------|-------------|
| `ManageChatInteractionSettings` | Allows users to configure the document index settings |

## Extending Document Extraction

The document extraction system is extensible. To add support for additional file formats:

1. Implement the `IDocumentTextExtractor` interface
2. Register your implementation using `AddDocumentTextExtractor<T>()`

Example:
```csharp
services.AddDocumentTextExtractor<MyCustomExtractor>();
```

## Adding Custom Processing Strategies

To add a custom document processing strategy with a custom intent:

1. Register your intent using `AddDocumentIntent()` - this configures the AI intent detector to recognize your custom intent
2. Implement `IDocumentProcessingStrategy` or extend `DocumentProcessingStrategyBase`
3. Register your strategy using `AddDocumentProcessingStrategy<T>()`

> **Important**: Intents must be registered via `AddDocumentIntent()` to be recognized by the AI intent detector. If an intent is not registered, it will not be included in the AI classification prompt and your strategy will never be invoked.

### Example: Custom Strategy with Custom Intent

```csharp
public class MyCustomStrategy : DocumentProcessingStrategyBase
{
    public const string MyCustomIntent = "MyCustomIntent";

    public override Task ProcessAsync(DocumentProcessingContext context)
    {
        // Check if we should handle this intent
        if (!CanHandle(context, MyCustomIntent))
        {
            return Task.CompletedTask;
        }

        // Get combined text from all documents
        var documentText = GetCombinedDocumentText(context, maxLength: 50000);

        // Add context to the result
        context.Result.AddContext(
            documentText,
            "Custom context from documents:",
            usedVectorSearch: false);

        return Task.CompletedTask;
    }
}
```

### Registering in Startup

```csharp
public override void ConfigureServices(IServiceCollection services)
{
    // Register the custom intent with a description for the AI classifier
    services.AddDocumentIntent(
        MyCustomStrategy.MyCustomIntent,
        "The user wants to perform a custom operation on the documents, such as [describe when this intent applies].");

    // Register the strategy
    services.AddDocumentProcessingStrategy<MyCustomStrategy>();
}
```

### How It Works

1. **Intent Registration**: `AddDocumentIntent()` adds your intent and description to `DocumentProcessingOptions.Intents`
2. **AI Classification**: The AI intent detector dynamically builds its classification prompt from all registered intents
3. **Intent Detection**: When a user sends a message, the AI classifies their intent based on the registered descriptions
4. **Strategy Invocation**: Your strategy's `ProcessAsync` method is called, and it checks if the detected intent matches

### Multiple Strategies

All registered strategies are called in sequence. Each strategy decides whether to handle the request based on the detected intent. Multiple strategies can contribute context to the same request.

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/ai/chat-interactions/upload-document` | POST | Upload one or more documents |
| `/ai/chat-interactions/remove-document` | POST | Remove a document |

## Troubleshooting

### "Index Not Configured" Warning

If you see this warning, navigate to **Settings > Chat Interaction** and select an index profile.

### "Embedding Search Service Not Available" Warning

This means the configured index profile doesn't have a registered embedding/search service. Supported providers include Elasticsearch and Azure AI Search. Make sure:
1. The corresponding feature is enabled (Elasticsearch or Azure AI Search)
2. Your index is configured to use a supported provider

### Documents Not Being Used in Chat

Check that:
1. An index profile is configured in settings
2. The index profile uses a supported provider (Elasticsearch or Azure AI Search)
3. Documents have been successfully uploaded (check for errors in the Documents tab)

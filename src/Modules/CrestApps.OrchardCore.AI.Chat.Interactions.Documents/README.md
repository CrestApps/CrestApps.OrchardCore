# AI Chat Interactions - Documents

This feature extends `CrestApps.OrchardCore.AI.Chat.Interactions` with document upload and additional prompt-processing strategies for document-aware conversations.

Key point: **prompt routing + intent detection are provided by the base `CrestApps.OrchardCore.AI.Chat.Interactions` module**. The Documents feature *adds more prompt processing strategies* (and registers additional intents) focused on documents (RAG and non-RAG).

## Features

- **Document Upload**: Upload PDF, Word, Excel, PowerPoint, and text-based documents
- **Drag and Drop**: Easy file upload via drag-and-drop or file browser
- **Text Extraction**: Automatic text extraction from uploaded documents
- **Document Intent Registration**: Registers document intents/descriptions for the shared intent detector
- **Strategy-Based Processing**: Adds document-focused prompt-processing strategies
- **Document Embedding**: Content is chunked and embedded for semantic search (RAG)
- **RAG Integration**: Relevant document chunks are retrieved and used as context for AI responses
- **Document Management**: View, manage, and remove uploaded documents

## Intent-Aware Document Processing

When documents are attached to a chat interaction, the shared prompt routing pipeline detects the user's intent and invokes the registered strategies.

### Supported Document Intents

| Intent | Description | Example Prompts |
|--------|-------------|-----------------|
| `DocumentQnA` | Question answering using RAG | "What does this document say about X?" |
| `SummarizeDocument` | Document summarization | "Summarize this document", "Give me a brief overview" |
| `AnalyzeTabularData` | CSV/Excel data analysis | "Calculate the total sales", "Show me the average" |
| `ExtractStructuredData` | Structured data extraction | "Extract all email addresses", "List all names" |
| `CompareDocuments` | Multi-document comparison | "Compare these documents", "What are the differences?" |
| `TransformFormat` | Content reformatting | "Convert to bullet points", "Make it a table" |
| `GeneralChatWithReference` | General chat using document context | Default fallback |

### Processing Strategies

Each intent is handled by a specialized strategy (registered into the shared prompt routing pipeline):

- **RAG Strategy**: Uses vector search to find relevant chunks (for `DocumentQnA`)
- **Summarization Strategy**: Provides full document content (bypasses vector search)
- **Tabular Analysis Strategy**: Parses structured data for calculations
- **Extraction Strategy**: Focuses on content extraction
- **Comparison Strategy**: Provides multi-document content
- **Transformation Strategy**: Provides content for reformatting
- **General Reference Strategy**: Provides context when asking general questions that reference documents

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

> Note: Legacy Office formats (.doc, .xls, .ppt) are not supported. Please convert them to the newer formats (.docx, .xlsx, .pptx).

## Configuration

### Intent Detection Model

Intent detection is provided by the base `CrestApps.OrchardCore.AI.Chat.Interactions` module and is shared across all registered intents (including those added by this Documents feature).

To configure the intent detection model, set the `DefaultIntentDeploymentName` in your provider connection settings.

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

If `DefaultIntentDeploymentName` is not configured, the system falls back to:
1. The `DefaultDeploymentName` (chat model) for the connection
2. Keyword-based intent detection (if no AI model is available)

### Documents Tab Settings

| Setting | Description | Default |
|---------|-------------|---------|
| Top N Results | Number of top matching document chunks to include as context | 3 |

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

## Adding Custom Processing Strategies

To add a custom document processing strategy with a custom intent:

1. Register your intent using `AddPromptProcessingIntent()`
2. Implement `IPromptProcessingStrategy`
3. Register your strategy using `AddPromptProcessingStrategy<T>()`

Important: Intents must be registered via `AddPromptProcessingIntent()` to be recognized by the AI intent detector. If an intent is not registered, it will not be included in the AI classification prompt and your strategy will never be invoked.

### Registering in Startup (example)

```csharp
public override void ConfigureServices(IServiceCollection services)
{
    services.AddPromptProcessingIntent(
        "MyCustomDocumentIntent",
        "The user wants to perform a custom operation on the documents, such as [describe when this intent applies].");

    services.AddPromptProcessingStrategy<MyCustomDocumentStrategy>();
}
```

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

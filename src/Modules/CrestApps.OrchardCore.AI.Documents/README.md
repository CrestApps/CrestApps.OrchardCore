# AI Documents

> 📖 **Full documentation is available at [orchardcore.crestapps.com](https://orchardcore.crestapps.com/docs/ai/documents).**

This module is the foundation for all document-related functionality in the CrestApps AI suite. It provides document upload, text extraction, embedding, and RAG (Retrieval-Augmented Generation) capabilities shared by both **AI Chat Interactions** and **AI Profiles**.

## Module Features

This module contains three features that work together:

| Feature | ID | Description |
|---------|-----|-------------|
| **AI Documents** (base) | `CrestApps.OrchardCore.AI.Documents` | Shared text extraction, settings, admin menu, document processing tools, and RAG search. Enabled automatically by dependency. |
| **AI Chat Interaction Documents** | `CrestApps.OrchardCore.AI.Documents.ChatInteractions` | Document upload and RAG support for ad-hoc AI Chat Interactions. |
| **AI Profile Documents** | `CrestApps.OrchardCore.AI.Documents.Profiles` | Document upload and RAG support for AI Profiles. |

The base feature is **enabled by dependency only** — it activates automatically when either `AI Chat Interaction Documents` or `AI Profile Documents` is enabled.

## Base Feature: AI Documents

The base feature (`CrestApps.OrchardCore.AI.Documents`) provides the shared infrastructure used by both chat interaction and profile document features:

- **Unified Document Store**: A single `IAIDocumentStore` for storing and querying documents across all reference types (chat interactions, profiles)
- **Text Extraction**: Automatic text extraction from uploaded documents via registered extractors
- **Settings UI**: Admin settings page for configuring the default document index (**Settings > Chat Interaction**)
- **Document Processing Tools**: AI tools for listing, reading, and searching documents
- **RAG Search Tool**: Semantic vector search across uploaded documents
- **Strategy-Based Processing**: Adds document-focused prompt-processing strategies
- **Index & Migrations**: Shared `AIDocumentIndex` with `ReferenceId` and `ReferenceType` columns for multi-purpose document storage

### Dependencies

- `CrestApps.OrchardCore.AI.Chat.Interactions`
- `OrchardCore.Indexing`

## AI Chat Interaction Documents Feature

The **AI Chat Interaction Documents** feature (`CrestApps.OrchardCore.AI.Documents.ChatInteractions`) adds document upload and RAG support to ad-hoc AI Chat Interactions. When enabled, a **Documents** tab appears in the chat interaction UI, allowing users to upload documents and chat against their own data.

Documents uploaded to a chat interaction are **scoped to that session**.

### Key Capabilities

- **Document Upload**: Upload documents via drag-and-drop or file browser
- **Text Extraction**: Content is automatically extracted from uploaded documents
- **Chunking & Embedding**: Text is split into chunks and embedded for semantic vector search
- **RAG Integration**: Relevant document chunks are retrieved and used as context for AI responses
- **Document Management**: View, manage, and remove uploaded documents within a chat session

### Intent-Aware Document Processing

When documents are attached to a chat interaction, the shared prompt routing pipeline detects the user's intent and invokes the registered strategies.

#### Supported Document Intents

| Intent | Description | Example Prompts |
|--------|-------------|-----------------|
| `DocumentQnA` | Question answering using RAG | "What does this document say about X?" |
| `SummarizeDocument` | Document summarization | "Summarize this document", "Give me a brief overview" |
| `AnalyzeTabularData` | CSV/Excel data analysis | "Calculate the total sales", "Show me the average" |
| `ExtractStructuredData` | Structured data extraction | "Extract all email addresses", "List all names" |
| `CompareDocuments` | Multi-document comparison | "Compare these documents", "What are the differences?" |
| `TransformFormat` | Content reformatting | "Convert to bullet points", "Make it a table" |
| `GeneralChatWithReference` | General chat using document context | Default fallback |

#### Processing Strategies

Each intent is handled by a specialized strategy:

- **RAG Strategy**: Uses vector search to find relevant chunks (for `DocumentQnA`)
- **Summarization Strategy**: Provides full document content (bypasses vector search)
- **Tabular Analysis Strategy**: Parses structured data for calculations
- **Extraction Strategy**: Focuses on content extraction
- **Comparison Strategy**: Provides multi-document content
- **Transformation Strategy**: Provides content for reformatting
- **General Reference Strategy**: Provides context when asking general questions that reference documents

### API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/ai/chat-interactions/upload-document` | POST | Upload one or more documents to a chat interaction |
| `/ai/chat-interactions/remove-document` | POST | Remove a document from a chat interaction |

### Dependencies

- `CrestApps.OrchardCore.AI.Documents` (base — enabled automatically)

### Getting Started

1. **Set up an indexing provider**: Enable Elasticsearch or Azure AI Search in the Orchard Core admin.
2. **Create an index**: Navigate to **Search > Indexing** and create a new index (e.g., "ChatDocuments").
3. **Configure settings**: Navigate to **Settings > Chat Interaction** and select your new index.
4. **Enable the feature**: Enable `AI Chat Interaction Documents` in the admin dashboard.
5. Start using the Documents tab in your chat interactions.

## AI Profile Documents Feature

The **AI Profile Documents** feature (`CrestApps.OrchardCore.AI.Documents.Profiles`) adds document upload and RAG support to AI Profiles. When enabled, a **Documents** tab appears on the AI Profile editor, allowing administrators to attach text-based documents that will be chunked, embedded, and used as context across all chat sessions using that profile.

Unlike chat interaction documents (which are scoped to a single session), profile documents **persist across all sessions** using the profile.

### Key Capabilities

- **Document Upload**: Upload text-based documents (PDF, Word, Markdown, etc.) directly to an AI Profile
- **Automatic Text Extraction**: Content is extracted from uploaded documents using registered text extractors
- **Chunking & Embedding**: Extracted text is split into chunks and embedded for semantic vector search
- **RAG Integration**: Relevant document chunks are automatically retrieved and used as context for AI responses
- **Top N Configuration**: Control how many matching chunks are included as context (default: 3)

### Supported File Types

Only embeddable file extensions are supported for AI Profile documents. The set of embeddable extensions is determined by the registered document text extractors. Typically, this includes:

| Format | Extension | Module Required |
|--------|-----------|-----------------|
| Text | .txt | Built-in |
| Markdown | .md | Built-in |
| JSON | .json | Built-in |
| XML | .xml | Built-in |
| HTML | .html, .htm | Built-in |
| YAML | .yml, .yaml | Built-in |
| Log | .log | Built-in |
| PDF | .pdf | `CrestApps.OrchardCore.AI.Documents.Pdf` |
| Word | .docx | `CrestApps.OrchardCore.AI.Documents.OpenXml` |
| PowerPoint | .pptx | `CrestApps.OrchardCore.AI.Documents.OpenXml` |

> **Note:** Tabular file types (`.csv`, `.tsv`, `.xlsx`, `.xls`) are registered as non-embeddable and are not available for AI Profile document upload, since they are intended for tabular data analysis rather than text-based RAG.

### How It Works

Documents are managed directly through the AI Profile editor form. When you save a profile:

1. **New files** selected in the Documents tab are uploaded, text-extracted, chunked, embedded, and stored
2. **Removed documents** marked for deletion are removed from the store
3. All changes are applied atomically when the profile is saved

There are no separate API endpoints for profile document management — everything is handled through the standard profile editor workflow.

### Dependencies

- `CrestApps.OrchardCore.AI.Documents` (base — enabled automatically)
- `CrestApps.OrchardCore.AI.Chat.Core`

### Getting Started

1. Enable the `AI Profile Documents` feature in the Orchard Core admin dashboard.
2. Navigate to **Artificial Intelligence > AI Profiles** and edit a profile.
3. Use the **Documents** tab to upload text-based documents.
4. Configure the **Top N Results** setting to control how many matching chunks are included as context.

## Supported Document Formats

| Format | Extension | Notes |
|--------|-----------|-------|
| PDF | .pdf | Requires `CrestApps.OrchardCore.AI.Documents.Pdf` feature |
| Word | .docx | Requires `CrestApps.OrchardCore.AI.Documents.OpenXml` feature |
| Excel | .xlsx | Requires `CrestApps.OrchardCore.AI.Documents.OpenXml` feature |
| PowerPoint | .pptx | Requires `CrestApps.OrchardCore.AI.Documents.OpenXml` feature |
| Text | .txt | Built-in support |
| CSV | .csv | Built-in support |
| Markdown | .md | Built-in support |
| JSON | .json | Built-in support |
| XML | .xml | Built-in support |
| HTML | .html, .htm | Built-in support |
| YAML | .yml, .yaml | Built-in support |

> Note: Legacy Office formats (.doc, .xls, .ppt) are not supported. Please convert them to the newer formats (.docx, .xlsx, .pptx).

## Configuration

### Documents Tab Settings

| Setting | Description | Default |
|---------|-------------|---------|
| Top N Results | Number of top matching document chunks to include as context | 3 |

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

## Troubleshooting

### "Index Not Configured" Warning

If you see this warning, navigate to **Settings > Chat Interaction** and select an index profile.

### "Embedding Search Service Not Available" Warning

This means the configured index profile doesn't have a registered embedding/search service. Supported providers include Elasticsearch and Azure AI Search. Make sure:
1. The corresponding feature is enabled (Elasticsearch or Azure AI Search)
2. Your index is configured to use a supported provider

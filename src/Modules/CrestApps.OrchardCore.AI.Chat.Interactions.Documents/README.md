# AI Chat Interactions - Documents

This feature extends the AI Chat Interactions module with document upload and RAG (Retrieval Augmented Generation) capabilities, enabling users to chat against their own uploaded documents.

## Features

- **Document Upload**: Upload PDF, Word, Excel, PowerPoint, and text-based documents
- **Drag and Drop**: Easy file upload via drag-and-drop or file browser
- **Text Extraction**: Automatic text extraction from uploaded documents
- **Document Embedding**: Content is chunked and embedded for semantic search
- **RAG Integration**: Relevant document chunks are retrieved and used as context for AI responses
- **Document Management**: View, manage, and remove uploaded documents

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

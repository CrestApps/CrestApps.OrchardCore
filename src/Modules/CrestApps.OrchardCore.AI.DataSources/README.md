# AI Data Sources (`CrestApps.OrchardCore.AI.DataSources`)

## Overview

This module provides AI data source management, knowledge base (KB) indexing, and Retrieval-Augmented Generation (RAG) search capabilities for Orchard Core. It enables any AI provider and orchestrator to leverage structured data sources for contextual AI responses.

## Features

- **Data Source Management** — Create and manage AI data sources that connect to search indexes (Elasticsearch, Azure AI Search).
- **Knowledge Base Indexing** — Automatically chunks, embeds, and indexes source documents into a master AI Knowledge Base index for efficient vector search.
- **RAG Search Tool** — An AI tool (`DataSourceSearchTool`) that performs vector search against the KB index and injects relevant context into AI conversations.
- **Early (Preemptive) RAG** — Optionally pre-fetches relevant context before AI completion to reduce latency and improve response quality.
- **Real-Time Sync** — Automatically updates the KB index when source content changes via content event handlers.
- **Background Sync** — Periodic background tasks keep the KB index aligned with source data.
- **OData Filtering** — Supports OData filter expressions translated to provider-specific queries for precise data retrieval.
- **Deployment & Recipe Support** — Export and import data source configurations via Orchard Core deployment plans and recipes.

## How It Works

> Note: The `CrestApps.OrchardCore.AI.DataSources` feature is dependency-driven and is not intended to be enabled manually. Enable one of the provider modules instead (Elasticsearch, Azure AI Search), and it will bring in the base feature automatically.

1. **Create a Knowledge Base Index** — In the admin menu go to **Search > Indexing**, click **Add Index**, then select one of:
   - **AI Knowledge Base Index (Elasticsearch)**
   - **AI Knowledge Base Index (Azure AI Search)**
2. **Add a Data Source** — Configure a data source that maps a source index to the KB index, specifying key/title/content field mappings.
3. **Automatic Indexing** — Documents from the source index are chunked, embedded, and stored in the KB index for efficient retrieval.
4. **AI Integration** — Attach data sources to AI profiles or chat interactions. The RAG tool searches the KB index and provides relevant context to the AI model.

## Dependencies

- `CrestApps.OrchardCore.AI` — Core AI services and profile management.
- A provider-specific data source module for your search backend:
  - `CrestApps.OrchardCore.AI.DataSources.Elasticsearch`
  - `CrestApps.OrchardCore.AI.DataSources.AzureAI`

## Configuration

### Site Settings

Navigate to **Settings > Artificial Intelligence** to configure global data source defaults:

- **Top N Documents** — Default number of documents to retrieve (1–50).
- **Strictness** — Default strictness level for search relevance (1–5).
- **Enable Preemptive RAG** — When enabled, context is pre-fetched before AI completion for reduced latency.

### Data Source Settings

Each data source can be configured with:

- **Source Index** — The search index to pull documents from.
- **Knowledge Base Index** — The AI KB index where chunked embeddings are stored.
- **Title Field** — Maps to the document title in search results.
- **Content Field** — Maps to the main text content for chunking and embedding.
- **Key Field** — Maps to the document reference ID for citations.
- **Filters** — OData filter expressions for scoping search results.

## Recipes

### Creating a Data Source

```json
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "DisplayText": "Articles",
          "SourceIndexProfileName": "articles",
          "AIKnowledgeBaseIndexProfileName": "AIRagKnowledgeBase",
          "KeyFieldName": "ContentItemId",
          "TitleFieldName": "Content.ContentItem.DisplayText.Analyzed",
          "ContentFieldName": "Content.ContentItem.FullText"
        }
      ]
    }
  ]
}
```

## Provider Modules

The base module provides the management UI and orchestration handlers; provider modules add index + search implementations:

- `CrestApps.OrchardCore.AI.DataSources.Elasticsearch`
- `CrestApps.OrchardCore.AI.DataSources.AzureAI`

## Keeping the AI KB index in sync (custom Index Profiles)

When your data source points to Orchard Core's built-in **Content** indexes, the module keeps the AI Knowledge Base (KB) index synchronized automatically by listening to content events and triggering incremental re-indexing.

This behavior is implemented in:

- `Handlers\DataSourceContentHandler.cs`

If you are using a **custom Index Profile** that is updated by something other than Orchard Core content events (e.g. external data ingested directly into Elasticsearch/Azure AI Search, or a custom indexing pipeline), you must ensure that the AI KB index is updated when the source index changes.

### Options

1. **Trigger a manual sync** from the Data Sources UI (Sync action), or run a scheduled background sync.
2. **Implement a custom event handler** in your module (recommended for near real-time): detect source changes (whatever "source of truth" events you have), then call `DataSourceIndexingService` to upsert/remove the affected documents.

### Incremental sync API

Use these methods to keep the AI KB index aligned:

- `DataSourceIndexingService.IndexDocumentsAsync(IEnumerable<string> documentIds)`
- `DataSourceIndexingService.RemoveDocumentsAsync(IEnumerable<string> documentIds)`

The IDs passed must match your data source's configured **Key Field** (e.g. `_id`, `ContentItemId`, etc.).


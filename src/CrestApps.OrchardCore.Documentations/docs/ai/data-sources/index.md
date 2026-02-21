---
sidebar_label: Overview
sidebar_position: 1
title: AI Data Sources
description: AI data source management, knowledge base indexing, and RAG search capabilities for Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | AI Data Sources |
| **Feature ID** | `CrestApps.OrchardCore.AI.DataSources` |

Provides AI data source management, knowledge base indexing, and RAG search capabilities.

> This feature is enabled automatically when you enable one of the provider modules (Elasticsearch, Azure AI Search).

## Overview

This module provides AI data source management, knowledge base (KB) indexing, and Retrieval-Augmented Generation (RAG) search capabilities for Orchard Core. It enables any AI provider and orchestrator to leverage structured data sources for contextual AI responses.

- **Data Source Management** — Create and manage AI data sources that connect to search indexes (Elasticsearch, Azure AI Search).
- **Knowledge Base Indexing** — Automatically chunks, embeds, and indexes source documents into a master AI Knowledge Base index for efficient vector search.
- **RAG Search Tool** — An AI tool (`DataSourceSearchTool`) that performs vector search against the KB index and injects relevant context into AI conversations.
- **Early (Preemptive) RAG** — Optionally pre-fetches relevant context before AI completion to reduce latency and improve response quality.
- **Real-Time Sync** — Automatically updates the KB index when source content changes via content event handlers.
- **Background Sync** — Periodic background tasks keep the KB index aligned with source data.
- **OData Filtering** — Supports OData filter expressions translated to provider-specific queries for precise data retrieval.
- **Deployment & Recipe Support** — Export and import data source configurations via Orchard Core deployment plans and recipes.

## Getting Started

1. **Create a Knowledge Base Index** — In the admin menu go to **Search > Indexing**, click **Add Index**, then select one of:
   - **AI Knowledge Base Index (Elasticsearch)**
   - **AI Knowledge Base Index (Azure AI Search)**
2. **Add a Data Source** — Configure a data source that maps a source index to the KB index, specifying key/title/content field mappings.
3. **Automatic Indexing** — Documents from the source index are chunked, embedded, and stored in the KB index for efficient retrieval.
4. **AI Integration** — Attach data sources to AI profiles or chat interactions. The RAG tool searches the KB index and provides relevant context to the AI model.

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

Enable one of the following provider modules to get started:

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

## Migrating from v1 to v2

Version 2 introduces **Knowledge Base (KB) indexing** with vector embeddings. During the migration, an "AI Knowledge Base Warehouse" index is automatically created for existing data sources. However, **you must configure an embedding connection** for the index to populate correctly.

### What changes

- Each data source now requires a **Knowledge Base Index** that stores chunked document embeddings for vector search.
- The migration creates the KB index automatically, but it needs an **embedding deployment** (e.g. `text-embedding-ada-002`, `text-embedding-3-small`) to generate embeddings.

### Required: Configure an embedding connection

If you have not already configured an AI provider connection with an embedding deployment, the KB index will be created **without embedding support**. This means the AI Knowledge Base index will have no data to feed the AI models when a data source is selected.

To fix this:

1. **Configure an AI provider connection with an embedding deployment:**
   - Navigate to **Artificial Intelligence > Connections** in the admin dashboard.
   - Edit or create a connection (e.g. Azure OpenAI, OpenAI).
   - Set the **Embedding Deployment Name** field to your embedding model deployment (e.g. `text-embedding-ada-002` or `text-embedding-3-small`).
   - Save the connection.

2. **Update the AI Knowledge Base Warehouse index:**
   - Navigate to **Search > Indexing** in the admin dashboard.
   - Find the **AI Knowledge Base Warehouse** index.
   - Edit it and select the embedding connection you configured in step 1.
   - Save the index.

3. **Trigger a sync:**
   - Go to **Artificial Intelligence > Data Sources**.
   - Click **Sync** on each data source to re-index documents with embeddings.

> **Note:** Without an embedding connection, data sources will appear configured but the KB index will remain empty. AI profiles using these data sources will not have any context documents to enhance their responses.

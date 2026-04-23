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
- **Real-Time Sync** — Automatically queues incremental KB updates when source content changes or when provider indexes are updated through Orchard Core indexing.
- **Background Sync** — CrestApps.OrchardCore defers incremental sync work until the current request completes, then hands it to an Orchard background job through the Orchard-specific indexing service adapter so the KB index stays aligned without blocking the UI. A nightly alignment task still repairs drift.
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

## Knowledge Source Behavior

When a data source (or uploaded documents) is attached to an AI profile or chat interaction, the system injects contextual instructions into the AI model's system prompt. The behavior depends on two settings:

### Preemptive RAG (Early Retrieval)

When **Enable Preemptive RAG** is on, the system automatically searches the knowledge base **before** the model generates a response. The retrieved context is injected directly into the system prompt so the model can use it immediately.

When preemptive RAG is off but the data source is still attached, the system injects instructions telling the model to **call search tools** (e.g., `search_data_source`, `search_documents`) before answering. This gives the model the ability to search on demand instead of receiving pre-fetched context.

### IsInScope ("Limit Responses to Indexed Data")

| Preemptive RAG | IsInScope | Behavior |
| --- | --- | --- |
| On | On | Context injected. Model MUST only use provided context. No general knowledge. |
| On | Off | Context injected. Model uses context as primary source but may supplement with general knowledge. |
| Off | On | No pre-fetched context. Model MUST call search tools first. No general knowledge allowed. |
| Off | Off | No pre-fetched context. Model MUST call search tools first, then may use general knowledge if no results found. |

### Instruction Style

All instructions injected into the system prompt use a consistent bracket-header format:

- `[Data Source Context]` — Preemptive RAG context from data sources
- `[Uploaded Document Context]` — Preemptive RAG context from documents
- `[Knowledge Source Instructions]` — Tool search directives (when preemptive RAG is off)
- `[Scope Constraint]` — IsInScope enforcement (when no references found)
- `[Response Guidelines]` — General guidance for using context with fallback to general knowledge
- `[Rules]` — Numbered rules for utility prompts (chart generation, data extraction, etc.)

This consistent format helps models identify and follow section-specific instructions reliably across providers.

## Citation & Reference Tracking

When the AI model uses content from a data source, the system produces `[doc:N]` citation markers in the response text. Each marker maps to a reference with:

- **ReferenceId** — The source document key (e.g., a content item ID).
- **ReferenceType** — The type of the source index (e.g., the index profile type). This determines how links are generated for the reference.
- **Title** — The document title from the source index.

References are collected from both **preemptive RAG** (context injected before AI completion) and **tool-based search** (the `SearchDataSources` tool invoked by the AI model during conversation).

All reference indices are coordinated through a shared counter on `AIInvocationScope.Current`, ensuring that `[doc:N]` indices are unique across data source references, document references, and tool-invoked searches within the same request. See [AI Tools — Invocation Context](../tools.md#invocation-context-aiinvocationscope) for details.

### Custom Link Resolvers

By default, references are shown without links unless a link resolver is registered for the reference type. To generate links for a custom reference type, implement `IAIReferenceLinkResolver` and register it as a keyed service:

```csharp
services.AddKeyedScoped<IAIReferenceLinkResolver, MyCustomLinkResolver>("MyIndexType");
```

The resolver receives the `referenceId` and optional metadata, and returns a URL string (or `null` for no link).

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

## Keeping the AI KB index in sync

When your data source points to Orchard Core's built-in **Content** indexes, the module keeps the AI Knowledge Base (KB) index synchronized automatically by listening to content events and calling the shared `IAIDataSourceIndexingService`.

When the source index is updated directly in Elasticsearch or Azure AI Search, Orchard Core now raises `IDocumentIndexHandler` notifications after successful upserts and deletes. CrestApps.OrchardCore bridges those notifications into the shared CrestApps.Core `IAIDataSourceIndexingQueue`, so mapped knowledge-base indexes are refreshed automatically without custom provider-specific observer code.

This means the default synchronization flow is:

1. A source document is added, updated, or deleted through Orchard Core indexing.
2. The Orchard bridge queues a targeted data-source sync through `IAIDataSourceIndexingQueue`.
3. The Orchard queue batches the work for the current request and schedules `HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(...)` from a deferred task.
4. The background job resolves the Orchard-specific `IAIDataSourceIndexingService` implementation, reindexes the mapped knowledge-base documents, and the nightly alignment task still repairs any drift that remains.

Synchronizing a **source** index profile from **Search > Indexing** also queues a full re-sync for every AI data source whose **Source Index** matches that profile. This is the expected recovery path when you rebuild Orchard's built-in **Content** index and want the mapped `ai_knowledge_base_warehouse` documents regenerated from the rebuilt source index.

For manual recovery or one-off reprocessing of a single mapping, use the **Sync** action in the Data Sources admin UI.

### Deletion cleanup

When a data source is deleted (via the admin UI or programmatically), all of its document chunks are automatically removed from the master knowledge base index. The system uses `IDataSourceVectorSearchService.DeleteByDataSourceIdAsync`, which leverages provider-native capabilities (Elasticsearch `DeleteByQuery`, Azure AI Search filter+batch delete) for efficient bulk removal. Cleanup is queued asynchronously so it does not block the admin UI.

When a content item is removed from a source index, the `DataSourceContentHandler` automatically removes its chunks from the KB index in real-time via a deferred task.

## Knowledge base indexing requirements

Each data source requires a **Knowledge Base Index** that stores chunked document embeddings for vector search.

To populate the index correctly:

1. **Configure an AI provider connection with an embedding deployment:**
   - Navigate to **Artificial Intelligence > Connections** in the admin dashboard.
   - Edit or create a connection (for example Azure OpenAI or OpenAI).
   - Set the embedding deployment to your embedding model (for example `text-embedding-ada-002` or `text-embedding-3-small`).
   - Save the connection.

2. **Configure the AI Knowledge Base Warehouse index:**
   - Navigate to **Search > Indexing** in the admin dashboard.
   - Find the **AI Knowledge Base Warehouse** index.
   - Edit it and select the embedding connection.
   - Save the index.

3. **Trigger a sync:**
   - Go to **Artificial Intelligence > Data Sources**.
   - Click **Sync** on each data source to index documents with embeddings.

:::note
Without an embedding connection, the AI Knowledge Base index remains empty and AI profiles using these data sources do not receive contextual documents.
:::

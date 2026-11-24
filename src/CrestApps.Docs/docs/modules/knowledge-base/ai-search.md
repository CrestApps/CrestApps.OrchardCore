---
sidebar_label: AI Search
title: AI Answers from the Knowledge Base
description: "The setup behind an AI assistant that answers from Knowledge Base articles: the Elasticsearch connections, the content index, the AI knowledge base index and data source, how restricted articles are kept out, and how citations link to articles."
user_manual: user-manual/knowledge-base/ai-search
---

Knowledge Base articles are content items, so the [AI data sources](../../ai/data-sources) pipeline turns them into a retrieval (RAG) knowledge base without code. The User Manual walks through every screen in [Let AI answer from your Knowledge Base](../../user-manual/knowledge-base/ai-search.md); this page covers what is behind it.

## Features

| Feature | Id | For |
| --- | --- | --- |
| Knowledge Base | `CrestApps.OrchardCore.KnowledgeBase` | The articles |
| Elasticsearch | `OrchardCore.Elasticsearch` | The content index of the `KnowledgeArticle` type; enabled with AI Data Sources - Elasticsearch |
| AI Data Sources - Elasticsearch | `CrestApps.OrchardCore.AI.DataSources.Elasticsearch` | The **AI Knowledge Base Index (Elasticsearch)** type and the **Search Index Profile** data source; enables AI Data Sources and Indexing |
| AI Chat | `CrestApps.OrchardCore.AI.Chat` | Admin chat and the **Artificial Intelligence Chat** widget |
| Widgets, Layers | `OrchardCore.Widgets`, `OrchardCore.Layers` | Placing the widget; the Knowledge Base recipe enables them |

AI knowledge base indexes exist for Elasticsearch and Azure AI Search (`CrestApps.OrchardCore.AI.DataSources.AzureAI`); Lucene has none. The AI connection needs a chat deployment and an embedding deployment ([AI providers](../../ai/providers/index.md)).

## Elasticsearch connections

Two configuration sections point at the server: Orchard Core's `OrchardCore_Elasticsearch` for the content index, and the shared `CrestApps:Elasticsearch` section for the AI data sources (see [AI Data Sources - Elasticsearch](../../ai/data-sources/elasticsearch.md#global-connection)).

```json
{
  "OrchardCore": {
    "OrchardCore_Elasticsearch": {
      "ConnectionType": "SingleNodeConnectionPool",
      "Url": "http://localhost",
      "Ports": [ 9200 ],
      "AuthenticationType": "Basic",
      "Username": "elastic",
      "Password": "elasticsearch"
    },
    "CrestApps": {
      "Elasticsearch": {
        "Url": "http://localhost:9200",
        "AuthenticationType": "Basic",
        "Username": "elastic",
        "Password": "elasticsearch"
      }
    }
  }
}
```

The Aspire host (`src/Startup/CrestApps.Aspire.AppHost`) runs Elasticsearch on port 9200 (user `elastic`, password parameter `ElasticsearchPassword`, `elasticsearch` by default) and sets both sections for the web project through `OrchardCore__OrchardCore_Elasticsearch__*` and `OrchardCore__CrestApps__Elasticsearch__*` environment variables.

## Indexes and data source

| Piece | Settings that matter |
| --- | --- |
| Content index (**Content in Elasticsearch**) | Content types: `KnowledgeArticle` only. **Index Latest version** off, so drafts stay out. **Store Source Data** on: `OrchardCoreElasticsearchDataSourceDocumentReader` reads each hit's `_source` and skips hits without one. |
| Article full text | The type's full-text settings: display text and body parts by default. Adding `{{ Model.Content.KnowledgeArticlePart.Summary }}` as the custom full-text template puts the summary in `Content.ContentItem.FullText` too. |
| AI knowledge base index (**AI Knowledge Base Index (Elasticsearch)**) | Its embedding deployment, fixed when created. Holds the chunks and their embeddings. |
| Data source (**Search Index Profile**) | Source index: the content index. Destination index: the knowledge base index. Content field `Content.ContentItem.FullText`, title field a display text field, key field empty (the document id, which is the content item id). |

`DataSourceIndexingService` reads the source documents, chunks and embeds them, and writes one document per chunk (`{referenceId}_{n}`). Content events and Orchard Core's index notifications queue a targeted re-sync, rebuilding the source index queues a full one, and a nightly alignment task repairs drift; see [Keeping the AI KB index in sync](../../ai/data-sources/index.md#keeping-the-ai-kb-index-in-sync).

## Restricted articles stay out

A knowledge base answers anyone allowed to query its profile, without the source's access checks, so it must only hold what everyone may read. `DataSourceIndexingService` asks every registered `IIndexedDocumentVisibility` (in `CrestApps.OrchardCore.Abstractions`, namespace `CrestApps.OrchardCore.Indexing`) whether a source document is for everyone, in a full sync and in a targeted one. A document that isn't is not indexed, and its chunks are deleted from the knowledge base, so an article restricted after it was indexed leaves at its next sync.

Content Access Control registers `ContentAccessIndexedDocumentVisibility`: a content item is for everyone when it has no row in `ContentAccessRoleIndex` (its type has no restricting role picker) or a row for `Anonymous` (no role picked). An article restricted to **Authenticated** or to staff roles is left out, even for staff using the admin chat: a knowledge base can't tell readers apart. The check uses the document id, so the data source's key field must stay the content item id.

A module that restricts other records implements the interface and registers it:

```csharp
services.AddIndexedDocumentVisibility<MyRecordVisibility>();
```

## Citations

A content index's references have the reference type `Content` (`AIConstants.DataSourceReferenceTypes.Content`), and `ContentItemAILinkGenerator` links each one to the item's display route, so a source under an answer opens the article. It needs the key field to be the content item id. See [Citation and reference tracking](../../ai/data-sources/index.md#citation--reference-tracking).

## Public chat

The **Artificial Intelligence Chat** widget shows only to people allowed to query its profile. Give the `Anonymous` role the profile's own `QueryAIProfile_{profileName}` permission (**Query AI profile - *name***), never `QueryAnyAIProfile`. Visitors' chats are rate limited and screened for prompt injection; see [AI chat](../../ai/chat.md#security-and-visitor-identity-settings).

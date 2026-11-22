---
sidebar_label: Azure AI Search
sidebar_position: 3
title: AI Data Sources - Azure AI Search
description: Azure AI Search support for AI data source knowledge base indexes with vector search and RAG capabilities.
user_manual:
  - user-manual/ai/knowledge
---

| | |
| --- | --- |
| **Feature Name** | AI Data Sources - Azure AI Search |
| **Feature ID** | `CrestApps.OrchardCore.AI.DataSources.AzureAI` |

Adds Azure AI Search support for AI data source document embeddings, vector search, and indexing.

## Overview

This module provides Azure AI Search support for AI data source knowledge base indexes. It enables vector search and document embedding storage using Azure AI Search's vector capabilities, allowing AI tools to perform Retrieval-Augmented Generation (RAG) searches against Azure AI Search indexes.

When AI profiles are configured with data sources, the system needs to search and retrieve relevant documents to provide context to AI models. This module handles:

- **Index schema management** — Creates and manages Azure AI Search index fields for knowledge base indexes with vector search profiles and embedding fields.
- **Document indexing** — Indexes source documents with their embeddings into Azure AI Search knowledge base indexes.
- **Vector search** — Performs vector similarity searches using Azure AI Search's built-in vector search to find the most relevant documents.
- **Filters** — Not supported on an Azure AI Search knowledge base index. The index declares no `filters` field, and Azure AI Search rejects a filter on an undeclared field, so the feature registers no OData filter translator. A filter set on an AI profile, a chat interaction or a data source search tool instance is ignored, and a warning is logged. See [Filtering retrieval](index.md#filtering-retrieval).
- **Batch document reading** — Reads source documents from Azure AI Search indexes in batches for efficient indexing.
- **Automatic source sync** — When Orchard Core updates or deletes documents in Azure AI Search, the module forwards those provider notifications into the shared data-source indexing queue so mapped knowledge-base indexes stay aligned.
- **External source connections** — Adds **Azure AI Search** as a selectable AI data-source **Source type** so operators can read directly from an external Azure AI Search index by configuring endpoint, authentication type, index name, and optional managed-identity client ID.

## Getting Started

1. Enable the **AI Data Sources - Azure AI Search** feature in the Orchard Core admin dashboard.
2. Create an Azure AI Search knowledge base index via **Search > Indexing** using the **AI Knowledge Base Index (Azure AI Search)** type.
3. Configure an AI data source under **Artificial Intelligence > Data Sources**, selecting either an Orchard-managed Azure AI Search source index profile or the **Azure AI Search** external source type and the knowledge base index.
4. The module will automatically sync documents from the source index to the knowledge base index with embeddings.

The data source editor is described in the User Manual under [Data sources](../../user-manual/ai/knowledge.md#data-sources).

## Use a data connection

When the **Data Connectors** feature is on, the external source editor also offers **Data connection**: pick an **HTTP API** connection whose URL is the search service endpoint (`https://<service>.search.windows.net`) and whose authentication is an **API key** (the header name, usually `api-key`, doesn't matter). The data source then reads the endpoint and the key from the connection, where the key stays protected; the connection's key is never written to the data source or to a deployment plan. The data source's own endpoint and key stay stored, unused, so switching back to its own settings loses nothing, and a deployment plan leaves its own key out while the connection is used. A connection without authentication uses the default Azure credential, such as the site's managed identity, and needs the host's approval on a tenant other than the default one (see [Signing in as the host](#signing-in-as-the-host)).

Only people who may **Manage data connections and their credentials** see the picker; for anyone else, a data source that uses a connection keeps it when they save. A data source without a connection keeps its own endpoint and credentials, so existing data sources are unchanged.

The search requests are checked when they connect, not only when the endpoint is saved: the data source's own endpoint follows the AI egress rule (it can't be on the site's own network unless the host sets `CrestApps:AI:Egress:AllowPrivateNetwork`, which a private endpoint needs), and a data connection follows the data connectors' network rule.

## Signing in as the host

**Managed identity** and **Default credential**, and a data connection without authentication, sign in with the host's Azure identity, not with a credential the tenant entered. On a host that serves several tenants, that identity may read every tenant's index, so it works only:

- on the default tenant, as the SQL Server connector's host sign-in does; or
- on another tenant, for a search service, index and identity the host's operator approved for that tenant.

The operator approves them in the tenant's own configuration (for example `App_Data/Sites/<tenant>/appsettings.json`, or the `OrchardCore:<tenant>` section of the host's configuration), which tenant administrators can't change:

```json
{
  "CrestApps": {
    "AI": {
      "DataSources": {
        "AzureAISearch": {
          "HostIdentityIndexes": [
            {
              "Endpoint": "https://contoso.search.windows.net",
              "IndexName": "tenant-a-policies",
              "IdentityClientId": "00000000-0000-0000-0000-000000000000"
            }
          ]
        }
      }
    }
  }
}
```

An entry matches a data source with the same service (scheme, host and port), the same index name and the same managed identity client ID; leave `IdentityClientId` out for the system-assigned identity, the default credential, or a data connection without authentication. The check runs when the data source is saved (editor, API and recipe) and again before every sync, so removing an entry stops the data source from reading the index. An API key needs no approval.

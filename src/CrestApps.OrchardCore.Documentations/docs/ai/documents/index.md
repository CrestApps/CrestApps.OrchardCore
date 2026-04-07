---
sidebar_label: Overview
sidebar_position: 1
title: AI Documents
description: Document upload, text extraction, embedding, and RAG capabilities for AI Chat Interactions, AI Profiles, and AI Chat Sessions.
---

| | |
| --- | --- |
| **Feature Name** | AI Documents |
| **Feature ID** | `CrestApps.OrchardCore.AI.Documents` |

Provides the foundation for document processing, text extraction, and Retrieval-Augmented Generation (RAG) capabilities.

## Overview

This module is the foundation for all document-related functionality in the CrestApps AI suite. It provides document upload, text extraction, embedding, and RAG (Retrieval-Augmented Generation) capabilities shared by both **AI Chat Interactions** and **AI Profiles**.

The base feature is **enabled by dependency only** — it activates automatically when either `AI Documents for Chat Interactions` or `AI Documents for Profiles` is enabled.

The base feature (`CrestApps.OrchardCore.AI.Documents`) provides the shared infrastructure used by both chat interaction and profile document features:

- **Unified Document Store**: A single `IAIDocumentStore` for storing and querying documents across all reference types (chat interactions, profiles)
- **Text Extraction**: Automatic text extraction from uploaded documents via registered `IngestionDocumentReader` implementations (from `Microsoft.Extensions.DataIngestion`)
- **Settings UI**: Admin settings page for configuring the default document index (**Settings > Artificial Intelligence**)
- **Document Processing Tools**: AI tools for listing, reading, and searching documents
- **RAG Search Tool**: Semantic vector search across uploaded documents
- **Strategy-Based Processing**: Adds document-focused prompt-processing strategies
- **Index & Migrations**: Shared `AIDocumentIndex` with `ReferenceId` and `ReferenceType` columns for multi-purpose document storage

The same document-processing pipeline is now shared with non-OrchardCore hosts. `CrestApps.Mvc.Web`, for example, uses the framework-owned document processor, search tools, and ingestion readers so text, OpenXml, and PDF uploads follow the same extraction and chunking rules as Orchard Core. Orchard Core now also bridges its native `IIndexProfileStore` into the shared `ISearchIndexProfileStore` contract so shared document RAG tools and handlers resolve tenant index profiles the same way in both hosts.

The shared PDF ingestion path now favors straightforward per-page text extraction instead of PdfPig layout-block analysis. That keeps PDF uploads more resilient for local and tenant-hosted chat attachment scenarios where some PDFs previously terminated the request process during extraction.

Orchard Core chat-interaction and chat-session document uploads now tolerate tenants where Orchard indexing services are not currently available. In that case, the document endpoints still store and remove attachments normally, while the Orchard-specific index update handler safely skips remote index synchronization instead of failing the request.

The Orchard Core host now also assigns route names to the shared chat-document upload and remove endpoints before generating UI links. That keeps chat pages and document editors posting to the JSON document endpoints instead of accidentally posting back to the current admin page, and the shared chat UI now collapses unexpected HTML error pages to readable upload/remove messages.

Within Orchard Core, the AI Documents feature also relies on its declared `OrchardCore.Indexing` dependency so document handlers can use explicit constructor-injected indexing services instead of lazily resolving core Orchard indexing registrations at runtime.

For chat upload/remove flows specifically, Orchard now queues the provider-specific index synchronization through the shell deferred-task pipeline instead of running that external indexing work inline during the upload request. That keeps the HTTP request focused on storing the attachment while the index update runs afterward with tenant-scoped services.

For local Aspire or `dotnet watch` development, the Orchard CMS host now excludes `App_Data` from watched items at both the default item and explicit watch-item levels, just like the MVC sample. That prevents document uploads, SQLite changes, and other runtime tenant writes from triggering an immediate app restart while you are testing chat attachments.

### Sub-Features

| Feature | ID | Description |
|---------|-----|-------------|
| **AI Documents for Chat Interactions** | `CrestApps.OrchardCore.AI.Documents.ChatInteractions` | Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Chat Interactions. |
| **AI Documents for Profiles** | `CrestApps.OrchardCore.AI.Documents.Profiles` | Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Profiles. |
| **AI Documents for Chat Sessions** | `CrestApps.OrchardCore.AI.Documents.ChatSessions` | Provides document upload and RAG support for AI Chat Sessions and AI Chat Widgets. |

## AI Documents for Chat Interactions

| | |
| --- | --- |
| **Feature Name** | AI Documents for Chat Interactions |
| **Feature ID** | `CrestApps.OrchardCore.AI.Documents.ChatInteractions` |

Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Chat Interactions.

When enabled, a **Documents** tab appears in the chat interaction UI, allowing users to upload documents and chat against their own data.

Documents uploaded to a chat interaction are **scoped to that session**.

### Key Capabilities

- **Document Upload**: Upload documents via drag-and-drop or file browser
- **Text Extraction**: Content is automatically extracted from uploaded documents
- **Chunking & Embedding**: Text is split into chunks and embedded for semantic vector search
- **RAG Integration**: Relevant document chunks are retrieved and used as context for AI responses
- **Document Management**: View, manage, and remove uploaded documents within a chat session

### Document Processing

When documents are attached to a chat interaction, the orchestrator manages document context automatically. It coordinates text extraction, chunking, embedding, and retrieval to provide relevant document content to the AI model.

The orchestrator supports various document-related operations:

- **Question Answering (RAG)** — Uses vector search to find relevant document chunks for answering questions
- **Summarization** — Provides full document content for summarization requests
- **Tabular Analysis** — Parses structured data (CSV, Excel) for calculations and analysis
- **Data Extraction** — Extracts structured information from documents
- **Document Comparison** — Provides multi-document content for comparison
- **Content Transformation** — Provides content for reformatting or conversion
- **General Reference** — Provides context when asking general questions that reference documents

### Getting Started

1. **Set up an indexing provider**: Enable Elasticsearch or Azure AI Search in the Orchard Core admin.
2. **Create an index**: Navigate to **Search > Indexing** and create a new index (e.g., "AI Documents").
3. **Configure settings**: Navigate to **Settings > Artificial Intelligence** and select your new index. You can leave the document index empty until you are ready to enable document retrieval; after this is configured in production, avoid changing the index profile to prevent losing access to documents in existing sessions.
4. **Enable the feature**: Enable `AI Chat Interaction Documents` in the admin dashboard.
5. Start using the Documents tab in your chat interactions.

If no **AI Documents** index has been configured yet, the UI should warn you before uploads are treated as searchable knowledge. Uploads can still be stored, but vector retrieval does not become active until a compatible AI Documents index is selected.
Changing the shared document index settings in Orchard Core now also shows a tenant reload warning and requests a shell release so refreshed `InteractionDocumentOptions` are applied consistently.
For Orchard Core provider modules, the Azure AI Search and Elasticsearch document search services now resolve their search clients from Orchard Core's provider infrastructure rather than relying on the standalone framework provider registrations.
Document index profiles now use the same shared `DataSourceIndexProfileMetadata` embedding-deployment record used by data sources, and the AI module migrates older document-specific metadata into that shared deployment-based format automatically.

## AI Documents for Profiles

| | |
| --- | --- |
| **Feature Name** | AI Documents for Profiles |
| **Feature ID** | `CrestApps.OrchardCore.AI.Documents.Profiles` |

Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Profiles.

When enabled, a **Documents** tab appears on the AI Profile editor, allowing administrators to attach text-based documents that will be chunked, embedded, and used as context across all chat sessions using that profile.

Unlike chat interaction documents (which are scoped to a single session), profile documents **persist across all sessions** using the profile.
Profile documents are treated as **background knowledge**. End users should not be told that the profile has attached documents unless they explicitly upload documents in the current session.

### Key Capabilities

- **Document Upload**: Upload text-based documents (PDF, Word, Markdown, etc.) directly to an AI Profile
- **Automatic Text Extraction**: Content is extracted from uploaded documents using registered `IngestionDocumentReader` implementations
- **Chunking & Embedding**: Extracted text is split into chunks and embedded for semantic vector search
- **RAG Integration**: Relevant document chunks are automatically retrieved and used as context for AI responses
- **Top N Configuration**: Control how many matching chunks are included as context (default: 3)

### Supported File Types

Only embeddable file extensions are supported for AI Profile documents. The set of embeddable extensions is determined by the registered `IngestionDocumentReader` implementations. Typically, this includes:

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

:::note Note
Tabular file types (`.csv`, `.tsv`, `.xlsx`, `.xls`) are registered as non-embeddable and are not available for AI Profile document upload, since they are intended for tabular data analysis rather than text-based retrieval-augmented generation (RAG).
:::

### How It Works

Documents are managed directly through the AI Profile editor form. When you save a profile:

1. **New files** selected in the Knowledge tab are uploaded, text-extracted, chunked, embedded, and stored
2. **Removed documents** marked for deletion are removed from the store
3. All changes are applied atomically when the profile is saved

There are no separate API endpoints for profile document management — everything is handled through the standard profile editor workflow.

### Getting Started

1. Enable the `AI Documents for Profiles` feature in the Orchard Core admin dashboard.
2. Navigate to **Artificial Intelligence > AI Profiles** and edit a profile.
3. Use the **Knowledge** tab to upload text-based documents.
4. Configure the **Top N Results** setting to control how many matching chunks are included as context.

For MVC hosts that use the shared framework services, configure the default AI Documents index first so uploaded profile documents are embedded into the expected search backend instead of remaining as unindexed attachments only. MVC profile-document uploads now resolve embeddings against the profile's active chat or utility deployment first, then fall back to the global embedding deployment, and profile document removals delete their indexed chunks so the knowledge base stays aligned with the saved profile.

## AI Documents for Chat Sessions

| | |
| --- | --- |
| **Feature Name** | AI Documents for Chat Sessions |
| **Feature ID** | `CrestApps.OrchardCore.AI.Documents.ChatSessions` |

Provides document upload and Retrieval-Augmented Generation (RAG) support directly within AI Chat Sessions and AI Chat Widgets (both admin and frontend).

When enabled, users can attach documents to any chat session via drag-and-drop or file browser. Documents are indexed using the same shared infrastructure (text extraction, chunking, embedding, and vector search) used by Chat Interactions and Profiles.

Unlike profile documents (which persist across all sessions), chat session documents are **scoped to the individual session** — similar to chat interaction documents.

### Key Capabilities

- **Document Upload**: Drag-and-drop or browse to attach files directly in the chat input area
- **Visual Attach Button**: A persistent "Attach files" button appears above the chat input when enabled
- **Document Pills**: Attached documents are shown as compact pill badges with remove (X) buttons
- **Drag-and-Drop Highlight**: The input area highlights when files are dragged over it
- **Text Extraction & Embedding**: Uploaded documents are automatically extracted, chunked, and embedded for vector search
- **RAG Integration**: Relevant chunks are retrieved and used as context for AI responses
- **Per-Profile Opt-In**: Each AI Profile has an **Allow Documents & Attachments** checkbox (unchecked by default) to control whether document upload is available

### Per-Profile Opt-In

Because document processing is resource-intensive, document upload is **not enabled by default** even when the feature is active. Administrators must explicitly opt in for each AI Profile:

1. Navigate to **Artificial Intelligence > AI Profiles** and edit a profile.
2. In the **Documents** section, check **Allow Documents & Attachments**.
3. Save the profile.

For **AI Chat Widget** content items, the same checkbox appears on the widget editor under the AI profile part settings.

### Supported UIs

| UI | Where | Notes |
|----|-------|-------|
| **AI Chat Session** | Admin > Artificial Intelligence > AI Chat | Full session page |
| **AI Chat Admin Widget** | Floating admin widget | Compact chat widget on admin pages |
| **AI Chat Widget** | Frontend content widget | Public-facing chat widget |

### Getting Started

1. **Set up an indexing provider**: Enable Elasticsearch or Azure AI Search in the Orchard Core admin.
2. **Create an index**: Navigate to **Search > Indexing** and create a new index (e.g., "AI Documents").
3. **Configure settings**: Navigate to **Settings > Artificial Intelligence** and select your new index. You can leave the document index empty until you are ready to enable document retrieval; after this is configured in production, avoid changing the index profile to prevent losing access to documents in existing sessions.
4. **Enable the feature**: Enable `AI Documents for Chat Sessions` in the admin dashboard.
5. **Opt in per profile**: Edit the desired AI Profile and check **Allow Documents & Attachments**.
6. Open a chat session — the attach button and drag-and-drop zone are now available.

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

## Document Lifecycle & Cleanup

When a chat interaction, chat session, or AI profile is deleted, all associated documents are automatically cleaned up:

| Scope | What happens on deletion |
|-------|------------------------|
| **Chat Interaction** | Document chunks are removed from all AI document indexes. `AIDocument` records are deleted from the document store. |
| **Chat Session** | All session documents are deleted from the document store. Document chunks are removed from all AI document indexes via a deferred task. |
| **AI Profile** | Documents are managed via the profile editor — removing a document triggers index chunk cleanup and store deletion on save. |

This ensures the AI document indexes stay free of orphaned entries when their parent resources are removed.

## Troubleshooting

### "Index Not Configured" Warning

If you see this warning, navigate to **Settings > Artificial Intelligence** and select an index profile.
If no index profiles are available, go to **Search > Indexing**, add an **AI Documents** index, and enable one of the **AI Documents indexing** features if the **AI Documents** index type is not listed.
Leaving the setting empty is supported while you are configuring other AI features, but document retrieval remains unavailable until a valid index profile is selected.

In `CrestApps.Mvc.Web`, the same requirement applies: upload storage alone does not make a document searchable. The MVC admin **AI Settings** page now lets you select the default **AI Documents** index profile and the default document `Top N` retrieval value. Until that setting is configured, the profile editor shows a warning so users know document uploads will not influence AI answers yet.

### "Embedding Search Service Not Available" Warning

This means the configured index profile doesn't have a registered embedding/search service. Supported providers include Elasticsearch and Azure AI Search. Make sure:
1. The corresponding feature is enabled (Elasticsearch or Azure AI Search)
2. Your index is configured to use a supported provider

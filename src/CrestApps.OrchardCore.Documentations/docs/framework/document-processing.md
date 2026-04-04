---
sidebar_label: Document Processing
sidebar_position: 6
title: Document Processing
description: Document readers, semantic search, and tabular data extraction for RAG-powered chat experiences.
---

# Document Processing

> Reads, chunks, and indexes uploaded documents so the AI can search and reference them during conversations.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddChatInteractionServices()
    .AddDefaultDocumentProcessingServices()
    .AddOpenAIProvider();
```

## Problem & Solution

Users upload documents (PDFs, Word files, spreadsheets) and expect the AI to answer questions about them. This requires:

- **Reading** diverse file formats into plain text
- **Chunking** large documents into embeddable segments
- **Embedding** chunks into vector space for semantic search
- **Searching** relevant chunks at query time (RAG)
- **Tabular processing** for CSV/Excel data with structured queries

The document processing system handles the full pipeline from upload to retrieval.

## Services Registered by `AddDefaultDocumentProcessingServices()`

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `IAIDocumentProcessingService` | `DefaultAIDocumentProcessingService` | Scoped | Reads, chunks, and materializes `AIDocument` / `AIDocumentChunk` records |
| `ITabularBatchProcessor` | `TabularBatchProcessor` | Scoped | Processes CSV/Excel batch queries |
| `ITabularBatchResultCache` | `TabularBatchResultCache` | Singleton | Caches tabular query results |
| `DocumentOrchestrationHandler` | — | Scoped | Injects document context into orchestration |

### Built-in Document Readers

| Reader | Supported Extensions | Embeddable |
|--------|---------------------|------------|
| `PlainTextIngestionDocumentReader` | `.txt`, `.md`, `.json`, `.xml`, `.html`, `.htm`, `.log`, `.yaml`, `.yml` | Yes |
| `PlainTextIngestionDocumentReader` | `.csv` | No (tabular) |
| `OpenXmlIngestionDocumentReader` | `.docx`, `.pptx` | Yes |
| `OpenXmlIngestionDocumentReader` | `.xlsx` | No (tabular) |
| `PdfIngestionDocumentReader` | `.pdf` | Yes |

### System Tools for Documents

These tools are automatically available to the orchestrator when documents are attached:

| Tool | Purpose |
|------|---------|
| `SearchDocumentsTool` | Semantic vector search across uploaded documents |
| `ReadDocumentTool` | Reads full text of a specific document |
| `ReadTabularDataTool` | Reads and parses CSV/TSV/Excel data |

## Key Interfaces

### `IAIDocumentProcessingService`

Processes an uploaded file after the host has resolved any embedding generator it wants to use.

```csharp
public interface IAIDocumentProcessingService
{
    Task<DocumentProcessingResult> ProcessFileAsync(
        IFormFile file,
        string referenceId,
        string referenceType,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator);
}
```

The framework no longer asks `IAIDocumentProcessingService` to create embedding generators. Hosts resolve the embedding deployment through `IAIDeploymentManager` and create the generator through `IAIClientFactory`, then pass it into `ProcessFileAsync(...)`. That keeps deployment selection and AI client creation in the shared client/deployment runtime instead of duplicating that logic inside the document processor.

### Adding a Custom Document Reader

Register a reader for additional file formats:

```csharp
builder.Services.AddIngestionDocumentReader<MyCustomReader>(".custom", ".myformat");
```

Implement the reader:

```csharp
public sealed class MyCustomReader : IngestionDocumentReader
{
    public override Task<IngestionDocument> ReadAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        // Parse the stream into sections and elements
    }
}
```

## Configuration

### `ChatDocumentsOptions`

Controls which file types can be uploaded and processed.

```csharp
services.Configure<ChatDocumentsOptions>(options =>
{
    // Add a new embeddable extension
    options.Add(".rtf", embeddable: true);

    // Add a tabular (non-embeddable) extension
    options.Add(".tsv", embeddable: false);
});
```

- **AllowedFileExtensions** — Complete set of uploadable extensions
- **EmbeddableFileExtensions** — Subset that gets vector-embedded (non-embeddable files use direct read tools instead)

### Limits

- Maximum **25,000 characters** total for embedding per session
- Results are cached via `IDistributedCache` for batch tabular queries

## Storage

Document metadata and chunks require store implementations:

```csharp
builder.Services.AddScoped<IAIDocumentStore, YesSqlAIDocumentStore>();
builder.Services.AddScoped<IAIDocumentChunkStore, YesSqlAIDocumentChunkStore>();
```

## Orchard Core Integration

The [AI Documents module](../ai/index.md) adds admin UI for document management, automatic indexing via Elasticsearch or Azure AI Search, and deployment-step support for document configuration.

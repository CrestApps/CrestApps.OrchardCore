---
sidebar_label: AI Documents
sidebar_position: 14
title: AI Documents
description: Upload, process, chunk, embed, and search documents so the AI model can retrieve relevant content during conversations (RAG).
---

# AI Documents

> A complete document management pipeline that reads uploaded files, splits them into chunks, generates vector embeddings, and makes the content searchable via semantic similarity — enabling retrieval-augmented generation (RAG) in AI conversations.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddChatInteractionHandlers()
    .AddDefaultDocumentProcessingServices()
    .AddOpenAIProvider();

// Register document and chunk stores
builder.Services.AddScoped<IAIDocumentStore, YesSqlAIDocumentStore>();
builder.Services.AddScoped<IAIDocumentChunkStore, YesSqlAIDocumentChunkStore>();
```

Upload a file and process it:

```csharp
public sealed class DocumentUploadController(
    IAIDocumentProcessingService processingService,
    IAIDocumentStore documentStore) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Upload(
        IFormFile file,
        string referenceId,
        string referenceType)
    {
        var embeddingGenerator =
            await processingService.CreateEmbeddingGeneratorAsync("OpenAI", "default");

        var result = await processingService.ProcessFileAsync(
            file, referenceId, referenceType, embeddingGenerator);

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
```

## Problem & Solution

Users upload documents (PDFs, Word files, spreadsheets, text files) and expect the AI to answer questions about them. This requires a multi-stage pipeline:

- **Reading** — Extract plain text from diverse file formats (`.pdf`, `.docx`, `.xlsx`, `.csv`, `.txt`, `.md`, and more)
- **Chunking** — Split large documents into segments small enough to embed
- **Embedding** — Convert each chunk into a vector representation using a configured embedding model
- **Indexing** — Store embeddings in a vector search index (Elasticsearch or Azure AI Search)
- **Searching** — At query time, perform semantic similarity search to find the most relevant chunks
- **Tabular processing** — CSV and Excel files receive special treatment with structured, batch-oriented queries

The document processing system handles this full pipeline from upload to retrieval, while the built-in document tools make the content available to the AI during orchestration.

## Architecture Overview

```text
┌─────────────┐
│  User Upload │
└──────┬──────┘
       ▼
┌──────────────────────────────┐
│  IAIDocumentProcessingService │  ← Orchestrates the pipeline
├──────────────────────────────┤
│  1. Store document record     │  → IAIDocumentStore
│  2. Read file content         │  → IngestionDocumentReader (keyed by extension)
│  3. Normalize & chunk text    │  → RagTextNormalizer
│  4. Store chunks              │  → IAIDocumentChunkStore
│  5. Generate embeddings       │  → IEmbeddingGenerator<string, Embedding<float>>
│  6. Index in vector store     │  → ISearchDocumentManager (Elasticsearch / Azure AI)
└──────────────────────────────┘

       ┌─────────────────────────────────────┐
       │          During Conversation         │
       ├─────────────────────────────────────┤
       │  DocumentOrchestrationHandler        │
       │  detects documents on the session    │
       │  and injects document tools:         │
       │                                      │
       │  • SearchDocumentsTool (vector RAG)  │
       │  • ReadDocumentTool (full text read) │
       │  • ReadTabularDataTool (CSV/Excel)   │
       └──────────────┬──────────────────────┘
                      ▼
       ┌─────────────────────────────────────┐
       │  AI Model calls tools as needed      │
       │  to answer user questions about      │
       │  the uploaded documents              │
       └─────────────────────────────────────┘
```

## Core Interfaces

| Interface | Package | Purpose |
|-----------|---------|---------|
| `IAIDocumentStore` | `CrestApps.AI` | CRUD for document records |
| `IAIDocumentChunkStore` | `CrestApps.AI` | CRUD for document chunks |
| `IAIDocumentProcessingService` | `CrestApps.AI.Chat` | Orchestrates file → chunk → embed → index |
| `ISearchDocumentManager` | `CrestApps.AI` | Manages documents in the vector search index |
| `IVectorSearchService` | `CrestApps.AI` | Performs vector similarity search at query time |
| `ITabularBatchProcessor` | `CrestApps.AI.Chat` | Splits and processes CSV/Excel batch queries |
| `ITabularBatchResultCache` | `CrestApps.AI.Chat` | Caches tabular query results |
| `IngestionDocumentReader` | `CrestApps.AI` | Abstract base for format-specific file readers |

## Document Processing Pipeline

### Step 1 — Upload and Store

When a file is uploaded, a new `AIDocument` record is created in `IAIDocumentStore`:

```csharp
public sealed class AIDocument : CatalogItem
{
    public string ReferenceId { get; set; }    // Owning resource (e.g., chat interaction ID)
    public string ReferenceType { get; set; }  // Resource type (e.g., "chatinteraction")
    public string FileName { get; set; }       // Original file name
    public string ContentType { get; set; }    // MIME type
    public long FileSize { get; set; }         // Size in bytes
    public DateTime UploadedUtc { get; set; }  // Upload timestamp
}
```

The `ReferenceId` and `ReferenceType` pair ties the document to an owning resource. Common reference types include:

| Constant | Value | Meaning |
|----------|-------|---------|
| `AIReferenceTypes.Document.Profile` | `"profile"` | Document attached to an AI profile |
| `AIReferenceTypes.Document.ChatInteraction` | `"chatinteraction"` | Document attached to a chat interaction |
| `AIReferenceTypes.Document.ChatSession` | `"chatsession"` | Document attached to a chat session |

### Step 2 — Read File Content

An `IngestionDocumentReader` is resolved as a keyed service using the file extension. The reader extracts plain text from the file:

```csharp
public abstract class IngestionDocumentReader
{
    public abstract Task<IngestionDocument> ReadAsync(
        Stream source,
        string identifier,
        string mediaType,
        CancellationToken cancellationToken = default);
}
```

### Step 3 — Normalize and Chunk

The extracted text is normalized (whitespace, encoding) and split into chunks. Each chunk becomes an `AIDocumentChunk`:

```csharp
public sealed class AIDocumentChunk : CatalogItem
{
    public string AIDocumentId { get; set; }   // Parent document ID
    public string ReferenceId { get; set; }    // Denormalized from parent
    public string ReferenceType { get; set; }  // Denormalized from parent
    public string Content { get; set; }        // Chunk text
    public float[] Embedding { get; set; }     // Vector embedding
    public int Index { get; set; }             // Chunk order within the document
}
```

The `ReferenceId` and `ReferenceType` are denormalized from the parent document for efficient query access without joins.

### Step 4 — Generate Embeddings

If the file extension is **embeddable** (see [Built-in Document Readers](#built-in-document-readers)), each chunk is converted to a vector via `IEmbeddingGenerator<string, Embedding<float>>`:

```csharp
var embeddingGenerator =
    await processingService.CreateEmbeddingGeneratorAsync("OpenAI", "default");
```

The generator is created from the configured provider and connection. Embeddings are stored on the chunk itself (`Embedding` property) so they survive index rebuilds.

### Step 5 — Index in Vector Store

Chunks with embeddings are pushed to the search index via `ISearchDocumentManager`:

```csharp
public interface ISearchDocumentManager
{
    Task<bool> AddOrUpdateAsync(
        IIndexProfileInfo profile,
        IReadOnlyCollection<IndexDocument> documents,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        IIndexProfileInfo profile,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default);

    Task DeleteAllAsync(
        IIndexProfileInfo profile,
        CancellationToken cancellationToken = default);
}
```

Implementations are registered as keyed services by provider name (e.g., `"Elasticsearch"`, `"AzureAISearch"`).

### Step 6 — Query-Time Retrieval

During a conversation, `SearchDocumentsTool` calls `IVectorSearchService` to find the most relevant chunks:

```csharp
public interface IVectorSearchService
{
    Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        IIndexProfileInfo indexProfile,
        float[] embedding,
        string referenceId,
        string referenceType,
        int topN,
        CancellationToken cancellationToken = default);
}
```

The user's query is embedded, and the resulting vector is compared against indexed chunks using cosine similarity.

## Built-in Document Readers

| Reader | Extensions | Embeddable | Notes |
|--------|-----------|------------|-------|
| `PlainTextIngestionDocumentReader` | `.txt`, `.md`, `.json`, `.xml`, `.html`, `.htm`, `.log`, `.yaml`, `.yml` | Yes | UTF-8 stream reader |
| `PlainTextIngestionDocumentReader` | `.csv` | No | Tabular — processed via `ReadTabularDataTool` |
| `OpenXmlIngestionDocumentReader` | `.docx`, `.pptx` | Yes | Uses `DocumentFormat.OpenXml` SDK |
| `OpenXmlIngestionDocumentReader` | `.xlsx` | No | Tabular — processed via `ReadTabularDataTool` |
| `PdfIngestionDocumentReader` | `.pdf` | Yes | Uses `UglyToad.PdfPig` with DocstrumBoundingBoxes |

**Embeddable** means the content is chunked and vector-embedded for semantic search. **Non-embeddable** (tabular) formats are instead handled by the `ReadTabularDataTool` which reads and parses them directly.

## Custom Document Reader

Register a reader for additional file formats:

```csharp
builder.Services.AddIngestionDocumentReader<RtfIngestionDocumentReader>(
    new ExtractorExtension(".rtf", embeddable: true));
```

Implement the reader:

```csharp
public sealed class RtfIngestionDocumentReader : IngestionDocumentReader
{
    public override async Task<IngestionDocument> ReadAsync(
        Stream source,
        string identifier,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        // Parse the RTF stream into plain text
        using var reader = new StreamReader(source);
        var rawContent = await reader.ReadToEndAsync(cancellationToken);
        var plainText = StripRtfFormatting(rawContent);

        return new IngestionDocument
        {
            Content = plainText,
            Identifier = identifier,
        };
    }
}
```

### `ExtractorExtension`

The `ExtractorExtension` type defines a file extension and whether its content is embeddable:

```csharp
public sealed class ExtractorExtension
{
    public string Extension { get; }   // Normalized with leading dot (e.g., ".rtf")
    public bool Embeddable { get; }    // Whether embeddings should be generated

    public ExtractorExtension(string extension, bool embeddable = true);
}
```

There is an implicit conversion from `string` to `ExtractorExtension` (with `embeddable: true` by default), so you can pass bare strings for embeddable extensions:

```csharp
// These are equivalent:
services.AddIngestionDocumentReader<MyReader>(".rtf");
services.AddIngestionDocumentReader<MyReader>(new ExtractorExtension(".rtf", true));

// For non-embeddable extensions, use the explicit constructor:
services.AddIngestionDocumentReader<MyReader>(new ExtractorExtension(".tsv", false));
```

### `AddIngestionDocumentReader<T>`

```csharp
public static IServiceCollection AddIngestionDocumentReader<T>(
    this IServiceCollection services,
    params ExtractorExtension[] supportedExtensions)
    where T : IngestionDocumentReader;
```

This method:
1. Registers the reader as a singleton
2. Registers a keyed singleton for each extension (used to resolve the right reader at runtime)
3. Adds the extensions to `ChatDocumentsOptions`

## Document Tools

Three system tools are automatically available when documents are attached to a session. They are registered with `AIToolPurposes.DocumentProcessing` and injected by `DocumentOrchestrationHandler`.

### `SearchDocumentsTool`

**Name:** `search_documents` (`SystemToolNames.SearchDocuments`)

Performs semantic vector search across all uploaded documents for the current session and returns the most relevant text chunks.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | `string` | Yes | The search query to find relevant content |
| `top_n` | `integer` | No | Number of top matching chunks to return (default: 3) |

### `ReadDocumentTool`

**Name:** `read_document` (`SystemToolNames.ReadDocument`)

Reads the full text content of a specific uploaded document. Truncates output to 50 KB maximum.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `document_id` | `string` | Yes | The unique identifier of the document to read |

### `ReadTabularDataTool`

**Name:** `read_tabular_data` (`SystemToolNames.ReadTabularData`)

Reads tabular data from CSV, TSV, or Excel files and returns formatted rows suitable for analysis.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `document_id` | `string` | Yes | The unique identifier of the tabular document |
| `max_rows` | `integer` | No | Maximum number of data rows to return (default: 100) |

**Supported extensions:** `.csv`, `.tsv`, `.xlsx`, `.xls`

## Implementing Stores

The framework defines two store interfaces. You must provide implementations for your persistence layer.

### `IAIDocumentStore`

```csharp
public interface IAIDocumentStore : ICatalog<AIDocument>
{
    Task<IReadOnlyCollection<AIDocument>> GetDocumentsAsync(
        string referenceId,
        string referenceType);
}
```

Inherits CRUD operations from `ICatalog<T>`:

| Method | Description |
|--------|-------------|
| `CreateAsync(T)` | Insert a new document record |
| `UpdateAsync(T)` | Update an existing document record |
| `DeleteAsync(T)` | Delete a document record |
| `FindByIdAsync(string)` | Find a document by its `ItemId` |
| `GetAllAsync()` | Retrieve all documents |
| `GetAsync(IEnumerable<string>)` | Retrieve documents by IDs |
| `PageAsync(int, int, TQuery)` | Paginated query |
| `SaveChangesAsync()` | Flush pending changes |

### `IAIDocumentChunkStore`

```csharp
public interface IAIDocumentChunkStore : ICatalog<AIDocumentChunk>
{
    Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByAIDocumentIdAsync(string documentId);
    Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByReferenceAsync(
        string referenceId, string referenceType);
    Task DeleteByDocumentIdAsync(string documentId);
}
```

### Registration

Register your implementations with the DI container:

```csharp
builder.Services.AddScoped<IAIDocumentStore, YesSqlAIDocumentStore>();
builder.Services.AddScoped<IAIDocumentChunkStore, YesSqlAIDocumentChunkStore>();
```

See [Data Storage](./data-storage.md) for more on the catalog pattern and YesSql index conventions.

## Orchestration Integration

`DocumentOrchestrationHandler` implements `IOrchestrationContextBuilderHandler` and is registered automatically by `AddDefaultDocumentProcessingServices()`.

```csharp
public sealed class DocumentOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    public Task BuildingAsync(OrchestrationContextBuildingContext context);
    public Task BuiltAsync(OrchestrationContextBuiltContext context);
}
```

During context building, the handler:

1. Checks if the current session has documents (via `ReferenceId` / `ReferenceType`)
2. If documents exist, sets `AICompletionContextKeys.HasDocuments = true`
3. Discovers all tools with purpose `AIToolPurposes.DocumentProcessing` and adds them to the tool set
4. Enriches the system message with document metadata so the model knows what content is available

This means document tools are **only** injected when the session actually has documents — no wasted tokens on tool descriptions when there are no documents.

## Tabular Data

CSV, TSV, and Excel files are marked as **non-embeddable** and receive special processing.

### `ITabularBatchProcessor`

Splits large tabular content into batches, processes each batch with the LLM, and merges results:

```csharp
public interface ITabularBatchProcessor
{
    IList<TabularBatch> SplitIntoBatches(string content, string fileName);

    Task<IList<TabularBatchResult>> ProcessBatchesAsync(
        IList<TabularBatch> batches,
        string userPrompt,
        TabularBatchContext context,
        CancellationToken cancellationToken = default);

    string MergeResults(IList<TabularBatchResult> results, bool includeHeader = true);
}
```

### `ITabularBatchResultCache`

Caches batch results to avoid re-processing identical queries:

```csharp
public interface ITabularBatchResultCache
{
    string GenerateCacheKey(string interactionId, string documentContentHash, string prompt);
    string ComputeDocumentContentHash(IEnumerable<(string FileName, string Content)> documents);
    TabularBatchCacheEntry TryGet(string cacheKey);
    void Set(string cacheKey, TabularBatchCacheEntry entry, TimeSpan? expiration = null);
    void Remove(string cacheKey);
    void InvalidateForInteraction(string interactionId);
}
```

When documents are added or removed from an interaction, call `InvalidateForInteraction` to clear stale cache entries.

## Configuration

### `ChatDocumentsOptions`

Controls which file types can be uploaded and how they are processed:

```csharp
services.Configure<ChatDocumentsOptions>(options =>
{
    // Add an embeddable extension
    options.Add(".rtf", embeddable: true);

    // Add a tabular (non-embeddable) extension
    options.Add(".tsv", embeddable: false);
});
```

| Property | Type | Description |
|----------|------|-------------|
| `AllowedFileExtensions` | `IReadOnlySet<string>` | Complete set of uploadable file extensions |
| `EmbeddableFileExtensions` | `IReadOnlySet<string>` | Subset that gets vector-embedded |

Extensions not in `EmbeddableFileExtensions` are still allowed for upload and can be read by `ReadDocumentTool` or `ReadTabularDataTool`, but they are not chunked and embedded.

### `InteractionDocumentSettings`

Per-interaction settings for document search:

```csharp
public sealed class InteractionDocumentSettings
{
    public string IndexProfileName { get; set; }  // Index profile for embedding and search
    public int TopN { get; set; } = 3;             // Top matching chunks to include in context
}
```

### Limits

- Maximum **25,000 characters** total for embedding per session
- `ReadDocumentTool` truncates output to **50 KB**
- `ReadTabularDataTool` defaults to **100 rows** maximum

## Services Registered by `AddDefaultDocumentProcessingServices()`

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `IAIDocumentProcessingService` | `DefaultAIDocumentProcessingService` | Scoped | Orchestrates document processing |
| `ITabularBatchProcessor` | `TabularBatchProcessor` | Scoped | Processes CSV/Excel batch queries |
| `ITabularBatchResultCache` | `TabularBatchResultCache` | Singleton | Caches tabular query results |
| `DocumentOrchestrationHandler` | — | Scoped | Injects document context into orchestration |
| `PlainTextIngestionDocumentReader` | — | Singleton | `.txt`, `.csv`, `.md`, `.json`, `.xml`, `.html`, `.htm`, `.log`, `.yaml`, `.yml` |
| `OpenXmlIngestionDocumentReader` | — | Singleton | `.docx`, `.xlsx`, `.pptx` |
| `PdfIngestionDocumentReader` | — | Singleton | `.pdf` |
| `SearchDocumentsTool` | — | System tool | Semantic vector search |
| `ReadDocumentTool` | — | System tool | Full document read |
| `ReadTabularDataTool` | — | System tool | Tabular data queries |

## Orchard Core Integration

The [AI Documents module](../ai/index.md) wraps this framework with admin UI for document management, automatic indexing via Elasticsearch or Azure AI Search, YesSql-backed stores, deployment-step support, and multi-tenant isolation. See also the [Document Processing](./document-processing.md) page for a condensed overview.

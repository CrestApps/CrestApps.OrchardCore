# CrestApps.OrchardCore.AI.DataSources.Elasticsearch

## Overview

This module provides Elasticsearch support for AI data source knowledge base indexes. It enables vector search and document embedding storage using Elasticsearch's k-NN capabilities, allowing AI tools to perform Retrieval-Augmented Generation (RAG) searches against Elasticsearch indexes.

## Problem Solved

When AI profiles are configured with data sources, the system needs to search and retrieve relevant documents to provide context to AI models. This module handles:

- **Index schema management**: Creates and manages Elasticsearch mappings for knowledge base indexes with dense vector fields for embeddings.
- **Document indexing**: Indexes source documents with their embeddings into Elasticsearch knowledge base indexes.
- **Vector search**: Performs k-NN similarity searches to find the most relevant documents for a given query.
- **Filter execution**: Executes Elasticsearch DSL filter queries for two-phase RAG search (filter first, then vector search within filtered results).
- **Document reading**: Reads source documents from Elasticsearch indexes in batches for efficient indexing.

## Features

- **Feature ID**: `CrestApps.OrchardCore.AI.DataSources.Elasticsearch`
- **Dependencies**: `CrestApps.OrchardCore.AI.DataSources`, `OrchardCore.Search.Elasticsearch`

## Usage

1. Enable the `AI Data Sources` feature and the `AI Data Sources - Elasticsearch` feature in the Orchard Core admin dashboard.
2. Create an Elasticsearch knowledge base index via **Search > Indexes** using the "AI Knowledge Base Index" type.
3. Configure an AI data source under **Artificial Intelligence > Data Sources**, selecting an Elasticsearch source index and the knowledge base index.
4. The module will automatically sync documents from the source index to the knowledge base index with embeddings.

## Services Registered

| Service | Key | Description |
|---------|-----|-------------|
| `IDataSourceVectorSearchService` | `Elasticsearch` | Performs k-NN vector searches against Elasticsearch knowledge base indexes |
| `IDataSourceDocumentReader` | `Elasticsearch` | Reads source documents from Elasticsearch indexes in batches |
| `IDataSourceFilterExecutor` | `Elasticsearch` | Executes Elasticsearch DSL filter queries for two-phase search |
| `IIndexProfileHandler` | — | Manages Elasticsearch index mappings for knowledge base indexes |
| `IDocumentIndexHandler` | — | Handles document indexing into Elasticsearch knowledge base indexes |

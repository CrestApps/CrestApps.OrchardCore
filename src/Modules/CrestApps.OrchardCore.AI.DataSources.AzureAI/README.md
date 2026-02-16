# CrestApps.OrchardCore.AI.DataSources.AzureAI

## Overview

This module provides Azure AI Search support for AI data source knowledge base indexes. It enables vector search and document embedding storage using Azure AI Search's vector capabilities, allowing AI tools to perform Retrieval-Augmented Generation (RAG) searches against Azure AI Search indexes.

## Problem Solved

When AI profiles are configured with data sources, the system needs to search and retrieve relevant documents to provide context to AI models. This module handles:

- **Index schema management**: Creates and manages Azure AI Search index fields for knowledge base indexes with vector search profiles and embedding fields.
- **Document indexing**: Indexes source documents with their embeddings into Azure AI Search knowledge base indexes.
- **Vector search**: Performs vector similarity searches using Azure AI Search's built-in vector search to find the most relevant documents.
- **Filter execution**: Executes OData filter expressions for two-phase RAG search (filter first, then vector search within filtered results).
- **Document reading**: Reads source documents from Azure AI Search indexes in batches for efficient indexing.

## Features

- **Feature ID**: `CrestApps.OrchardCore.AI.DataSources.AzureAI`
- **Dependencies**: `CrestApps.OrchardCore.AI.DataSources`, `OrchardCore.Search.AzureAI`

## Usage

1. Enable the `AI Data Sources` feature and the `AI Data Sources - Azure AI Search` feature in the Orchard Core admin dashboard.
2. Create an Azure AI Search knowledge base index via **Search > Indexes** using the "AI Knowledge Base Index" type.
3. Configure an AI data source under **Artificial Intelligence > Data Sources**, selecting an Azure AI Search source index and the knowledge base index.
4. The module will automatically sync documents from the source index to the knowledge base index with embeddings.

## Services Registered

| Service | Key | Description |
|---------|-----|-------------|
| `IDataSourceVectorSearchService` | `AzureAISearch` | Performs vector searches against Azure AI Search knowledge base indexes |
| `IDataSourceDocumentReader` | `AzureAISearch` | Reads source documents from Azure AI Search indexes in batches |
| `IDataSourceFilterExecutor` | `AzureAISearch` | Executes OData filter expressions for two-phase search |
| `IIndexProfileHandler` | — | Manages Azure AI Search index fields for knowledge base indexes |
| `IDocumentIndexHandler` | — | Handles document indexing into Azure AI Search knowledge base indexes |

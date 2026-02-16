# CrestApps.OrchardCore.AI.DataSources.MongoDB

## Overview

This module provides MongoDB Atlas support for AI data source knowledge base indexes. It enables vector search and document embedding storage using MongoDB Atlas Vector Search capabilities, allowing AI tools to perform Retrieval-Augmented Generation (RAG) searches against MongoDB collections.

## Problem Solved

When AI profiles are configured with data sources, the system needs to search and retrieve relevant documents to provide context to AI models. This module handles:

- **Vector search**: Performs vector similarity searches using MongoDB Atlas Vector Search to find the most relevant documents.
- **Filter execution**: Applies OData filter expressions translated to MongoDB Atlas Vector Search filter syntax.
- **OData filter translation**: Translates user OData filters into MongoDB-compatible BSON filter documents.
- **Document reading**: Reads source documents from MongoDB collections in batches for efficient indexing.

## Features

- **Feature ID**: `CrestApps.OrchardCore.AI.DataSources.MongoDB`
- **Dependencies**: `CrestApps.OrchardCore.AI.DataSources`

## Usage

1. Enable the `AI Data Sources` feature and the `AI Data Sources - MongoDB` feature in the Orchard Core admin dashboard.
2. Configure MongoDB Atlas Vector Search indexes for your collections.
3. Configure an AI data source under **Artificial Intelligence > Data Sources**, selecting a MongoDB source index and a knowledge base index.
4. The module will handle vector search operations against your MongoDB Atlas indexes.

## Services Registered

| Service | Key | Description |
|---------|-----|-------------|
| `IDataSourceVectorSearchService` | `MongoDB` | Performs vector searches using MongoDB Atlas Vector Search |
| `IDataSourceDocumentReader` | `MongoDB` | Reads source documents from MongoDB collections in batches |
| `IODataFilterTranslator` | `MongoDB` | Translates OData filter expressions into MongoDB BSON filter documents |

## Note

MongoDB Atlas must be configured with vector search indexes to use this module. The module requires the `MongoDB.Driver` package for database connectivity.

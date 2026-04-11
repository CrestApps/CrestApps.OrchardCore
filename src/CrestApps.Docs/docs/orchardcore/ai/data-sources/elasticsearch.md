---
sidebar_label: Elasticsearch
sidebar_position: 2
title: AI Data Sources - Elasticsearch
description: Elasticsearch support for AI data source knowledge base indexes with vector search and RAG capabilities.
---

| | |
| --- | --- |
| **Feature Name** | AI Data Sources - Elasticsearch |
| **Feature ID** | `CrestApps.OrchardCore.AI.DataSources.Elasticsearch` |

Adds Elasticsearch support for AI data source document embeddings, vector search, and indexing.

## Overview

This module provides Elasticsearch support for AI data source knowledge base indexes. It enables vector search and document embedding storage using Elasticsearch's k-NN capabilities, allowing AI tools to perform Retrieval-Augmented Generation (RAG) searches against Elasticsearch indexes.

When AI profiles are configured with data sources, the system needs to search and retrieve relevant documents to provide context to AI models. This module handles:

- **Index schema management** — Creates and manages Elasticsearch mappings for knowledge base indexes with dense vector fields for embeddings.
- **Document indexing** — Indexes source documents with their embeddings into Elasticsearch knowledge base indexes.
- **Vector search** — Performs k-NN similarity searches to find the most relevant documents for a given query.
- **Filter execution** — Executes Elasticsearch DSL filter queries translated from OData for filtered vector search.
- **OData filter translation** — Translates OData filter expressions into Elasticsearch-compatible bool queries targeting root-level filter fields.
- **Batch document reading** — Reads source documents from Elasticsearch indexes in batches for efficient indexing.

## Getting Started

1. Enable the **AI Data Sources - Elasticsearch** feature in the Orchard Core admin dashboard.
2. Create an Elasticsearch knowledge base index via **Search > Indexes** using the "AI Knowledge Base Index" type.
3. Configure an AI data source under **Artificial Intelligence > Data Sources**, selecting an Elasticsearch source index and the knowledge base index.
4. The module will automatically sync documents from the source index to the knowledge base index with embeddings.

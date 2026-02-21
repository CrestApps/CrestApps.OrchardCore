---
sidebar_label: Azure AI Search
sidebar_position: 4
title: AI Documents (Azure AI Search)
description: Azure AI Search integration as an embedding and search provider for the AI Documents feature.
---

This module integrates Azure AI Search as an embedding and search provider for the AI Documents feature.

## Features

- **Embedding Support**: Uses Azure AI Search for storing vector embeddings of document chunks
- **Search Integration**: Retrieves relevant document chunks using Azure AI Search vector search

## Getting Started

1. Enable the `AI Documents (Azure AI Search)` feature in Orchard Core admin
2. Configure an Azure AI Search connection and create an index **Search > Indexing**. Add a new "Chat Interaction Documents (Elasticsearch)" index.
3. Select the index in **Settings > Chat Interaction**

## Notes

The `AI Documents` feature is provided on demand and will only be enabled when a feature that requires it is enabled (for example, this Azure AI Search provider feature). Ensure you enable this provider feature if you want to configure document indexing with Azure AI Search.

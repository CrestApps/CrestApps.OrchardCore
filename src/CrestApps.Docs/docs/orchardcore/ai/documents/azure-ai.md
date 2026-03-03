---
sidebar_label: Azure AI Search
sidebar_position: 4
title: AI Documents (Azure AI Search)
description: Azure AI Search integration as an embedding and search provider for the AI Documents feature.
---

| | |
| --- | --- |
| **Feature Name** | AI Documents indexing using Azure AI Search |
| **Feature ID** | `CrestApps.OrchardCore.AI.Documents.AzureAI` |

Provides services to index AI Documents in Azure AI Search indexes.

## Overview

This module integrates Azure AI Search as an embedding and search provider for the AI Documents feature.

- **Embedding Support**: Uses Azure AI Search for storing vector embeddings of document chunks
- **Search Integration**: Retrieves relevant document chunks using Azure AI Search vector search

## Getting Started

1. Enable the `AI Documents indexing using Azure AI Search` feature in Orchard Core admin.
2. Configure an Azure AI Search connection and create an index via **Search > Indexing**.
3. Select the index in **Settings > Chat Interaction**.

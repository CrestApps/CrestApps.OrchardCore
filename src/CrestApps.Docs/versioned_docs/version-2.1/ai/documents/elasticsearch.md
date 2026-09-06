---
sidebar_label: Elasticsearch
sidebar_position: 5
title: AI Documents (Elasticsearch)
description: Elasticsearch integration as an embedding and search provider for the AI Documents feature.
---

| | |
| --- | --- |
| **Feature Name** | AI Documents indexing using Elasticsearch |
| **Feature ID** | `CrestApps.OrchardCore.AI.Documents.Elasticsearch` |

Provides services to index AI Documents in Elasticsearch indexes.

## Overview

This module integrates Elasticsearch as an embedding and search provider for the AI Documents feature.

- **Embedding Support**: Uses Elasticsearch for storing vector embeddings of document chunks
- **Search Integration**: Retrieves relevant document chunks using Elasticsearch vector search

## Getting Started

1. Enable the `AI Documents indexing using Elasticsearch` feature in Orchard Core admin.
2. Configure an Elasticsearch connection and create an index via **Search > Indexing**.
3. Select the index in **Settings > Chat Interaction**.

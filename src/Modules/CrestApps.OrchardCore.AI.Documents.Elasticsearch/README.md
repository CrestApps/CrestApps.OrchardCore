# AI Documents (Elasticsearch)

This module integrates Elasticsearch as an embedding and search provider for the AI Documents feature.

## Features

- **Embedding Support**: Uses Elasticsearch for storing vector embeddings of document chunks
- **Search Integration**: Retrieves relevant document chunks using Elasticsearch vector search

## Getting Started

1. Enable the `AI Documents (Elasticsearch)` feature in Orchard Core admin
2. Configure an Elasticsearch connection and create an index **Search > Indexing**. Add a new "Chat Interaction Documents (Elasticsearch)" index.
3. Select the index in **Settings > Chat Interaction**

## Notes

The `AI Documents` feature is provided on demand and will only be enabled when a feature that requires it is enabled (for example, this Elasticsearch provider feature). Ensure you enable this provider feature if you want to configure document indexing with Elasticsearch.

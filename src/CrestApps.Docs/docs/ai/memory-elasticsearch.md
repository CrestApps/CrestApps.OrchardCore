---
sidebar_label: AI Memory Elasticsearch
sidebar_position: 10
title: AI Memory with Elasticsearch
description: Elasticsearch indexing and vector search support for the Orchard Core AI Memory feature.
---

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.AI.Memory.Elasticsearch` |
| **Feature Name** | AI Memory indexing using Elasticsearch |

Provides Elasticsearch indexing and vector search support for [AI Memory](./memory).

## When to enable this module

Enable this module when you want authenticated user memories to be stored in the core AI Memory feature and indexed into Elasticsearch for semantic lookup.

This module depends on:

- [AI Memory](./memory)
- `OrchardCore.Indexing`
- `OrchardCore.Elasticsearch`

## What this provider adds

- an Elasticsearch-backed memory index profile type
- vector indexing for saved user memories
- semantic memory search using the embedding deployment selected for the memory index
- the provider-specific wiring needed by the shared AI Memory tools and preemptive retrieval pipeline

## Setup

1. Enable **AI Memory indexing using Elasticsearch**.
2. Enable the Orchard Core Elasticsearch feature that provides search indexes.
3. Open **Search -> Indexing** and create an **AI Memory (Elasticsearch)** index.
4. Select the embedding deployment that should be used for memory embeddings and queries.
5. Go to **Settings -> Artificial Intelligence -> Memory** and choose that index as the **Index profile**.

## Operational notes

- The embedding deployment is chosen when the memory index is created and then treated as stable so the stored vectors keep the expected dimensions.
- Changing the master memory index after production data exists usually requires a full re-index or data migration plan.
- All reads and writes remain scoped to the current authenticated user even though the vectors are stored in a shared search service.

## Related docs

- [AI Memory](./memory)
- [Elasticsearch data sources](./data-sources/elasticsearch)
- [Documents with Elasticsearch](./documents/elasticsearch)

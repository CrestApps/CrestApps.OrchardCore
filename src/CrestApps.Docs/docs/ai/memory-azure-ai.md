---
sidebar_label: AI Memory Azure AI
sidebar_position: 9
title: AI Memory with Azure AI Search
description: Azure AI Search indexing and vector search support for the Orchard Core AI Memory feature.
---

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.AI.Memory.AzureAI` |
| **Feature Name** | AI Memory indexing using Azure AI Search |

Provides Azure AI Search indexing and vector search support for [AI Memory](./memory).

## When to enable this module

Enable this module when you want authenticated user memories to be stored in the core AI Memory feature and indexed into Azure AI Search for semantic lookup.

This module depends on:

- [AI Memory](./memory)
- `OrchardCore.Indexing`
- `OrchardCore.AzureAI`

## What this provider adds

- an Azure AI Search-backed memory index profile type
- vector indexing for saved user memories
- semantic memory search using the embedding deployment selected for the memory index
- the provider-specific wiring needed by the shared AI Memory tools and preemptive retrieval pipeline

## Setup

1. Enable **AI Memory indexing using Azure AI Search**.
2. Enable the Orchard Core Azure AI Search feature that provides search indexes.
3. Open **Search -> Indexing** and create an **AI Memory (Azure AI Search)** index.
4. Select the embedding deployment that should be used for memory embeddings and queries.
5. Go to **Settings -> Artificial Intelligence -> Memory** and choose that index as the **Index profile**.

## Operational notes

- The embedding deployment is chosen when the memory index is created and then treated as stable so the stored vectors keep the expected dimensions.
- Changing the master memory index after production data exists usually requires a full re-index or data migration plan.
- All reads and writes remain scoped to the current authenticated user even though the vectors are stored in a shared search service.

## Related docs

- [AI Memory](./memory)
- [Azure AI Search data sources](./data-sources/azure-ai)
- [Azure AI Inference provider](./providers/azure-ai-inference)
- [Azure OpenAI provider](./providers/azure-openai)

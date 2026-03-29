---
sidebar_label: AI Memory
sidebar_position: 8
title: AI Memory Module
description: Persistent, user-scoped AI memory for AI Profiles and Chat Interactions.
---

| | |
| --- | --- |
| **Feature Name** | AI Memory |
| **Feature ID** | `CrestApps.OrchardCore.AI.Memory` |

Provides persistent, user-scoped AI memory so the AI can remember durable, non-sensitive preferences and background details for authenticated users across multiple conversations.

Provider-specific indexing support is split into separate modules:

| Module | Feature ID |
| --- | --- |
| AI Memory (Core) | `CrestApps.OrchardCore.AI.Memory` |
| AI Memory (Azure AI Search) | `CrestApps.OrchardCore.AI.Memory.AzureAI` |
| AI Memory (Elasticsearch) | `CrestApps.OrchardCore.AI.Memory.Elasticsearch` |

## Overview

The AI Memory module adds a private memory layer for the currently signed-in user. Memories are stored as first-class records in the tenant and indexed into a dedicated **master memory index** so the AI can search or list them later.

When preemptive RAG is enabled, Orchard Core also performs an upfront semantic search across the authenticated user's memory before the model answers. Matching memories are injected into the system prompt as private background context, similar to the existing document and data-source preemptive retrieval flow. The `search_user_memories` tool remains available for follow-up lookups when the initial memory context is not enough.

Every memory is scoped to a single `userId`. Searches and updates are always filtered to the current authenticated user, which prevents memories from leaking between users.

## What AI Memory Is For

Use memory for durable, non-sensitive information such as:

- response style preferences
- formatting preferences
- active projects or workstreams
- recurring topics the user commonly asks about
- durable interests or reference areas
- long-lived product or workflow preferences
- stable background context that the user explicitly wants remembered

Do **not** use memory for secrets or sensitive information.

## Sensitive Data Rules

AI Memory should never store:

- passwords
- API keys
- access or refresh tokens
- credit card numbers
- Social Security numbers
- private keys
- connection strings

The built-in memory save tool rejects obvious sensitive patterns, and the orchestration guidance also instructs the model not to save confidential data such as passwords, API keys, tokens, credit card numbers, Social Security numbers, private keys, or connection strings.

## How It Works

The feature adds four built-in system tools for the current authenticated user:

- **Search User Memories** — semantic search across the user's saved memories
- **List User Memories** — enumerate the user's existing memories
- **Save User Memory** — create or update a named memory entry
- **Remove User Memory** — remove a saved memory entry when it should be forgotten

The orchestration prompt instructs the model to call **Save User Memory** in the same turn before it claims it will remember durable facts such as the user's name, role, or stable preferences. When the user later asks about stable remembered details, the system first attempts preemptive memory retrieval when that tenant-level setting is enabled and still instructs the model to search or list memory before saying the information is unknown if more lookup is needed.

Memory tools are only force-included for requests where user memory is enabled for the current authenticated user. This keeps memory tools available when needed without making them global for unrelated orchestration requests.

Each memory is stored as a single record in the configured master index. The index stores the memory ID, `userId`, a stable memory `name`, a semantic `description`, content, timestamp, and embedding vector.

Memory indexing is triggered from deferred AI memory catalog entry handlers instead of individual tools. This keeps the tenant store and external index in sync even when memory entries are created, updated, or deleted from other code paths such as admin actions or future integrations.

Use short stable names for durable facts so the system can update and locate the same memory later. For example, store a remembered preferred name using a key like `preferred_name`.

Each saved memory should also include a short description explaining what the value represents. For example:

- `name`: `preferred_name`
- `description`: `The user's preferred name.`
- `content`: `Mike Alhayek`

The system uses the memory name together with the description for semantic search embeddings because the raw content alone may not be meaningful enough for retrieval.

Within the tenant store, memory queries are always scoped by `userId` and ordered by timestamps so the feature remains user-isolated even before vector search is involved. The tenant-side index also stores the memory name for efficient exact lookup when updating or removing a named memory.

## Configuration

### 1. Enable a memory indexing provider

Enable one of the provider modules in Orchard Core:

- **AI Memory (Azure AI Search)** for Azure AI Search-backed memory indexes
- **AI Memory (Elasticsearch)** for Elasticsearch-backed memory indexes

The core **AI Memory** feature is enabled by dependency when one of those provider features is enabled.

### 2. Create a memory index

1. Enable the corresponding Orchard Core search feature for your provider.
2. Navigate to **Search → Indexing**.
3. Create a new index using either:
   - **AI Memory (Azure AI Search)**
   - **AI Memory (Elasticsearch)**
4. Choose the embedding connection that should be used for memory indexing and search.

### 3. Configure global memory settings

Navigate to **Settings → Artificial Intelligence → Memory** and configure:

- **Index profile** — the master memory index used for storing memories
- **Default top N** — the default number of matching memories returned by searches

You can leave **Index profile** empty while you are still setting up unrelated AI features such as Copilot. Memory retrieval and indexing stay inactive until you select a valid memory index profile.

Preemptive memory retrieval itself is controlled separately under **Settings → Artificial Intelligence → General** through **Enable Preemptive Memory Retrieval**. This lets you keep user memory tools enabled while turning off the upfront memory injection step for the tenant.

:::warning
After you start storing production memory data, avoid changing the configured master index unless you plan a full re-index.
:::

### 4. Enable memory where you need it

#### AI Profiles

AI Profiles expose **Enable User Memory** in the **Interactions** card of the profile editor.

- Default: **disabled**
- Scope: per profile

This lets you opt in only on the profiles where cross-session personalization is appropriate.

#### AI Profile Templates

Profile-source AI Templates also expose **Enable User Memory** in the **Interactions** card.

- Default: **disabled**
- Scope: persisted with the template and applied to new profiles created from it

This makes it easy to preconfigure memory behavior when you create reusable chat profile templates.

#### Chat Interactions

Chat Interactions add a site setting under **Settings → Artificial Intelligence → Chat Interactions**:

- **Enable User Memory**
- Default: **enabled**

This enables private memory for authenticated Chat Interaction users. Memory retrieval and indexing only become active after a valid memory index profile is configured.

## Authentication Behavior

AI Memory is only available to authenticated users.

- Anonymous users do not receive memory tools
- Anonymous users cannot search, list, or save memories
- All memory reads and writes are filtered by the current `ClaimTypes.NameIdentifier`

## Clearing Your Memory

Users can clear their own saved AI memory from their user profile editor.

- The clear option is only shown when a user is editing their **own** profile
- A confirmation checkbox is required before memory is removed
- Clearing memory removes the user's stored memory records and deletes their indexed memory documents from the configured master memory index

## Related Features

- [AI Chat Interactions](./chat-interactions)
- [AI Documents](./documents/)
- [AI Data Sources](./data-sources/)

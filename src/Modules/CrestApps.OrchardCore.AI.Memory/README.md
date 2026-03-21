# AI Memory

Provides persistent, user-scoped memory for AI Profiles and Chat Interactions.

The core feature stores non-sensitive user preferences and durable details per authenticated user and scopes all retrieval to the current user ID.

Provider-specific indexing support lives in:

- `CrestApps.OrchardCore.AI.Memory.AzureAI`
- `CrestApps.OrchardCore.AI.Memory.Elasticsearch`

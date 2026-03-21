# AI Memory

Provides persistent, user-scoped memory for AI Profiles and Chat Interactions.

The core feature stores durable user context per authenticated user, such as preferences, active projects, recurring topics, interests, and other reusable background details, while scoping all retrieval to the current user ID.

Provider-specific indexing support lives in:

- `CrestApps.OrchardCore.AI.Memory.AzureAI`
- `CrestApps.OrchardCore.AI.Memory.Elasticsearch`

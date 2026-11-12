# Artificial Intelligence

Provides AI services.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| AI Services | `CrestApps.OrchardCore.AI` | Provides AI services. Enabled by the features that depend on it. |
| AI Chat Services | `CrestApps.OrchardCore.AI.Chat.Core` | Provides all the necessary services to enable chatting with AI models using profiles. Enabled by the features that depend on it. |
| AI Chat WebAPI | `CrestApps.OrchardCore.AI.Chat.Api` | Provides a RESTful API for interacting with the AI chat. |
| AI Connection Management | `CrestApps.OrchardCore.AI.ConnectionManagement` | Provides user interface to manage AI connections. |
| AI Tool Instances | `CrestApps.OrchardCore.AI.ToolInstances` | Provides user interface to manage AI tool instances. |
| AI Personas | `CrestApps.OrchardCore.AI.Personas` | Reusable characters for AI profiles and chat interactions, with their own instructions, assistant name, avatar and voice. Profiles link to a persona, so an edit applies to all of them from the next message. |
| AI Quality | `CrestApps.OrchardCore.AI.Quality` | Simulation test cases you run against AI profiles from the admin or a CI pipeline, QA cohorts that score a sample of real conversations, the AI QA Dashboard report (with Reports on), and alert rules that watch the AI's metrics. |
| Custom LLM (WebSocket) | `CrestApps.OrchardCore.AI.CustomLlm` | A provider that sends each turn to your own server over a WebSocket and streams its reply back, for a model or agent you host that does not speak an OpenAI-compatible API. |
| AI Webhooks | `CrestApps.OrchardCore.AI.Webhooks` | Posts signed AI events (a conversation starting, ending, analyzed, transferred or scored, an alert triggering or resolving) to outside addresses, with retries and a delivery log. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)
- `OrchardCore.Liquid`
- [`CrestApps.OrchardCore.AI.Prompting`](../CrestApps.OrchardCore.AI.Prompting/README.md)

## Extension points

- `IAIProfileTemplateApplicationHandler` (CrestApps.OrchardCore.AI.Core): Lets a module finish a profile that was built from a template, for example by copying data the template owns so the new profile gets its own.
- `IAIApiKeyStore` (CrestApps.OrchardCore.AI.Core): Stores API keys.
- `IAIConversationClosedHandler` (CrestApps.OrchardCore.AI.Core): Runs when an AI conversation is closed, after its analysis and contact memory.
- `IAIConversationEvaluator` (CrestApps.OrchardCore.AI.Core): Scores a conversation against criteria.
- `IAIChatSessionCloser` (CrestApps.OrchardCore.AI.Core): Closes an AI conversation as soon as it is known to be over, such as when a call hangs up, instead of waiting for the profile's inactivity timeout.
- and 15 more contracts in the same projects.

## Completion API limits

- Every API key, and every signed-in API caller, has a quota of model calls on `api/ai/completion/chat` and `api/ai/completion/utility`: 1,000 an hour by default. Change it under `CrestApps_Throttling:AttemptPolicies:ai-api-completions` (`PermitLimit`, `Window`). A caller over the quota gets `429` with `Retry-After`.
- With Orchard Core's Rate Limits feature on, the same routes carry the `ai-completion` rate-limit group (60 requests a minute per caller by default).

## Documentation

See [AI Services](../../CrestApps.Docs/docs/ai/overview.md) in the technical manual.

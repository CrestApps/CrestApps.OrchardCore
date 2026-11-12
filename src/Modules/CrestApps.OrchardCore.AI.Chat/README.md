# Artificial Intelligence

Provides UI to interact with AI models using the profiles.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| AI Chat | `CrestApps.OrchardCore.AI.Chat` | Provides UI to interact with AI models using the profiles. |
| AI Chat Admin Widget | `CrestApps.OrchardCore.AI.Chat.AdminWidget` | Provides a floating AI chat widget on every admin page, allowing users to interact with a predefined AI profile. |
| AI Chat Session Analytics | `CrestApps.OrchardCore.AI.Chat.Analytics` | Tracks chat session usage metrics (unique visitors, handle time, containment rate, abandonment rate) and reports them, with the extracted data and conversion goals, under Reports. |
| AI Usage Analytics | `CrestApps.OrchardCore.AI.UsageAnalytics` | Records the usage of every request sent to an AI provider (tokens, audio, images and automated voice call summaries) and provides the AI Usage Analytics report. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `OrchardCore.Liquid`
- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)
- `CrestApps.OrchardCore.AI.Chat.Core`
- `OrchardCore.SignalR`
- [`CrestApps.OrchardCore.AI`](../CrestApps.OrchardCore.AI/README.md)
- [`CrestApps.OrchardCore.AI.Agent`](../CrestApps.OrchardCore.AI.Agent/README.md)

## Extension points

- `IAIChatHubClient` (CrestApps.OrchardCore.AI.Chat.Core): OrchardCore-specific extension of the framework `IAIChatHubClient`.
- `IChatHubClient` (CrestApps.OrchardCore.AI.Chat.Core): Defines the common SignalR client methods shared by both AI Chat and Chat Interaction hubs.

## Visitor address

- The anonymous visitor's address, used by the chat rate limits, is taken from the connection, not from a client's `X-Forwarded-For` header. Behind a proxy, enable Orchard Core's Reverse Proxy feature so the connection carries the client's address.

## Images in chat output

Pages that render chat send `Content-Security-Policy: img-src 'self' data: blob:`, so model output can't load an image from another host (an image URL can carry the conversation out of the page). Allow more sources, such as a media CDN, with `CrestApps_AI:Chat:ContentSecurityPolicy:ImageSources`, or turn it off with `CrestApps_AI:Chat:ContentSecurityPolicy:Enabled: false`. The policy is added beside any policy the site already sends.

## Documentation

See [AI Chat](../../CrestApps.Docs/docs/ai/chat.md) in the technical manual.

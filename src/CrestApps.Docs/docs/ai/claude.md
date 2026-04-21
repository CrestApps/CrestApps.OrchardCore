---
sidebar_label: Claude
sidebar_position: 7
title: Claude Integration
description: Orchard Core module guidance for the Claude-based orchestrator.
---

# Claude Integration

| | |
| --- | --- |
| **Feature Name** | AI Claude Orchestrator |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Claude` |

This module wires the shared Claude support from CrestApps.Core into Orchard Core and exposes the related Orchard settings and editors.

## Enable the feature

Enable **AI Claude Orchestrator** when you want Claude to appear as an Orchard-managed orchestrator option for:

- AI profiles
- AI profile templates
- chat interactions

## Orchard configuration

Claude configuration can come from both shell configuration and tenant site settings.

### appsettings.json

The module binds shared Claude options from:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Claude": {
        "BaseUrl": "https://api.anthropic.com",
        "DefaultModel": "claude-sonnet-4-5"
      }
    }
  }
}
```

### Site settings

In Orchard Core, go to **Settings -> Artificial Intelligence -> Claude**.

From there you can configure:

- authentication type
- API key
- base URL
- default model

Tenant site settings override the shell configuration for values such as the base URL and default model. When API-key authentication is used, the key is stored encrypted.

## How Orchard users work with Claude

Once the feature is configured:

- AI profile editors can select Claude models
- AI profile template editors can store Claude-related defaults
- chat interaction editors can pick Claude-backed model settings

If the site is not configured with an API key, the Claude model selectors remain effectively unavailable.

## Shared framework documentation

Detailed Claude runtime guidance lives in **CrestApps.Core**:

- [Claude](https://core.crestapps.com/docs/core/claude)

## Related Orchard docs

- [AI Services](overview)
- [AI Chat](chat)
- [AI Chat Interactions](chat-interactions)

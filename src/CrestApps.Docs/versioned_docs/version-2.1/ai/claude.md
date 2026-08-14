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

## Capabilities

- tenant-level Claude settings under **Settings -> Artificial Intelligence -> Claude**
- encrypted Anthropic API key storage
- Claude model discovery from the Anthropic models API
- per-item Claude model overrides for AI Profiles, AI Profile templates, and Chat Interactions
- per-item **Effort level** overrides (`Default`, `Low`, `Medium`, `High`)
- template-to-profile propagation for Claude session settings

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

When API key authentication is enabled, the stored API key is encrypted at rest. The editor also uses the configured Claude connection to populate the available model list from Anthropic so Orchard users can pick a model instead of typing one manually.

Tenant site settings override the shell configuration for values such as the base URL and default model. When API-key authentication is used, the key is stored encrypted.

## How Orchard users work with Claude

Once the feature is configured:

- AI profile editors can select Claude models and an **Effort level**
- AI profile template editors can store Claude model and **Effort level** defaults
- chat interaction editors can pick Claude-backed model settings and an **Effort level**

When you create a new AI Profile from a profile-source template, the saved Claude settings are copied to the generated profile so the template can act as a reusable Claude preset.

If the site is not configured with an API key, the Claude model selectors remain effectively unavailable.

## Shared framework documentation

Detailed Claude runtime guidance lives in **CrestApps.Core**:

- [Claude](https://core.crestapps.com/docs/core/claude)

## Related Orchard docs

- [AI Services](overview)
- [AI Chat](chat)
- [AI Chat Interactions](chat-interactions)

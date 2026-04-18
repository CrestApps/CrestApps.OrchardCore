---
sidebar_label: Claude Integration
sidebar_position: 6
title: Claude Integration
description: Claude-based orchestrator and tenant settings for AI chat sessions in Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | AI Claude Orchestrator |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Claude` |

Provides a Claude-based orchestrator for AI chat sessions in Orchard Core.

## Summary

This module adds direct Anthropic Claude orchestration to Orchard Core through `CrestApps.Core.AI.Claude`. It includes tenant-level settings, model discovery, runtime availability checks, and per-item Claude configuration for AI Profiles, AI Profile templates, and Chat Interactions.

## Capabilities

- **Claude Orchestration**: Runs chat sessions through `ClaudeOrchestrator`
- **Tenant Settings**: Configure Claude under **Settings -> Artificial Intelligence -> Claude**
- **Encrypted API Key Storage**: Stores the Claude API key with ASP.NET Core Data Protection
- **Model Discovery**: Loads available Claude models from the Anthropic Models API when an API key is configured
- **Per-Item Model Overrides**: AI Profiles, AI Profile templates, and Chat Interactions can override the default Claude model
- **Reasoning Effort Selection**: AI Profiles, AI Profile templates, and Chat Interactions expose `Default`, `Low`, `Medium`, and `High` effort levels
- **Template Propagation**: Claude model and effort settings saved on a Profile-source template are copied to the generated AI Profile

## Configuration

Open **Settings -> Artificial Intelligence -> Claude** and configure:

- **Authentication type** - `NotConfigured` or `ApiKey`
- **Base URL** - Defaults to `https://api.anthropic.com`
- **API key** - Stored encrypted
- **Default model** - Optional tenant-level default used when an item does not override the model

When an API key is already stored, the settings editor attempts to load the available Claude models and switches the default-model field to a dropdown.

## Usage

### AI Profiles

1. Select **Claude** from the Orchestrator dropdown.
2. The editor shows a **Claude configuration** section.
3. Choose a model override or leave it empty to use the tenant default.
4. Select the **Effort level** to control Claude reasoning effort.

### Chat Interactions

1. Select **Claude** from the Orchestrator picker.
2. The interaction settings panel shows the Claude model and **Effort level** fields.
3. Values are saved through the generic chat interaction settings pipeline and stored as `ClaudeSessionMetadata`.

### AI Profile Templates

1. Edit a template with **Source = Profile**.
2. Select **Claude** as the orchestrator.
3. Save the Claude model override and **Effort level** on the template.
4. When the template is applied, the generated AI Profile receives the same Claude metadata.

## Architecture

- `ClaudeOptionsConfiguration` maps Orchard Core tenant settings onto `ClaudeOptions`
- `ClaudeSettingsDisplayDriver` provides the tenant settings UI and secure API-key persistence
- `AIProfileClaudeDisplayDriver`, `AIProfileTemplateClaudeDisplayDriver`, and `ChatInteractionClaudeDisplayDriver` surface Claude-specific model and effort settings
- `ClaudeOrchestratorAvailabilityProvider` hides the orchestrator from availability-aware UIs until Claude has been configured

## Related

- [AI Templates](profile-templates)
- [Chat Interactions](chat-interactions)
- [Artificial Intelligence Suite](./)

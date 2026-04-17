# CrestApps.OrchardCore.AI.Chat.Claude

Adds the Claude orchestrator to CrestApps.OrchardCore.

## Features

- Tenant-level Claude settings under **Settings -> Artificial Intelligence**
- Anthropic API key authentication with encrypted storage
- Claude model discovery from the Anthropic Models API
- Claude model and reasoning-effort overrides for AI Profiles, Chat Interactions, and AI Profile-source templates
- Template-to-profile propagation for Claude session metadata

## Configuration

1. Enable **AI Claude Orchestrator**.
2. Open **Settings -> Artificial Intelligence -> Claude**.
3. Choose **API key** authentication.
4. Save the API key, optional base URL override, and optional default model.

## Usage

Select **Claude** as the orchestrator on an AI Profile, Chat Interaction, or AI Profile-source template. When Claude is configured, the editor surfaces a model override and an effort-level selector.

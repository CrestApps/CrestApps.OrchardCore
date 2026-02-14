# AI Copilot Orchestrator (`CrestApps.OrchardCore.AI.Chat.Copilot`)

## Summary

Provides a GitHub Copilot SDK-based orchestrator for AI chat sessions in Orchard Core. This module integrates the [GitHub Copilot SDK for .NET](https://github.com/github/copilot-sdk) as an alternative orchestrator alongside the default Progressive Tool Orchestrator.

## Features

- **Copilot-Powered Orchestration**: Delegates planning, tool selection, and execution to the GitHub Copilot agent runtime.
- **Full Tool Registry Integration**: Discovers and uses all registered local and system tools from the OrchardCore AI Tool Registry.
- **Native MCP Support**: MCP connections are configured on the Copilot session so that Copilot can manage MCP tools natively.
- **Data Source Support**: Data source context (documents) is handled by the orchestration context pipeline before reaching the orchestrator.
- **Streaming Responses**: Supports real-time streaming of AI responses.
- **Per-Profile Model Selection**: The model/deployment is configured per AI Profile or Chat Interaction — no global model setting needed.

## Prerequisites

- **GitHub Copilot CLI** must be installed and authenticated on the server. See the [Copilot CLI installation guide](https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli).
- A valid **GitHub Copilot subscription** (or BYOK configuration).

## Usage

Once the module is enabled:

1. Go to **AI Profiles** or **Chat Interactions** in the admin dashboard.
2. If multiple orchestrators are registered, an **Orchestrator** dropdown will appear.
3. Select **GitHub Copilot Orchestrator** to use Copilot for that profile or interaction.
4. The model/deployment configured on the profile or interaction is used for the Copilot session.

If only one orchestrator is registered, it is used automatically without showing the selector.

## Dependencies

- `CrestApps.OrchardCore.AI` — Core AI module
- `GitHub.Copilot.SDK` — Official GitHub Copilot SDK for .NET

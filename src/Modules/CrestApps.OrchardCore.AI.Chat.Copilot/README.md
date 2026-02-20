# AI Copilot Orchestrator (`CrestApps.OrchardCore.AI.Chat.Copilot`)

## Summary

Provides a GitHub Copilot SDK-based orchestrator for AI chat sessions in Orchard Core. This module integrates the [GitHub Copilot SDK for .NET](https://github.com/github/copilot-sdk) as an alternative orchestrator alongside the default Progressive Tool Orchestrator.

## Features

- **Copilot-Powered Orchestration**: Delegates planning, tool selection, and execution to the GitHub Copilot agent runtime
- **Full Tool Registry Integration**: Discovers and uses all registered local and system tools from the OrchardCore AI Tool Registry
- **Native MCP Support**: MCP connections are described in the system message so Copilot is aware of available servers
- **Data Source Support**: Data source context (documents) is handled by the orchestration context pipeline before reaching the orchestrator
- **Streaming Responses**: Supports real-time streaming of AI responses via `AssistantMessageDeltaEvent`
- **Per-Profile Model Selection**: The model is configured per AI Profile or Chat Interaction
- **Allow All Tool Executions**: Configurable checkbox that passes the `--allow-all` flag via `CliArgs`
- **GitHub OAuth Authentication**: User-scoped or profile-scoped authentication with GitHub for Copilot access
- **Profile-Level Credential Storage**: AI Profiles can store GitHub credentials so all chat sessions using the profile share the same token
- **Popup OAuth Flow**: AI Profile editing uses a popup window for GitHub authentication to avoid losing unsaved form data
- **Extensible Settings Pipeline**: Chat Interaction settings are saved via `IChatInteractionSettingsHandler`, allowing Copilot-specific fields to be handled without modifying core chat infrastructure

## Prerequisites

- A valid **GitHub Copilot subscription**
- **GitHub OAuth App** configured for authentication
- **GitHub Copilot CLI** installed and available in PATH (bundled with the SDK NuGet package)

## Configuration

### GitHub OAuth Setup

1. Create a GitHub OAuth App:
   - Go to GitHub Settings → Developer settings → OAuth Apps
   - Click "New OAuth App"
   - Set Authorization callback URL to: `https://your-domain.com/copilot/OAuthCallback`
   - Note the Client ID and Client Secret

2. Configure Copilot settings in Orchard Core:
   - Go to Configuration → Settings → Copilot
   - Enter your GitHub OAuth App Client ID
   - Enter your GitHub OAuth App Client Secret (stored encrypted)
   - Save settings

3. Required OAuth scopes:
   - `user:email` - To identify the user
   - `read:org` - To access Copilot on behalf of the user

## Usage

### Chat Interactions

When a Chat Interaction is configured with the Copilot orchestrator:

1. The user must be authenticated with GitHub (via the Sign in with GitHub button)
2. The **Copilot Model** dropdown shows available models from the user's GitHub account
3. The first model is auto-selected; there is no "Default model" option since model availability depends on the user's access
4. **Allow All** checkbox is checked by default, enabling the `--allow-all` flag for the Copilot CLI process
5. Settings are saved automatically via SignalR when any form field changes, using the extensible `IChatInteractionSettingsHandler` pipeline

### AI Profiles

When creating or editing an AI Profile with the Copilot orchestrator:

1. Select **GitHub Copilot Orchestrator** from the Orchestrator dropdown
2. The Connection and Deployment fields are hidden (not used by Copilot)
3. The **Copilot Configuration** section appears with:
   - **GitHub Authentication** — Click "Sign in with GitHub" to authenticate via a popup window (your form data is preserved)
   - **Credential Warning** — If already connected, a warning explains that your GitHub credentials will be shared with all users chatting via this AI Profile
   - **Copilot Model** — Select which model to use (GPT-4o, Claude Sonnet, etc.)
   - **Allow All** — Checkbox (default checked) to run with `--allow-all` flag
4. When the profile is saved, your encrypted GitHub credentials are stored on the profile entity so any chat session using this profile can authenticate without requiring individual user tokens

### Authentication Flow

**Chat Interactions (user-scoped)**:
1. Click "Sign in with GitHub" in the Copilot section
2. Authorize the application in the popup window
3. Your access token is stored on your user account (encrypted)
4. Each user authenticates individually

**AI Profiles (profile-scoped)**:
1. Click "Sign in with GitHub" in the AI Profile editor (opens in a popup)
2. Authorize the application — the popup closes and the page updates automatically
3. When you save the AI Profile, your encrypted credentials are copied to the profile
4. Any user chatting via this profile uses the stored credentials (not their own)

## Architecture

### Extensible Settings Pipeline

The module uses `IChatInteractionSettingsHandler` to decouple Copilot-specific settings from the core chat infrastructure:

- `CopilotChatInteractionSettingsHandler` reads `copilotModel` and `isAllowAll` from the generic form data and stores them as `CopilotSessionMetadata` on the interaction entity
- Adding new orchestrator-specific fields does not require changes to `chat-interaction.js` or `ChatInteractionHub`

### Orchestration Context Flow

1. `CopilotOrchestrationContextHandler` (implements `IOrchestrationContextHandler`) reads `CopilotSessionMetadata` from the resource entity and sets it on `OrchestrationContext.Properties`
2. `CopilotOrchestrator` reads the metadata from `Properties` to configure the session model and the `--allow-all` flag
3. Authentication uses the SDK's `GithubToken` property (not environment variables) and `CliArgs` for the allow-all flag

### Credential Resolution Order

The orchestrator resolves GitHub credentials in this order:
1. **Profile-level credentials** — Encrypted tokens stored on the AI Profile via `CopilotSessionMetadata`
2. **User-level credentials** — The current HTTP user's stored GitHub OAuth tokens

## Dependencies

- `CrestApps.OrchardCore.AI` — Core AI module
- `GitHub.Copilot.SDK` — Official GitHub Copilot SDK for .NET
- `OrchardCore.Users.Core` — For user authentication
- `OrchardCore.Admin.Abstractions` — For admin configuration

## Security

- OAuth tokens are stored encrypted at rest using ASP.NET Core Data Protection
- Profile-level tokens use the same encryption protector as user-level tokens
- Tokens are never exposed in logs or client-side code
- Client secret is protected using data protection
- Popup OAuth flow prevents credentials from being exposed in URL parameters
- Profile credential warning alerts administrators that their credentials will be shared


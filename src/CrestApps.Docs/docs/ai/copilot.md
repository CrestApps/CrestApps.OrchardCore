---
sidebar_label: Copilot Integration
sidebar_position: 5
title: Copilot Integration
description: GitHub Copilot SDK-based orchestrator for AI chat sessions in Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | AI Copilot Orchestrator |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Copilot` |

Provides a GitHub Copilot SDK-based orchestrator for AI chat sessions.

## Summary

Provides a GitHub Copilot SDK-based orchestrator for AI chat sessions in Orchard Core. This module integrates the [GitHub Copilot SDK for .NET](https://github.com/github/copilot-sdk) as an alternative orchestrator alongside the default Progressive Tool Orchestrator.

## Capabilities

- **Copilot-Powered Orchestration**: Delegates planning, tool selection, and execution to the GitHub Copilot agent runtime
- **Full Tool Registry Integration**: Discovers and uses all registered local and system tools from the OrchardCore AI Tool Registry
- **Native MCP Support**: MCP connections are described in the system message so Copilot is aware of available servers
- **Data Source Support**: Data source context (documents) is handled by the orchestration context pipeline before reaching the orchestrator
- **Streaming Responses**: Supports real-time streaming of AI responses via `AssistantMessageDeltaEvent`
- **Per-Item Model Selection**: The model is configured per AI Profile, AI Profile template, or Chat Interaction
- **Model Cost Visibility**: Copilot model pickers show the model cost multiplier next to each available model (for example, `GPT-5.4 (x1)` or `Claude Opus 4.6 (x3)`)
- **Per-Session Effort Level**: AI Profiles, AI Profile templates, and Chat Interactions can override the Copilot reasoning effort level (`Default`, `Low`, `Medium`, `High`)
- **Allow All Tool Executions**: Configurable checkbox that passes the `--allow-all` flag via `CliArgs`
- **GitHub OAuth Authentication**: User-scoped or profile-scoped authentication with GitHub for Copilot access
- **Profile-Level Credential Storage**: AI Profiles can store GitHub credentials so all chat sessions using the profile share the same token
- **Popup OAuth Flow**: AI Profile editing uses a popup window for GitHub authentication to avoid losing unsaved form data
- **Extensible Settings Pipeline**: Chat Interaction settings are saved via `IChatInteractionSettingsHandler`, allowing Copilot-specific fields to be handled without modifying core chat infrastructure

## Prerequisites

- **GitHub Copilot CLI** installed and available in PATH (bundled with the SDK NuGet package)

Depending on which authentication mode you choose:

- **GitHub Signed-in User (GitHub OAuth)**: a valid **GitHub Copilot subscription** and a **GitHub OAuth App**
- **API Key (Bring your own key)**: an API key (and endpoint) for a supported model provider

## Configuration

Configure the Copilot orchestrator at **Settings → Copilot**. The main choice is which **authentication type** you want to use.

| Authentication type | When to use | What you configure |
|---|---|---|
| **Not configured** (`NotConfigured`) | You want to disable Copilot for the tenant without deleting previously entered settings | Nothing else is required; Copilot stays disabled until you select and save another authentication mode |
| **GitHub OAuth (GitHub Signed-in User)** (`GitHubOAuth`) | You want to use GitHub Copilot entitlements and user-scoped access | GitHub OAuth app (client ID/secret) and user/profile sign-in |
| **API Key (Bring your own key)** (`ApiKey`) | You want to use your own model provider credentials (no Copilot subscription required) | Provider type, base URL, API key, default model, wire format |

When **Authentication type** is set to **Not configured**, the Copilot orchestrator is disabled for the tenant. The GitHub OAuth client ID and client secret are only required when **GitHub signed-in user** is selected, and an existing stored client secret stays in place until you explicitly replace it with a new value.

### Authentication: GitHub OAuth (GitHub Signed-in User)

Use this mode when you want the orchestrator to authenticate to Copilot via GitHub and use the models available to that signed-in identity.

#### Settings (site/tenant)

- **GitHub OAuth App Client ID**
- **GitHub OAuth App Client Secret** (stored encrypted)

#### GitHub OAuth App setup

1. Create a GitHub OAuth App:
   - GitHub Settings → Developer settings → OAuth Apps → **New OAuth App**
   - **Authorization callback URL**: `https://your-domain.com/copilot/OAuthCallback`
   - Copy the **Client ID** and **Client Secret**
2. In Orchard Core: go to **Settings → Copilot** and enter the client ID/secret.

The **Client Secret** field only needs a value the first time you configure GitHub OAuth. After that, leave it empty to keep the existing encrypted secret, or enter a new value to replace it.

#### Required OAuth scopes

- `user:email` — to identify the user
- `read:org` — to access Copilot on behalf of the user

### Authentication: API Key (Bring your own key)

Use this mode when you want Copilot orchestration but prefer to call a model provider directly using your own credentials.

You only need to define “Bring your own key” once—after configuration, this document refers to it as **API Key authentication**.

#### Settings (site/tenant)

- **Provider Type** — which provider the API key targets
- **Base URL** — provider endpoint
- **API Key** — stored encrypted (optional for local providers like Ollama)
- **Default Model** — model/deployment name used for sessions
- **Wire API Format** — `completions` or `responses`
- **Azure API Version** — required when Provider Type is Azure OpenAI

#### Provider Types

- **OpenAI / OpenAI-compatible** (`openai`) — Works with OpenAI and OpenAI-compatible endpoints (Ollama, vLLM, LiteLLM, etc.). Base URL typically includes the full path, e.g. `https://api.openai.com/v1`.
- **Azure OpenAI** (`azure`) — For native Azure OpenAI endpoints (`*.openai.azure.com`). Base URL should be the resource URL (do **not** add `/openai/v1`).
- **Anthropic** (`anthropic`) — For direct Anthropic API access to Claude models. Base URL is typically `https://api.anthropic.com`.

#### Wire API Format

The **Wire API Format** controls the HTTP format used by the underlying SDK:

- **Chat Completions** (`completions`) — the default and most compatible option
- **Responses** (`responses`) — use this when targeting GPT-5 series models that support the newer responses format

#### Setup steps

1. Go to **Settings → Copilot**.
2. Set **Authentication Type** to **API Key (Bring your own key)**.
3. Choose a **Provider Type**.
4. Configure the settings listed above.
5. Save settings.

## Usage

Usage differs slightly depending on the authentication type selected in **Settings → Copilot**.

In all modes, **Allow All** is checked by default (passes `--allow-all` to the Copilot CLI) and settings are saved automatically via SignalR using the extensible `IChatInteractionSettingsHandler` pipeline.

### GitHub OAuth authentication

#### Chat Interactions

1. The user authenticates with GitHub (via the **Sign in with GitHub** button).
2. The **Copilot Model** dropdown shows available models from the user's GitHub account.
3. Each model label includes its Copilot cost multiplier when GitHub reports one, making it easier to compare premium and standard models before saving.
4. The first model is auto-selected; there is no site-level “Default model” because model availability depends on the signed-in identity.

#### AI Profiles

1. Select **GitHub Copilot Orchestrator** from the Orchestrator dropdown.
2. The Connection and Deployment fields are hidden (not used by Copilot).
3. The **Copilot Configuration** section includes GitHub sign-in, a model picker, an **Effort level** selector, and the **Allow all tool executions** option.

#### AI Profile Templates

1. Edit a template with **Source = Profile**.
2. Select **GitHub Copilot Orchestrator** from the Orchestrator dropdown.
3. The template editor shows the same Copilot model, **Effort level**, and **Allow all tool executions** fields as the AI Profile editor.
4. When you apply the template to create a profile, the saved Copilot model, reasoning effort, and allow-all flag are copied to the generated profile.

#### Credential scope

- **Chat Interactions** are typically **user-scoped**: the access token is stored on the user account (encrypted), and each user signs in individually.
- **AI Profiles** can be **profile-scoped**: when you sign in while editing a profile, the encrypted credentials can be stored on the profile so any chat session using the profile can authenticate without requiring each user to sign in.

### API Key authentication

#### Chat Interactions

- No GitHub sign-in is required.
- The session uses the **Default Model** configured in **Settings → Copilot**.
- AI Profiles, AI Profile templates, and Chat Interactions can still override the model name and **Effort level** for that specific Copilot-backed session.

#### AI Profiles

- GitHub authentication and model listing are not used.
- The profile relies on the site-level API key settings (provider type, base URL, and default model).
- AI Profile templates expose the same override fields and copy those values to generated profiles.

## Architecture

### Extensible Settings Pipeline

The module uses `IChatInteractionSettingsHandler` to decouple Copilot-specific settings from the core chat infrastructure:

- `CopilotChatInteractionSettingsHandler` reads `copilotModel`, `copilotReasoningEffort`, and `isAllowAll` from the generic form data and stores them as `CopilotSessionMetadata` on the interaction entity
- Adding new orchestrator-specific fields does not require changes to `chat-interaction.js` or `ChatInteractionHub`

### Orchestration Context Flow

1. `CopilotOrchestrationContextHandler` (implements `IOrchestrationContextHandler`) reads `CopilotSessionMetadata` from the resource entity and sets it on `OrchestrationContext.Properties`
2. `CopilotOrchestrator` reads the metadata from `Properties` to configure the session model and the `--allow-all` flag
3. Authentication uses the SDK's `GithubToken` property (not environment variables) and `CliArgs` for the allow-all flag

### Credential Resolution Order

The orchestrator resolves GitHub credentials in this order:
1. **Profile-level credentials** — Encrypted tokens stored on the AI Profile via `CopilotSessionMetadata`
2. **User-level credentials** — The current HTTP user's stored GitHub OAuth tokens

## Security

- OAuth tokens are stored encrypted at rest using ASP.NET Core Data Protection
- Profile-level tokens use the same encryption protector as user-level tokens
- Tokens are never exposed in logs or client-side code
- Client secret is protected using data protection
- Popup OAuth flow prevents credentials from being exposed in URL parameters
- Profile credential warning alerts administrators that their credentials will be shared

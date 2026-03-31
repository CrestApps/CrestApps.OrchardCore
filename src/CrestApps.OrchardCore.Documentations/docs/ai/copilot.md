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
- **Per-Profile Model Selection**: The model is configured per AI Profile or Chat Interaction
- **Allow All Tool Executions**: Configurable checkbox that both passes the `--allow-all` flag and supplies the required Copilot SDK permission handler for tool execution
- **GitHub OAuth Authentication**: User-scoped or profile-scoped authentication with GitHub for Copilot access
- **Profile-Level Credential Storage**: AI Profiles can store GitHub credentials so all chat sessions using the profile share the same token
- **Popup OAuth Flow**: AI Profile editing uses a popup window for GitHub authentication to avoid losing unsaved form data
- **Extensible Settings Pipeline**: Chat Interaction settings are saved via `IChatInteractionSettingsHandler`, allowing Copilot-specific fields to be handled without modifying core chat infrastructure
- **Configuration Safety Checks**: Copilot-backed chat UIs now warn and stay disabled until the required Copilot settings are complete, and the orchestrator short-circuits with a friendly message instead of forwarding incomplete requests
- **Tenant-Aware OAuth Callback URL**: GitHub OAuth now prefers the tenant **Base URI** from site settings when constructing the callback URL, which avoids localhost redirects when a tenant is accessed through a public tunnel or reverse proxy
- **Tenant-Aware Chat Hub URLs**: Copilot-backed chat surfaces now use the tenant **Base URI** for SignalR hub endpoints too, so chat reconnects and post-sign-in flows do not fall back to `localhost`
- **Direct Orchard Site Settings Resolution**: The shared SignalR hub route builder now resolves the tenant Base URI through Orchard's `ISiteService` directly instead of using reflection

## Prerequisites

- **GitHub Copilot CLI** installed and available in PATH (bundled with the SDK NuGet package)

Depending on which authentication mode you choose:

- **GitHub Signed-in User (GitHub OAuth)**: a valid **GitHub Copilot subscription** and a **GitHub OAuth App**
- **API Key (Bring your own key)**: an API key (and endpoint) for a supported model provider

## Configuration

Configure the Copilot orchestrator at **Settings → Copilot**. The main choice is which **authentication type** you want to use.

| Authentication type | When to use | What you configure |
|---|---|---|
| **Not configured** (`NotConfigured`) | You are not ready to enable Copilot yet | Nothing yet — keeps Copilot disabled until you explicitly choose and save a real authentication mode |
| **GitHub OAuth (GitHub Signed-in User)** (`GitHubOAuth`) | You want to use GitHub Copilot entitlements and user-scoped access | GitHub OAuth app (client ID/secret) and user/profile sign-in |
| **API Key (Bring your own key)** (`ApiKey`) | You want to use your own model provider credentials (no Copilot subscription required) | Provider type, base URL, API key, default model, wire format |

The settings editor defaults to **Not configured** so you can save other tenant settings without accidentally selecting an incomplete Copilot authentication mode.

### Authentication: GitHub OAuth (GitHub Signed-in User)

Use this mode when you want the orchestrator to authenticate to Copilot via GitHub and use the models available to that signed-in identity.

#### Settings (site/tenant)

- **GitHub OAuth App Client ID**
- **GitHub OAuth App Client Secret** (stored encrypted)

#### GitHub OAuth App setup

1. Create a GitHub OAuth App:
   - GitHub Settings → Developer settings → OAuth Apps → **New OAuth App**
   - In Orchard Core, first set **Settings → General → Base URI** to the public URL for the tenant.
   - **Authorization callback URL**: `<Base URI>/copilot/OAuthCallback`
   - Copy the **Client ID** and **Client Secret**
2. In Orchard Core: go to **Settings → Artificial Intelligence → Copilot** and enter the client ID/secret.
3. If AI Memory or AI Documents features are enabled but not configured yet, you can still save Copilot settings first and come back later to choose those index profiles.

Until both the client ID and client secret are saved, Copilot editors and chat experiences show a warning and prevent new Copilot requests from being sent.

If the tenant is exposed through a dev tunnel, reverse proxy, or another public hostname, set **Settings → General → Base URI** to that public URL. Both the GitHub OAuth callback URL and the SignalR chat hub URLs use that value.

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

If the required API key settings are incomplete, Copilot chat surfaces stay disabled and show a warning until the missing settings are supplied.

## Usage

Usage differs slightly depending on the authentication type selected in **Settings → Copilot**.

In all modes, **Allow All** is checked by default. When enabled, the orchestrator both passes `--allow-all` to the Copilot CLI and approves tool permission requests through the SDK session callback. When disabled, the orchestrator denies tool execution explicitly instead of letting session startup fail.

### GitHub OAuth authentication

#### Chat Interactions

1. The user authenticates with GitHub (via the **Sign in with GitHub** button).
2. The **Copilot Model** dropdown shows available models from the user's GitHub account.
3. The first model is auto-selected; there is no site-level “Default model” because model availability depends on the signed-in identity.

#### AI Profiles

1. Select **GitHub Copilot Orchestrator** from the Orchestrator dropdown.
2. The Connection and Deployment fields are hidden (not used by Copilot).
3. The **Copilot Configuration** section includes GitHub sign-in and a model picker.

#### Credential scope

- **Chat Interactions** are typically **user-scoped**: the access token is stored on the user account (encrypted), and each user signs in individually.
- **AI Profiles** can be **profile-scoped**: when you sign in while editing a profile, the encrypted credentials can be stored on the profile so any chat session using the profile can authenticate without requiring each user to sign in.

### API Key authentication

#### Chat Interactions

- No GitHub sign-in is required.
- The session uses the **Default Model** configured in **Settings → Copilot**.

#### AI Profiles

- GitHub authentication and model listing are not used.
- The profile relies on the site-level API key settings (provider type, base URL, and default model).

## Architecture

### Extensible Settings Pipeline

The module uses `IChatInteractionSettingsHandler` to decouple Copilot-specific settings from the core chat infrastructure:

- `CopilotChatInteractionSettingsHandler` reads `copilotModel` and `isAllowAll` from the generic form data and stores them as `CopilotSessionMetadata` on the interaction entity
- Adding new orchestrator-specific fields does not require changes to `chat-interaction.js` or `ChatInteractionHub`

### Orchestration Context Flow

1. `CopilotOrchestrationContextHandler` (implements `IOrchestrationContextHandler`) reads `CopilotSessionMetadata` from the resource entity and sets it on `OrchestrationContext.Properties`
2. `CopilotOrchestrator` reads the metadata from `Properties` to configure the session model, CLI behavior, and the required SDK permission callback
3. Authentication uses the SDK's `GithubToken` property (not environment variables), while the allow-all behavior is applied through both `CliArgs` and `SessionConfig.OnPermissionRequest`

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

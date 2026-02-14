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
- **Conditional UI**: Connection and Deployment fields are hidden when Copilot orchestrator is selected.
- **GitHub OAuth Authentication**: User-scoped authentication with GitHub for Copilot access.
- **Copilot-Specific Configuration**: Model selector and execution flags specific to Copilot.

## Prerequisites

- A valid **GitHub Copilot subscription**.
- **GitHub OAuth App** configured for authentication (see Configuration section below).

## Configuration

### GitHub OAuth Setup

To enable GitHub OAuth authentication for Copilot:

1. Create a GitHub OAuth App:
   - Go to GitHub Settings → Developer settings → OAuth Apps
   - Click "New OAuth App"
   - Set Authorization callback URL to: `https://your-domain.com/CopilotAuth/OAuthCallback`
   - Note the Client ID and Client Secret

2. Configure the OAuth credentials in your application settings (this functionality is pending implementation).

3. Required OAuth scopes:
   - `user:email` - To identify the user
   - `read:org` - To access Copilot on behalf of the user
   - Additional scopes as required by GitHub Copilot

## Usage

Once the module is enabled:

1. Go to **AI Profiles** in the admin dashboard.
2. Create or edit a profile.
3. Select **GitHub Copilot Orchestrator** from the Orchestrator dropdown.
4. The Connection and Deployment fields will be hidden (not used by Copilot).
5. The Copilot Configuration section will appear with:
   - **GitHub Authentication** - Sign in with GitHub button
   - **Copilot Model** - Select which model to use (GPT-4o, Claude 3.5 Sonnet, etc.)
   - **Copilot Flags** - Optional execution flags like `--allow-all`

### Authentication Flow

1. Click "Sign in with GitHub" in the Copilot Configuration section
2. Authorize the application to access your GitHub account
3. Your access token is securely stored and encrypted
4. The token is reused across sessions
5. Reauthentication only required if:
   - Token expires
   - Token is revoked
   - User explicitly disconnects
   - 401 response from Copilot service

## Implementation Status

### ✅ Completed
- Conditional UI rendering (Connection/Deployment hidden for Copilot)
- Copilot configuration UI (model selector, flags)
- View models and display drivers
- OAuth service interface and controller structure
- Token storage model

### ⚠️ Pending Implementation
- **GitHub OAuth Service**: Full implementation of token exchange, refresh, and storage
- **Token Encryption**: Secure storage of access/refresh tokens using data protection
- **Database Integration**: YesSql indexes and queries for credential storage
- **OAuth Configuration**: Settings UI for GitHub OAuth App credentials
- **Token Refresh**: Automatic token refresh when expired
- **User Profile Integration**: Showing auth status in user dashboard

### 📝 TODO
1. Implement `GitHubOAuthService` methods (currently stubs with NotImplementedException)
2. Add YesSql indexes for `GitHubOAuthCredential`
3. Add data protection for encrypting tokens
4. Create configuration UI for GitHub OAuth App settings
5. Add user profile section showing GitHub connection status
6. Implement token refresh logic
7. Add comprehensive error handling and logging
8. Add unit tests for OAuth flow

## Dependencies

- `CrestApps.OrchardCore.AI` — Core AI module
- `GitHub.Copilot.SDK` — Official GitHub Copilot SDK for .NET
- `OrchardCore.Users` — For user authentication
- `OrchardCore.Data` — For credential storage (when implemented)

## Security Notes

- OAuth tokens are stored encrypted at rest (pending implementation)
- Tokens are user-scoped, not application-scoped
- Each user must authenticate individually
- Tokens are never exposed in logs or client-side code
- Reauthentication required when token is revoked or expired

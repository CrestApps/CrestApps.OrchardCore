# AI Copilot Orchestrator (`CrestApps.OrchardCore.AI.Chat.Copilot`)

## Summary

Provides a GitHub Copilot SDK-based orchestrator for AI chat sessions in Orchard Core. This module integrates the [GitHub Copilot SDK for .NET](https://github.com/github/copilot-sdk) as an alternative orchestrator alongside the default Progressive Tool Orchestrator.

## Features

- **Copilot-Powered Orchestration**: Delegates planning, tool selection, and execution to the GitHub Copilot agent runtime
- **Full Tool Registry Integration**: Discovers and uses all registered local and system tools from the OrchardCore AI Tool Registry
- **Native MCP Support**: MCP connections are configured on the Copilot session so that Copilot can manage MCP tools natively
- **Data Source Support**: Data source context (documents) is handled by the orchestration context pipeline before reaching the orchestrator
- **Streaming Responses**: Supports real-time streaming of AI responses
- **Per-Profile Model Selection**: The model/deployment is configured per AI Profile or Chat Interaction
- **GitHub OAuth Authentication**: User-scoped authentication with GitHub for Copilot access
- **Copilot-Specific Configuration**: Model selector and execution flags specific to Copilot

## Prerequisites

- A valid **GitHub Copilot subscription**
- **GitHub OAuth App** configured for authentication

## Configuration

### GitHub OAuth Setup

1. Create a GitHub OAuth App:
   - Go to GitHub Settings → Developer settings → OAuth Apps
   - Click "New OAuth App"
   - Set Authorization callback URL to: `https://your-domain.com/CopilotAuth/OAuthCallback`
   - Note the Client ID and Client Secret

2. Configure Copilot settings in OrchardCore:
   - Go to Configuration → Settings → Copilot
   - Enter your GitHub OAuth App Client ID
   - Enter your GitHub OAuth App Client Secret (stored encrypted)
   - Save settings

3. Required OAuth scopes:
   - `user:email` - To identify the user
   - `read:org` - To access Copilot on behalf of the user

## Usage

### Setting up a Copilot Profile

1. Go to **AI Profiles** in the admin dashboard
2. Create or edit a profile
3. Select **GitHub Copilot Orchestrator** from the Orchestrator dropdown
4. The Connection and Deployment fields will be hidden (not used by Copilot)
5. The Copilot Configuration section will appear with:
   - **GitHub Authentication** - Sign in with GitHub button
   - **Copilot Model** - Select which model to use (GPT-4o, Claude 3.5 Sonnet, o1-preview, o1-mini)
   - **Copilot Flags** - Optional execution flags like `--allow-all`

### Authentication Flow

1. Click "Sign in with GitHub" in the Copilot Configuration section
2. Authorize the application to access your GitHub account
3. Your access token is securely stored and encrypted
4. The token is reused across sessions
5. Reauthentication is required if:
   - Token expires
   - Token is revoked
   - User explicitly disconnects

## Dependencies

- `CrestApps.OrchardCore.AI` — Core AI module
- `GitHub.Copilot.SDK` — Official GitHub Copilot SDK for .NET
- `OrchardCore.Users.Core` — For user authentication
- `OrchardCore.Admin.Abstractions` — For admin configuration

## Security

- OAuth tokens are stored encrypted at rest using ASP.NET Core Data Protection
- Tokens are user-scoped, not application-scoped
- Each user must authenticate individually
- Tokens are never exposed in logs or client-side code
- Client secret is protected using data protection


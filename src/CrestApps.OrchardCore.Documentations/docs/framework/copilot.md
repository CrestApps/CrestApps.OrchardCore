---
sidebar_label: GitHub Copilot
sidebar_position: 16
title: GitHub Copilot Orchestrator
description: An alternative IOrchestrator implementation that uses the GitHub Copilot Extensions SDK for AI completions, supporting GitHub OAuth and Bring Your Own Key (BYOK) authentication modes.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/copilot)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# GitHub Copilot Orchestrator

> An alternative orchestrator that uses the GitHub Copilot Extensions SDK instead of the default orchestration pipeline, supporting both GitHub OAuth and BYOK authentication modes.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddCopilotOrchestrator();
```

Then resolve and use it by name:

```csharp
public class MyController(IOrchestratorResolver resolver)
{
    public async IAsyncEnumerable<string> StreamAsync(OrchestrationContext context)
    {
        var orchestrator = resolver.Resolve("copilot");

        await foreach (var update in orchestrator.ExecuteStreamingAsync(context))
        {
            yield return update.Text;
        }
    }
}
```

## Problem & Solution

The [default orchestrator](orchestration.md) manages the full agentic pipeline — tool calling, progressive scoping, RAG injection, and streaming — using `Microsoft.Extensions.AI`. This works well when you control the provider connection and model selection.

However, some scenarios require a different execution model:

- **GitHub Copilot subscribers** want to use their existing Copilot subscription without managing API keys.
- **GitHub OAuth flows** delegate authentication and model access to GitHub, requiring a dedicated SDK integration.
- **The Copilot SDK** handles MCP tool invocation natively, eliminating duplication with the framework's own MCP pipeline.
- **BYOK (Bring Your Own Key)** mode lets tenant admins configure any OpenAI-compatible provider while still using the Copilot SDK's session management and streaming.

The Copilot orchestrator addresses all of these by wrapping the GitHub Copilot Extensions SDK behind the same `IOrchestrator` interface.

## Authentication Modes

The orchestrator supports three authentication states, controlled by the `CopilotOptions.AuthenticationType` property:

| Mode | Value | Who Provides Credentials | Use Case |
|------|-------|--------------------------|----------|
| **Not configured** | `NotConfigured` | Nobody yet | Keep Copilot disabled until the host finishes configuration |
| **GitHub OAuth** | `GitHubOAuth` | Each user authenticates via GitHub | Users with Copilot subscriptions |
| **BYOK (API Key)** | `ApiKey` | Tenant admin configures a shared API key | Any OpenAI-compatible provider |

Hosts should treat `NotConfigured` as an intentionally disabled state. Admin UIs can save other settings first, then enable a real Copilot authentication mode later without accidentally showing Copilot as ready.

When a host exposes admin editors for AI profiles, templates, or chat interactions, those screens should also respect the configured state. If Copilot is selected while still `NotConfigured`, show a warning instead of Copilot-specific fields; in GitHub OAuth mode, show the same sign-in / connected-as state consistently anywhere the Copilot orchestrator can be selected.

### GitHub OAuth Mode

Users authenticate through a standard GitHub OAuth flow. The framework exchanges the authorization code for an access token and refresh token, then stores them encrypted via `ICopilotCredentialStore`. Each user's Copilot subscription determines which models are available.

### BYOK (API Key) Mode

The tenant admin configures a provider type, base URL, and API key. All users share the same credentials — no per-user authentication is needed. This mode supports any OpenAI-compatible endpoint (OpenAI, Azure OpenAI, Anthropic, or self-hosted).

## Configuration

### `CopilotOptions`

```csharp
public sealed class CopilotOptions
{
    public CopilotAuthenticationType AuthenticationType { get; set; }

    // GitHub OAuth fields
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string[] Scopes { get; set; } = ["user:email", "read:org"];

    // BYOK fields
    public string ProviderType { get; set; }   // "openai", "azure", "anthropic"
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
    public string WireApi { get; set; } = "completions";
    public string DefaultModel { get; set; }
    public string AzureApiVersion { get; set; }
}
```

### `appsettings.json` Example — GitHub OAuth

```json
{
  "CopilotOptions": {
    "AuthenticationType": "GitHubOAuth",
    "ClientId": "Iv1.abc123",
    "ClientSecret": "your-client-secret",
    "Scopes": ["user:email", "read:org"]
  }
}
```

### `appsettings.json` Example — BYOK

```json
{
  "CopilotOptions": {
    "AuthenticationType": "ApiKey",
    "ProviderType": "openai",
    "BaseUrl": "https://api.openai.com/v1",
    "ApiKey": "sk-...",
    "DefaultModel": "gpt-4o",
    "WireApi": "completions"
  }
}
```

For Azure OpenAI BYOK:

```json
{
  "CopilotOptions": {
    "AuthenticationType": "ApiKey",
    "ProviderType": "azure",
    "BaseUrl": "https://my-resource.openai.azure.com",
    "ApiKey": "your-azure-key",
    "DefaultModel": "gpt-4o",
    "AzureApiVersion": "2024-12-01-preview"
  }
}
```

## Services Registered by `AddCopilotOrchestrator()`

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `HttpClient` | Via `AddHttpClient()` | Transient | HTTP calls for OAuth token exchange and API requests |
| `IOrchestrator` | `CopilotOrchestrator` (name: `"copilot"`) | Scoped | Copilot SDK-based agentic execution |
| `GitHubOAuthService` | `GitHubOAuthService` | Scoped | GitHub OAuth token lifecycle |
| `IChatInteractionSettingsHandler` | `CopilotChatInteractionSettingsHandler` | Scoped | Adjusts chat settings for Copilot sessions |
| `IOrchestrationContextBuilderHandler` | `CopilotOrchestrationContextHandler` | Scoped | Injects Copilot metadata into orchestration context |

## Implementing `ICopilotCredentialStore`

When using GitHub OAuth mode, the host application **must** provide an implementation of `ICopilotCredentialStore`. This interface is responsible for persisting and retrieving encrypted OAuth credentials per user.

```csharp
public interface ICopilotCredentialStore
{
    Task<CopilotProtectedCredential> GetProtectedCredentialAsync(
        string userId);

    Task SaveProtectedCredentialAsync(
        string userId,
        CopilotProtectedCredential credential);

    Task ClearCredentialAsync(string userId);
}
```

### What the Host Must Do

1. **Encrypt at rest** — Store the `CopilotProtectedCredential` fields (access token, refresh token) using your platform's data protection mechanism (e.g., ASP.NET Core Data Protection API).
2. **Scope to user** — Credentials are keyed by `userId`. Multi-tenant hosts should also scope by tenant.
3. **Handle expiry** — The `GitHubOAuthService` calls `GetProtectedCredentialAsync` to check for a valid token before each request. If the token is expired, it uses the refresh token automatically.

### Example Implementation

```csharp
public sealed class DatabaseCredentialStore(
    IDataProtectionProvider dataProtection,
    ISession session) : ICopilotCredentialStore
{
    private readonly IDataProtector _protector =
        dataProtection.CreateProtector("CopilotCredentials");

    public async Task<CopilotProtectedCredential> GetProtectedCredentialAsync(
        string userId)
    {
        var record = await session.Query<CopilotCredentialRecord, CopilotCredentialIndex>(
            x => x.UserId == userId).FirstOrDefaultAsync();

        if (record == null)
        {
            return null;
        }

        return new CopilotProtectedCredential
        {
            ProtectedAccessToken = record.EncryptedAccessToken,
            ProtectedRefreshToken = record.EncryptedRefreshToken,
            ExpiresAt = record.ExpiresAt,
        };
    }

    public async Task SaveProtectedCredentialAsync(
        string userId,
        CopilotProtectedCredential credential)
    {
        var record = await session.Query<CopilotCredentialRecord, CopilotCredentialIndex>(
            x => x.UserId == userId).FirstOrDefaultAsync();

        record ??= new CopilotCredentialRecord { UserId = userId };

        record.EncryptedAccessToken = credential.ProtectedAccessToken;
        record.EncryptedRefreshToken = credential.ProtectedRefreshToken;
        record.ExpiresAt = credential.ExpiresAt;

        await session.SaveAsync(record);
    }

    public async Task ClearCredentialAsync(string userId)
    {
        var record = await session.Query<CopilotCredentialRecord, CopilotCredentialIndex>(
            x => x.UserId == userId).FirstOrDefaultAsync();

        if (record != null)
        {
            session.Delete(record);
        }
    }
}
```

Register your implementation:

```csharp
services.AddScoped<ICopilotCredentialStore, DatabaseCredentialStore>();
```

## `GitHubOAuthService`

The `GitHubOAuthService` manages the full GitHub OAuth lifecycle. It is registered automatically by `AddCopilotOrchestrator()` and is only relevant in `GitHubOAuth` authentication mode.

### Methods

| Method | Description |
|--------|-------------|
| `GetAuthorizationUrl(callbackUrl, returnUrl)` | Builds the GitHub OAuth authorization URL with the configured `ClientId` and `Scopes` |
| `ExchangeCodeForTokenAsync(code, userId)` | Exchanges the authorization code for access/refresh tokens and stores them via `ICopilotCredentialStore` |
| `GetCredentialAsync(userId)` | Returns credential metadata (expiry, username) without decrypting the token |
| `GetValidAccessTokenAsync(userId)` | Returns a decrypted, valid access token — refreshes automatically if expired |
| `ListModelsAsync(userId)` | Lists Copilot models available to the authenticated user |
| `IsAuthenticatedAsync(userId)` | Returns `true` if the user has a stored, non-expired credential |
| `DisconnectAsync(userId)` | Clears the stored credential for the user |

### Building an OAuth Controller

```csharp
public sealed class CopilotAuthController(
    GitHubOAuthService oauthService) : Controller
{
    [HttpGet("connect")]
    public IActionResult Connect(string returnUrl = "/")
    {
        var callbackUrl = Url.Action(nameof(Callback), null, null, Request.Scheme);
        var authUrl = oauthService.GetAuthorizationUrl(callbackUrl, returnUrl);

        return Redirect(authUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string code, string state)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await oauthService.ExchangeCodeForTokenAsync(code, userId);

        // state contains the returnUrl
        return Redirect(state ?? "/");
    }

    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await oauthService.DisconnectAsync(userId);

        return Ok();
    }

    [HttpGet("models")]
    public async Task<IActionResult> Models()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var models = await oauthService.ListModelsAsync(userId);

        return Ok(models);
    }
}
```

## BYOK Setup

In BYOK (API Key) mode, no per-user authentication is required. The tenant admin configures the provider connection once and all users share it.

### Steps

1. Set `AuthenticationType` to `ApiKey` in configuration.
2. Configure the provider type, base URL, API key, and default model.
3. Optionally set `WireApi` to control the API format (defaults to `"completions"`).
4. For Azure OpenAI, also set `AzureApiVersion`.

```csharp
services.Configure<CopilotOptions>(options =>
{
    options.AuthenticationType = CopilotAuthenticationType.ApiKey;
    options.ProviderType = "openai";
    options.BaseUrl = "https://api.openai.com/v1";
    options.ApiKey = "sk-...";
    options.DefaultModel = "gpt-4o";
});
```

:::tip
BYOK mode does **not** require an `ICopilotCredentialStore` implementation. The API key is read directly from configuration.
:::

## Session Metadata

`CopilotSessionMetadata` carries Copilot-specific session state and is attached to the `AIProfile` or `ChatInteraction`.

```csharp
public sealed class CopilotSessionMetadata
{
    public string CopilotModel { get; set; }
    public bool IsAllowAll { get; set; } = true;
    public string GitHubUsername { get; set; }
    public string ProtectedAccessToken { get; set; }
    public string ProtectedRefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
```

| Property | Description |
|----------|-------------|
| `CopilotModel` | The model identifier to use for this session (e.g., `"gpt-4o"`, `"claude-sonnet-4"`) |
| `IsAllowAll` | When `true`, all available tools are included in the session |
| `GitHubUsername` | The GitHub username of the authenticated user (OAuth mode only) |
| `ProtectedAccessToken` | Encrypted access token for the session (OAuth mode only) |
| `ProtectedRefreshToken` | Encrypted refresh token for the session (OAuth mode only) |
| `ExpiresAt` | Token expiration timestamp (OAuth mode only) |

## Execution Flow

The `CopilotOrchestrator` follows this pipeline when `ExecuteStreamingAsync` is called:

### 1. Build Tools

Tools are loaded from `IToolRegistry`, which merges tools from all registered `IToolRegistryProvider` instances (system tools, profile tools, agent tools). **MCP tools are excluded** because the Copilot SDK handles MCP natively.

### 2. Wrap Tools for DI

Each tool is wrapped in a `ServiceInjectedAIFunction` to ensure dependency injection works correctly when the Copilot SDK invokes tool functions.

### 3. Build Session Configuration

A `SessionConfig` is assembled with:

- **Streaming** enabled
- **Model** from `CopilotSessionMetadata.CopilotModel` or `CopilotOptions.DefaultModel`
- **Tools** from the wrapped tool list
- **System message** from the orchestration context

### 4. Configure Provider

- **BYOK mode**: The provider type, base URL, and API key are set on the session config from `CopilotOptions`.
- **OAuth mode**: A valid GitHub access token is resolved via `GitHubOAuthService.GetValidAccessTokenAsync()`.

### 5. Configure MCP Servers

If MCP server connections are present in the orchestration context, they are passed to the Copilot SDK's session configuration. The SDK manages MCP tool discovery and invocation directly.

### 6. Create Client and Session

A `CopilotClient` and `Session` are created using the GitHub Copilot Extensions SDK with the assembled configuration.

### 7. Execute Streaming

The session is executed and streaming updates are yielded back through the `IAsyncEnumerable<StreamingChatCompletionUpdate>` return type, matching the standard `IOrchestrator` contract.

```
┌──────────────────────────────────────────────────┐
│              OrchestrationContext                 │
└──────────────┬───────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────┐
│  1. IToolRegistry → Load tools (exclude MCP)     │
│  2. Wrap in ServiceInjectedAIFunction            │
│  3. Build SessionConfig (model, tools, system)   │
│  4. Configure provider (BYOK) or OAuth token     │
│  5. Attach MCP servers (SDK-managed)             │
│  6. Create CopilotClient + Session               │
│  7. Execute streaming → yield updates            │
└──────────────────────────────────────────────────┘
```

## MCP Integration

The Copilot orchestrator handles MCP differently from the [default orchestrator](orchestration.md):

| Aspect | Default Orchestrator | Copilot Orchestrator |
|--------|---------------------|---------------------|
| **MCP tool discovery** | Framework resolves MCP tools via `IMcpClientManager` | Copilot SDK discovers MCP tools natively |
| **MCP tool invocation** | Framework invokes MCP tools through the tool registry | Copilot SDK invokes MCP tools directly |
| **Tool registry** | MCP tools appear in `IToolRegistry` | MCP tools are **excluded** from `IToolRegistry` |
| **MCP server config** | Managed by framework | Passed to Copilot SDK session config |

This means:

- MCP tools registered through the framework still work — they are passed to the SDK as server configurations.
- You do **not** need to change MCP server registrations. The orchestrator translates them automatically.
- Tool access control (`IAIToolAccessEvaluator`) applies only to non-MCP tools, since MCP tools are managed by the SDK.

## Orchard Core Integration

The [AI Chat Copilot module](../ai/copilot) wraps the Copilot orchestrator with an admin UI for configuring authentication mode, managing GitHub OAuth connections per user, selecting Copilot models, and providing a GitHub Copilot-style embedded chat experience.

# GitHub OAuth Implementation Guide

## Overview

This guide documents the GitHub OAuth authentication implementation for the Copilot orchestrator. The implementation provides user-scoped authentication with GitHub, allowing users to authenticate once and reuse their tokens across multiple sessions.

## Architecture

### Components

1. **Models** (`Models/GitHubOAuthCredential.cs`)
   - Stores encrypted OAuth credentials per user
   - Tracks token expiration
   - Associates GitHub username with OrchardCore user

2. **Services** (`Services/IGitHubOAuthService.cs` and `Services/GitHubOAuthService.cs`)
   - Manages OAuth flow (authorization, token exchange, refresh)
   - Handles token encryption/decryption
   - Validates token expiration

3. **Controllers** (`Controllers/CopilotAuthController.cs`)
   - Handles OAuth callback from GitHub
   - Initiates authentication flow
   - Manages disconnect/revoke actions

4. **UI Integration**
   - Views: `src/Modules/CrestApps.OrchardCore.AI/Views/AIProfileCopilotConfig.Edit.cshtml`
   - Driver: `src/Modules/CrestApps.OrchardCore.AI/Drivers/AIProfileCopilotDisplayDriver.cs`
   - ViewModel: `src/Modules/CrestApps.OrchardCore.AI/ViewModels/EditCopilotProfileViewModel.cs`

## OAuth Flow

### 1. Authorization Request

When user clicks "Sign in with GitHub":

```
GET /CopilotAuth/AuthorizeGitHub
  → Redirect to https://github.com/login/oauth/authorize
    ?client_id={client_id}
    &redirect_uri={callback_url}
    &scope=user:email,read:org
    &state={csrf_token}
```

### 2. Authorization Callback

GitHub redirects back with authorization code:

```
GET /CopilotAuth/OAuthCallback?code={code}&state={state}
  → Exchange code for access token
  → Store encrypted token in database
  → Redirect back to profile edit page
```

### 3. Token Exchange

```
POST https://github.com/login/oauth/access_token
  client_id={client_id}
  client_secret={client_secret}
  code={authorization_code}
  
Response:
  access_token={token}
  token_type=bearer
  scope={scopes}
```

### 4. Get User Information

```
GET https://api.github.com/user
  Authorization: Bearer {access_token}
  
Response:
  login: {username}
  id: {github_user_id}
  email: {email}
```

## Implementation Tasks

### ✅ Completed

- [x] OAuth service interface
- [x] OAuth controller structure
- [x] Token storage model
- [x] UI integration (display driver, view, view model)
- [x] Conditional rendering based on orchestrator selection

### ⚠️ In Progress / TODO

#### 1. OAuth Configuration Settings

Create a settings model and UI for GitHub OAuth App configuration:

```csharp
public class GitHubOAuthSettings
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string CallbackUrl { get; set; }
    public string[] Scopes { get; set; } = ["user:email", "read:org"];
}
```

**Files to create:**
- `Models/GitHubOAuthSettings.cs`
- `Drivers/GitHubOAuthSettingsDisplayDriver.cs`
- `Views/GitHubOAuthSettings.Edit.cshtml`

**Register in Startup:**
```csharp
services.AddSiteSettingsDisplayDriver<GitHubOAuthSettingsDisplayDriver>();
```

#### 2. Token Storage (YesSql)

Create database index and queries for `GitHubOAuthCredential`:

```csharp
public class GitHubOAuthCredentialIndex : MapIndex
{
    public string UserId { get; set; }
    public string GitHubUsername { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class GitHubOAuthCredentialIndexProvider : IndexProvider<GitHubOAuthCredential>
{
    public override void Describe(DescribeContext<GitHubOAuthCredential> context)
    {
        context.For<GitHubOAuthCredentialIndex>()
            .Map(credential => new GitHubOAuthCredentialIndex
            {
                UserId = credential.UserId,
                GitHubUsername = credential.GitHubUsername,
                ExpiresAt = credential.ExpiresAt,
            });
    }
}
```

**Files to create:**
- `Indexes/GitHubOAuthCredentialIndex.cs`
- `Indexes/GitHubOAuthCredentialIndexProvider.cs`
- `Migrations/GitHubOAuthMigrations.cs`

**Register in Startup:**
```csharp
services.AddIndexProvider<GitHubOAuthCredentialIndexProvider>();
services.AddDataMigration<GitHubOAuthMigrations>();
```

#### 3. Token Encryption

Use ASP.NET Core Data Protection to encrypt tokens:

```csharp
public class GitHubTokenProtector
{
    private readonly IDataProtector _protector;

    public GitHubTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("CrestApps.OrchardCore.AI.Chat.Copilot.GitHubTokens");
    }

    public string Protect(string token)
        => _protector.Protect(token);

    public string Unprotect(string encryptedToken)
        => _protector.Unprotect(encryptedToken);
}
```

**Files to create:**
- `Services/GitHubTokenProtector.cs`

**Register in Startup:**
```csharp
services.AddSingleton<GitHubTokenProtector>();
```

#### 4. Complete GitHubOAuthService Implementation

Implement all stub methods in `Services/GitHubOAuthService.cs`:

**Dependencies to inject:**
```csharp
private readonly ISession _session;
private readonly IHttpClientFactory _httpClientFactory;
private readonly GitHubTokenProtector _tokenProtector;
private readonly IOptions<GitHubOAuthSettings> _settings;
private readonly ILogger<GitHubOAuthService> _logger;
```

**Key methods to implement:**

- `GetAuthorizationUrl()`: Generate GitHub OAuth URL
- `ExchangeCodeForTokenAsync()`: Exchange code for token
- `GetCredentialAsync()`: Query database for credential
- `GetValidAccessTokenAsync()`: Get token, refresh if expired
- `DisconnectAsync()`: Revoke and delete credential

#### 5. Token Refresh Logic

If GitHub provides refresh tokens:

```csharp
private async Task<string> RefreshTokenAsync(GitHubOAuthCredential credential)
{
    // POST to GitHub refresh token endpoint
    // Update credential with new tokens
    // Save to database
    // Return new access token
}
```

#### 6. Error Handling

Add comprehensive error handling:
- Network failures
- Invalid tokens
- Expired tokens
- Revoked access
- Configuration errors

#### 7. Testing

Create unit tests:
- OAuth flow simulation
- Token encryption/decryption
- Token refresh
- Error scenarios
- Database operations

## Security Considerations

### Token Storage
- ✅ Tokens are never stored in plain text
- ⚠️ Tokens must be encrypted using Data Protection API
- ✅ Tokens are user-scoped, not shared
- ⚠️ Tokens must be stored in secure database (YesSql)

### Token Lifecycle
- ⚠️ Implement automatic token refresh
- ⚠️ Handle token expiration gracefully
- ⚠️ Provide manual disconnect option
- ✅ Require reauthentication on token revocation

### API Security
- ✅ Use HTTPS for all OAuth requests
- ✅ Validate state parameter for CSRF protection
- ⚠️ Implement rate limiting on OAuth endpoints
- ✅ Never log tokens or credentials

### User Privacy
- ✅ Each user authenticates individually
- ✅ Tokens are not accessible to other users
- ⚠️ Provide clear consent/authorization messaging
- ⚠️ Allow users to view/revoke their GitHub connection

## Configuration

### Development Environment

1. Create GitHub OAuth App at: https://github.com/settings/developers
2. Set callback URL to: `https://localhost:5001/CopilotAuth/OAuthCallback`
3. Configure Client ID and Secret in OrchardCore settings
4. Enable Copilot module
5. Test OAuth flow

### Production Environment

1. Create production GitHub OAuth App
2. Set callback URL to: `https://your-domain.com/CopilotAuth/OAuthCallback`
3. Use secure secret management (Azure Key Vault, AWS Secrets Manager)
4. Enable HTTPS-only cookies
5. Configure data protection with shared keys for multi-server deployments

## API Endpoints

### Application Endpoints

- `GET /CopilotAuth/AuthorizeGitHub` - Initiates OAuth flow
- `GET /CopilotAuth/OAuthCallback` - Handles GitHub callback
- `POST /CopilotAuth/DisconnectGitHub` - Disconnects GitHub account

### GitHub API Endpoints

- `GET https://github.com/login/oauth/authorize` - Authorization
- `POST https://github.com/login/oauth/access_token` - Token exchange
- `GET https://api.github.com/user` - User information
- `DELETE https://api.github.com/applications/{client_id}/token` - Revoke token

## Troubleshooting

### "OAuth is not configured"
- Ensure GitHub OAuth App is created
- Verify Client ID and Secret are configured
- Check callback URL matches GitHub App settings

### "Token exchange failed"
- Verify Client Secret is correct
- Check network connectivity to GitHub
- Review GitHub API rate limits

### "User not authenticated"
- Token may have expired - require reauthentication
- Token may have been revoked - check GitHub account
- Database may be missing credential - require reauthentication

## References

- [GitHub OAuth Documentation](https://docs.github.com/en/developers/apps/building-oauth-apps/authorizing-oauth-apps)
- [GitHub Copilot SDK](https://github.com/github/copilot-sdk)
- [ASP.NET Core Data Protection](https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/)
- [OrchardCore Data (YesSql)](https://docs.orchardcore.net/en/latest/reference/core/Data/)

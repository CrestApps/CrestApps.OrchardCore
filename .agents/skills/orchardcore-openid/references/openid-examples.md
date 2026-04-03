# OpenID Connect Examples

## Example 1: Authorization Server with Web Application Client

A complete setup configuring Orchard Core as an OpenID Connect authorization server with a confidential web application client.

### Server Settings Recipe

```json
{
  "steps": [
    {
      "name": "OpenIdServerSettings",
      "AllowAuthorizationCodeFlow": true,
      "AllowClientCredentialsFlow": true,
      "AllowRefreshTokenFlow": true,
      "UseRollingRefreshTokens": true,
      "AuthorizationEndpointPath": "/connect/authorize",
      "TokenEndpointPath": "/connect/token",
      "LogoutEndpointPath": "/connect/logout",
      "UserinfoEndpointPath": "/connect/userinfo"
    }
  ]
}
```

### Custom Scope Registration

```json
{
  "steps": [
    {
      "name": "OpenIdScope",
      "ScopeName": "portal_api",
      "DisplayName": "Portal API",
      "Description": "Grants access to the customer portal API.",
      "Resources": "portal-resource-server"
    }
  ]
}
```

### Confidential Web Application Client

```json
{
  "steps": [
    {
      "name": "OpenIdApplication",
      "ClientId": "customer-portal",
      "DisplayName": "Customer Portal",
      "ClientType": "confidential",
      "ClientSecret": "portal-secret-value",
      "ConsentType": "implicit",
      "AllowAuthorizationCodeFlow": true,
      "AllowRefreshTokenFlow": true,
      "RedirectUris": "https://portal.example.com/signin-oidc",
      "PostLogoutRedirectUris": "https://portal.example.com/signout-callback-oidc",
      "AllowedScopes": [
        "openid",
        "profile",
        "email",
        "roles",
        "portal_api"
      ]
    }
  ]
}
```

### Migration for Server and Client Together

```csharp
public sealed class PortalOpenIdMigrations : DataMigration
{
    private readonly IOpenIdServerService _serverService;
    private readonly IOpenIdApplicationManager _applicationManager;
    private readonly IOpenIdScopeManager _scopeManager;

    public PortalOpenIdMigrations(
        IOpenIdServerService serverService,
        IOpenIdApplicationManager applicationManager,
        IOpenIdScopeManager scopeManager)
    {
        _serverService = serverService;
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
    }

    public async Task<int> CreateAsync()
    {
        var settings = await _serverService.GetSettingsAsync();
        settings.AllowAuthorizationCodeFlow = true;
        settings.AllowClientCredentialsFlow = true;
        settings.AllowRefreshTokenFlow = true;
        settings.UseRollingRefreshTokens = true;
        settings.AuthorizationEndpointPath = "/connect/authorize";
        settings.TokenEndpointPath = "/connect/token";
        settings.LogoutEndpointPath = "/connect/logout";
        settings.UserinfoEndpointPath = "/connect/userinfo";
        await _serverService.UpdateSettingsAsync(settings);

        await _scopeManager.CreateAsync(new OpenIdScopeDescriptor
        {
            Name = "portal_api",
            DisplayName = "Portal API",
            Description = "Grants access to the customer portal API.",
            Resources = { "portal-resource-server" },
        });

        await _applicationManager.CreateAsync(new OpenIdApplicationDescriptor
        {
            ClientId = "customer-portal",
            DisplayName = "Customer Portal",
            ClientSecret = "portal-secret-value",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Logout,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + "portal_api",
            },
            RedirectUris = { new Uri("https://portal.example.com/signin-oidc") },
            PostLogoutRedirectUris = { new Uri("https://portal.example.com/signout-callback-oidc") },
        });

        return 1;
    }
}
```

## Example 2: Client Credentials for Service-to-Service Communication

A background service authenticating with the authorization server using client credentials to call a protected API.

### Service Client Registration Recipe

```json
{
  "steps": [
    {
      "name": "OpenIdApplication",
      "ClientId": "background-worker",
      "DisplayName": "Background Worker Service",
      "ClientType": "confidential",
      "ClientSecret": "worker-service-secret",
      "ConsentType": "implicit",
      "AllowClientCredentialsFlow": true,
      "AllowedScopes": [
        "api"
      ]
    }
  ]
}
```

### Service Client Migration

```csharp
public sealed class WorkerClientMigrations : DataMigration
{
    private readonly IOpenIdApplicationManager _applicationManager;

    public WorkerClientMigrations(IOpenIdApplicationManager applicationManager)
    {
        _applicationManager = applicationManager;
    }

    public async Task<int> CreateAsync()
    {
        await _applicationManager.CreateAsync(new OpenIdApplicationDescriptor
        {
            ClientId = "background-worker",
            DisplayName = "Background Worker Service",
            ClientSecret = "worker-service-secret",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "api",
            },
        });

        return 1;
    }
}
```

### Requesting a Token from the Service

```csharp
public sealed class TokenService
{
    private readonly HttpClient _httpClient;

    public TokenService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://auth.example.com/connect/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "background-worker",
            ["client_secret"] = "worker-service-secret",
            ["scope"] = "api",
        });

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("access_token").GetString()!;
    }
}
```

## Example 3: Public SPA Client with PKCE

A single-page application using authorization code flow with Proof Key for Code Exchange.

### SPA Client Registration Recipe

```json
{
  "steps": [
    {
      "name": "OpenIdApplication",
      "ClientId": "react-dashboard",
      "DisplayName": "React Dashboard",
      "ClientType": "public",
      "ConsentType": "explicit",
      "AllowAuthorizationCodeFlow": true,
      "AllowRefreshTokenFlow": true,
      "RequireProofKeyForCodeExchange": true,
      "RedirectUris": "https://dashboard.example.com/callback,https://dashboard.example.com/silent-renew",
      "PostLogoutRedirectUris": "https://dashboard.example.com/logout",
      "AllowedScopes": [
        "openid",
        "profile",
        "email",
        "api"
      ]
    }
  ]
}
```

### SPA Client Migration

```csharp
public sealed class SpaClientMigrations : DataMigration
{
    private readonly IOpenIdApplicationManager _applicationManager;

    public SpaClientMigrations(IOpenIdApplicationManager applicationManager)
    {
        _applicationManager = applicationManager;
    }

    public async Task<int> CreateAsync()
    {
        await _applicationManager.CreateAsync(new OpenIdApplicationDescriptor
        {
            ClientId = "react-dashboard",
            DisplayName = "React Dashboard",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Logout,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + "api",
            },
            Requirements =
            {
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
            },
            RedirectUris =
            {
                new Uri("https://dashboard.example.com/callback"),
                new Uri("https://dashboard.example.com/silent-renew"),
            },
            PostLogoutRedirectUris =
            {
                new Uri("https://dashboard.example.com/logout"),
            },
        });

        return 1;
    }
}
```

## Example 4: Protected API with JWT Bearer Validation

A resource server validating tokens issued by an Orchard Core authorization server.

### Validation Settings Recipe

```json
{
  "steps": [
    {
      "name": "OpenIdValidationSettings",
      "Authority": "https://auth.example.com",
      "Audience": "portal-resource-server"
    }
  ]
}
```

### API Controller with Scoped Authorization

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public sealed class OrdersController : ControllerBase
{
    private readonly IContentManager _contentManager;

    public OrdersController(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var userId = User.FindFirstValue(OpenIddictConstants.Claims.Subject);

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var query = await _contentManager.QueryAsync("Order",
            q => q.Where<ContainedPartIndex>(x => x.ListContentItemId == userId));

        return Ok(query);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,OrderManager")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var contentItem = await _contentManager.NewAsync("Order");
        contentItem.Alter<TitlePart>(part => part.Title = request.Title);
        await _contentManager.CreateAsync(contentItem);

        return CreatedAtAction(nameof(GetOrders), new { id = contentItem.ContentItemId }, contentItem);
    }
}

public sealed class CreateOrderRequest
{
    public string Title { get; set; } = string.Empty;
}
```

## Example 5: External Azure AD Authentication

Configuring Azure Active Directory as an external identity provider for federated login.

### Azure AD Client Settings Recipe

```json
{
  "steps": [
    {
      "name": "OpenIdClientSettings",
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "ClientId": "azure-app-client-id",
      "ClientSecret": "azure-app-client-secret",
      "CallbackPath": "/signin-oidc-azure",
      "SignedOutCallbackPath": "/signout-callback-oidc-azure",
      "DisplayName": "Azure Active Directory",
      "Scopes": [
        "openid",
        "profile",
        "email"
      ]
    }
  ]
}
```

## Example 6: Complete Multi-Application Setup Recipe

A full recipe deploying an authorization server with multiple clients and scopes in a single step.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.OpenId",
        "OrchardCore.OpenId.Server",
        "OrchardCore.OpenId.Validation"
      ]
    },
    {
      "name": "OpenIdServerSettings",
      "AllowAuthorizationCodeFlow": true,
      "AllowClientCredentialsFlow": true,
      "AllowRefreshTokenFlow": true,
      "UseRollingRefreshTokens": true,
      "AuthorizationEndpointPath": "/connect/authorize",
      "TokenEndpointPath": "/connect/token",
      "LogoutEndpointPath": "/connect/logout",
      "UserinfoEndpointPath": "/connect/userinfo"
    },
    {
      "name": "OpenIdScope",
      "ScopeName": "inventory_api",
      "DisplayName": "Inventory API",
      "Description": "Grants access to the inventory management API.",
      "Resources": "inventory-server"
    },
    {
      "name": "OpenIdScope",
      "ScopeName": "reporting_api",
      "DisplayName": "Reporting API",
      "Description": "Grants access to the reporting API endpoints.",
      "Resources": "reporting-server"
    },
    {
      "name": "OpenIdApplication",
      "ClientId": "warehouse-app",
      "DisplayName": "Warehouse Management App",
      "ClientType": "confidential",
      "ClientSecret": "warehouse-app-secret",
      "ConsentType": "implicit",
      "AllowAuthorizationCodeFlow": true,
      "AllowRefreshTokenFlow": true,
      "RedirectUris": "https://warehouse.example.com/signin-oidc",
      "PostLogoutRedirectUris": "https://warehouse.example.com/signout-callback-oidc",
      "AllowedScopes": [
        "openid",
        "profile",
        "roles",
        "inventory_api"
      ]
    },
    {
      "name": "OpenIdApplication",
      "ClientId": "reporting-service",
      "DisplayName": "Reporting Background Service",
      "ClientType": "confidential",
      "ClientSecret": "reporting-service-secret",
      "ConsentType": "implicit",
      "AllowClientCredentialsFlow": true,
      "AllowedScopes": [
        "inventory_api",
        "reporting_api"
      ]
    },
    {
      "name": "OpenIdApplication",
      "ClientId": "mobile-app",
      "DisplayName": "Mobile Application",
      "ClientType": "public",
      "ConsentType": "explicit",
      "AllowAuthorizationCodeFlow": true,
      "AllowRefreshTokenFlow": true,
      "RequireProofKeyForCodeExchange": true,
      "RedirectUris": "com.example.mobile:/callback",
      "PostLogoutRedirectUris": "com.example.mobile:/logout",
      "AllowedScopes": [
        "openid",
        "profile",
        "email",
        "inventory_api"
      ]
    },
    {
      "name": "OpenIdValidationSettings",
      "Authority": "https://auth.example.com",
      "Audience": "inventory-server"
    }
  ]
}
```

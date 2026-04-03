# Security Examples

## Example 1: OpenID Connect Application Recipe

```json
{
  "steps": [
    {
      "name": "OpenIdApplication",
      "OpenIdApplications": [
        {
          "ClientId": "my-spa-client",
          "DisplayName": "My SPA Application",
          "Type": "public",
          "AllowAuthorizationCodeFlow": true,
          "RequirePkce": true,
          "AllowRefreshTokenFlow": true,
          "RedirectUris": "https://myapp.example.com/callback",
          "PostLogoutRedirectUris": "https://myapp.example.com/signout-callback",
          "Scopes": "openid profile email"
        },
        {
          "ClientId": "my-api-service",
          "DisplayName": "My API Service",
          "Type": "confidential",
          "AllowClientCredentialsFlow": true,
          "ClientSecret": "{{SecureSecret}}",
          "Scopes": "api"
        }
      ]
    }
  ]
}
```

## Example 2: CORS Policy Recipe

```json
{
  "steps": [
    {
      "name": "CorsSettings",
      "Policies": [
        {
          "Name": "AllowSPA",
          "AllowedOrigins": [
            "https://myapp.example.com",
            "http://localhost:3000"
          ],
          "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
          "AllowedHeaders": ["Content-Type", "Authorization", "X-Requested-With"],
          "AllowCredentials": true,
          "ExposedHeaders": ["X-Total-Count"]
        }
      ],
      "DefaultPolicyName": "AllowSPA"
    }
  ]
}
```

## Example 3: Content-Level Authorization

```csharp
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;

public sealed class SecureContentController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentManager _contentManager;

    public SecureContentController(
        IAuthorizationService authorizationService,
        IContentManager contentManager)
    {
        _authorizationService = authorizationService;
        _contentManager = contentManager;
    }

    public async Task<IActionResult> View(string contentItemId)
    {
        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem == null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(
            User,
            OrchardCore.Contents.CommonPermissions.ViewContent,
            contentItem))
        {
            return Forbid();
        }

        return View(contentItem);
    }
}
```

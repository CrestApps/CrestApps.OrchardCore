# Site Settings Examples

## Example 1: Social Media Settings

A custom settings section for managing social media links displayed across the site.

### Content Part

```csharp
public sealed class SocialMediaSettings : ContentPart
{
    public string FacebookUrl { get; set; }

    public string TwitterHandle { get; set; }

    public string LinkedInUrl { get; set; }

    public bool ShowSocialLinks { get; set; }
}
```

### Migration

```csharp
public sealed class SocialMediaMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SocialMediaMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("SocialMediaSettings", type => type
            .DisplayedAs("Social Media Settings")
            .Stereotype("CustomSettings")
            .WithPart("SocialMediaSettings", part => part
                .WithPosition("0")
            )
        );

        return 1;
    }
}
```

### View Model

```csharp
public class SocialMediaSettingsViewModel
{
    public string FacebookUrl { get; set; }

    public string TwitterHandle { get; set; }

    public string LinkedInUrl { get; set; }

    public bool ShowSocialLinks { get; set; }
}
```

### Display Driver

```csharp
public sealed class SocialMediaSettingsDisplayDriver : DisplayDriver<ISite>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public SocialMediaSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override async Task<IDisplayResult> EditAsync(ISite model, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, SocialMediaPermissions.ManageSocialMediaSettings))
        {
            return null;
        }

        return Initialize<SocialMediaSettingsViewModel>("SocialMediaSettings_Edit", viewModel =>
        {
            var settings = model.As<SocialMediaSettings>();

            viewModel.FacebookUrl = settings.FacebookUrl;
            viewModel.TwitterHandle = settings.TwitterHandle;
            viewModel.LinkedInUrl = settings.LinkedInUrl;
            viewModel.ShowSocialLinks = settings.ShowSocialLinks;
        }).Location("Content:5")
        .OnGroup("social-media");
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite model, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, SocialMediaPermissions.ManageSocialMediaSettings))
        {
            return null;
        }

        if (context.GroupId == "social-media")
        {
            var viewModel = new SocialMediaSettingsViewModel();

            await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

            model.Put(new SocialMediaSettings
            {
                FacebookUrl = viewModel.FacebookUrl,
                TwitterHandle = viewModel.TwitterHandle,
                LinkedInUrl = viewModel.LinkedInUrl,
                ShowSocialLinks = viewModel.ShowSocialLinks,
            });
        }

        return await EditAsync(model, context);
    }
}
```

### Permissions

```csharp
public sealed class SocialMediaPermissions : IPermissionProvider
{
    public static readonly Permission ManageSocialMediaSettings = new(
        "ManageSocialMediaSettings",
        "Manage Social Media Settings"
    );

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        return Task.FromResult<IEnumerable<Permission>>(
        [
            ManageSocialMediaSettings,
        ]);
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = "Administrator",
                Permissions = [ManageSocialMediaSettings],
            },
        ];
    }
}
```

### Admin Navigation

```csharp
public sealed class SocialMediaAdminMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public SocialMediaAdminMenu(IStringLocalizer<SocialMediaAdminMenu> localizer)
    {
        S = localizer;
    }

    public Task BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!NavigationHelper.IsAdminMenu(name))
        {
            return Task.CompletedTask;
        }

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Social Media"], S["Social Media"].PrefixPosition(), entry => entry
                    .AddClass("social-media")
                    .Id("socialmedia")
                    .Permission(SocialMediaPermissions.ManageSocialMediaSettings)
                    .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = "social-media" })
                    .LocalNav()
                )
            );

        return Task.CompletedTask;
    }
}
```

### Startup Registration

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<SocialMediaSettings>();
        services.AddScoped<IDisplayDriver<ISite>, SocialMediaSettingsDisplayDriver>();
        services.AddScoped<INavigationProvider, SocialMediaAdminMenu>();
        services.AddScoped<IPermissionProvider, SocialMediaPermissions>();
    }
}
```

### Razor View (SocialMediaSettings_Edit.cshtml)

```html
@model SocialMediaSettingsViewModel

<div class="mb-3">
    <label asp-for="FacebookUrl" class="form-label">Facebook URL</label>
    <input asp-for="FacebookUrl" class="form-control" placeholder="https://facebook.com/yourpage" />
    <span asp-validation-for="FacebookUrl"></span>
</div>

<div class="mb-3">
    <label asp-for="TwitterHandle" class="form-label">Twitter Handle</label>
    <div class="input-group">
        <span class="input-group-text">@@</span>
        <input asp-for="TwitterHandle" class="form-control" placeholder="yourhandle" />
    </div>
    <span asp-validation-for="TwitterHandle"></span>
</div>

<div class="mb-3">
    <label asp-for="LinkedInUrl" class="form-label">LinkedIn URL</label>
    <input asp-for="LinkedInUrl" class="form-control" placeholder="https://linkedin.com/company/yourcompany" />
    <span asp-validation-for="LinkedInUrl"></span>
</div>

<div class="mb-3">
    <div class="form-check">
        <input asp-for="ShowSocialLinks" class="form-check-input" />
        <label asp-for="ShowSocialLinks" class="form-check-label">Display social media links on the site</label>
    </div>
</div>
```

### Reading in a View Component

```csharp
public sealed class SocialLinksViewComponent : ViewComponent
{
    private readonly ISiteService _siteService;

    public SocialLinksViewComponent(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.As<SocialMediaSettings>();

        if (!settings.ShowSocialLinks)
        {
            return Content(string.Empty);
        }

        return View(settings);
    }
}
```

---

## Example 2: Analytics Tracking Settings with Content Fields

A settings section that uses content fields for a media logo and configurable tracking script.

### Migration

```csharp
public sealed class AnalyticsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public AnalyticsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("AnalyticsSettings", type => type
            .DisplayedAs("Analytics Settings")
            .Stereotype("CustomSettings")
            .WithPart("AnalyticsSettings", part => part
                .WithPosition("0")
            )
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync("AnalyticsSettings", part => part
            .WithField("TrackingId", field => field
                .OfType("TextField")
                .WithDisplayName("Tracking ID")
                .WithPosition("0")
                .WithSettings(new TextFieldSettings
                {
                    Hint = "Enter the analytics tracking ID (e.g., G-XXXXXXXXXX).",
                })
            )
            .WithField("EnableTracking", field => field
                .OfType("BooleanField")
                .WithDisplayName("Enable Tracking")
                .WithPosition("1")
            )
            .WithField("ConsentBannerLogo", field => field
                .OfType("MediaField")
                .WithDisplayName("Consent Banner Logo")
                .WithPosition("2")
            )
        );

        return 1;
    }
}
```

When using content fields in a custom settings type, the fields are rendered automatically by their own display drivers. You do not need to create a custom `DisplayDriver<ISite>` for the field editors.

---

## Example 3: Configuring Site Settings via Recipe

### Setting Built-In Properties

```json
{
    "steps": [
        {
            "name": "Settings",
            "SiteName": "Contoso Corporate Portal",
            "BaseUrl": "https://www.contoso.com",
            "TimeZoneId": "Europe/London",
            "Culture": "en-GB",
            "PageSize": 15,
            "UseCdn": false
        }
    ]
}
```

### Setting Custom Settings Values

```json
{
    "steps": [
        {
            "name": "custom-settings",
            "SocialMediaSettings": {
                "ContentItemId": "[js:uuid()]",
                "ContentType": "SocialMediaSettings",
                "SocialMediaSettings": {
                    "FacebookUrl": "https://facebook.com/contoso",
                    "TwitterHandle": "contoso",
                    "LinkedInUrl": "https://linkedin.com/company/contoso",
                    "ShowSocialLinks": true
                }
            }
        }
    ]
}
```

### Combined Recipe for Full Site Setup

```json
{
    "steps": [
        {
            "name": "Settings",
            "SiteName": "Contoso Corporate Portal",
            "BaseUrl": "https://www.contoso.com",
            "TimeZoneId": "Europe/London",
            "Culture": "en-GB",
            "PageSize": 15,
            "UseCdn": true
        },
        {
            "name": "custom-settings",
            "SocialMediaSettings": {
                "ContentItemId": "[js:uuid()]",
                "ContentType": "SocialMediaSettings",
                "SocialMediaSettings": {
                    "FacebookUrl": "https://facebook.com/contoso",
                    "TwitterHandle": "contoso",
                    "LinkedInUrl": "https://linkedin.com/company/contoso",
                    "ShowSocialLinks": true
                }
            },
            "AnalyticsSettings": {
                "ContentItemId": "[js:uuid()]",
                "ContentType": "AnalyticsSettings",
                "AnalyticsSettings": {
                    "TrackingId": {
                        "Text": "G-ABC123XYZ"
                    },
                    "EnableTracking": {
                        "Value": true
                    }
                }
            }
        }
    ]
}
```

---

## Example 4: Reading Settings in a Middleware

A middleware that reads custom settings to inject a response header on every request.

```csharp
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var siteService = context.RequestServices.GetRequiredService<ISiteService>();
        var site = await siteService.GetSiteSettingsAsync();
        var settings = site.As<SecurityHeaderSettings>();

        if (!string.IsNullOrEmpty(settings.ContentSecurityPolicy))
        {
            context.Response.Headers["Content-Security-Policy"] = settings.ContentSecurityPolicy;
        }

        if (settings.EnableStrictTransportSecurity)
        {
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await _next(context);
    }
}
```

### Content Part

```csharp
public sealed class SecurityHeaderSettings : ContentPart
{
    public string ContentSecurityPolicy { get; set; }

    public bool EnableStrictTransportSecurity { get; set; }
}
```

---

## Example 5: Settings with Validation

A display driver that validates user input before saving settings.

```csharp
public sealed class SmtpSettingsDisplayDriver : DisplayDriver<ISite>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public SmtpSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override async Task<IDisplayResult> EditAsync(ISite model, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, SmtpPermissions.ManageSmtpSettings))
        {
            return null;
        }

        return Initialize<SmtpSettingsViewModel>("SmtpSettings_Edit", viewModel =>
        {
            var settings = model.As<SmtpSettings>();

            viewModel.Host = settings.Host;
            viewModel.Port = settings.Port;
            viewModel.EnableSsl = settings.EnableSsl;
            viewModel.FromAddress = settings.FromAddress;
        }).Location("Content:5")
        .OnGroup("smtp");
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite model, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, SmtpPermissions.ManageSmtpSettings))
        {
            return null;
        }

        if (context.GroupId == "smtp")
        {
            var viewModel = new SmtpSettingsViewModel();

            await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

            if (string.IsNullOrWhiteSpace(viewModel.Host))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Host), "SMTP host is required.");
            }

            if (viewModel.Port <= 0 || viewModel.Port > 65535)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Port), "Port must be between 1 and 65535.");
            }

            if (string.IsNullOrWhiteSpace(viewModel.FromAddress) || !viewModel.FromAddress.Contains('@'))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.FromAddress), "A valid from address is required.");
            }

            if (context.Updater.ModelState.IsValid)
            {
                model.Put(new SmtpSettings
                {
                    Host = viewModel.Host,
                    Port = viewModel.Port,
                    EnableSsl = viewModel.EnableSsl,
                    FromAddress = viewModel.FromAddress,
                });
            }
        }

        return await EditAsync(model, context);
    }
}
```

### Content Part and View Model

```csharp
public sealed class SmtpSettings : ContentPart
{
    public string Host { get; set; }

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string FromAddress { get; set; }
}

public class SmtpSettingsViewModel
{
    public string Host { get; set; }

    public int Port { get; set; }

    public bool EnableSsl { get; set; }

    public string FromAddress { get; set; }
}
```

---

## Example 6: Accessing Settings in a Razor View via Injection

```html
@using OrchardCore.Settings
@using OrchardCore.ContentManagement

@inject ISiteService SiteService

@{
    var site = await SiteService.GetSiteSettingsAsync();
    var socialSettings = site.As<SocialMediaSettings>();
}

<footer>
    <p>&copy; @DateTime.Now.Year @site.SiteName</p>

    @if (socialSettings.ShowSocialLinks)
    {
        <nav aria-label="Social media links">
            <ul class="list-inline">
                @if (!string.IsNullOrEmpty(socialSettings.FacebookUrl))
                {
                    <li class="list-inline-item">
                        <a href="@socialSettings.FacebookUrl" target="_blank" rel="noopener">Facebook</a>
                    </li>
                }
                @if (!string.IsNullOrEmpty(socialSettings.TwitterHandle))
                {
                    <li class="list-inline-item">
                        <a href="https://twitter.com/@socialSettings.TwitterHandle" target="_blank" rel="noopener">Twitter</a>
                    </li>
                }
                @if (!string.IsNullOrEmpty(socialSettings.LinkedInUrl))
                {
                    <li class="list-inline-item">
                        <a href="@socialSettings.LinkedInUrl" target="_blank" rel="noopener">LinkedIn</a>
                    </li>
                }
            </ul>
        </nav>
    }
</footer>
```

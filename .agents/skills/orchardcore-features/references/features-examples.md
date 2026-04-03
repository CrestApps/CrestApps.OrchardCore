# OrchardCore Features — Practical Examples

## Enabling a Set of Features for a Blog Site via Recipe

This recipe enables all features commonly needed for a blog-oriented site:

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        "OrchardCore.Title",
        "OrchardCore.Html",
        "OrchardCore.Alias",
        "OrchardCore.Autoroute",
        "OrchardCore.Lists",
        "OrchardCore.Media",
        "OrchardCore.Menu",
        "OrchardCore.Navigation",
        "OrchardCore.Themes",
        "OrchardCore.Users",
        "OrchardCore.Roles",
        "OrchardCore.Taxonomies",
        "OrchardCore.Flows",
        "OrchardCore.Widgets",
        "OrchardCore.Layers"
      ]
    }
  ]
}
```

## Switching Search Providers via Recipe

Disable Lucene and switch to Elasticsearch in a single recipe step:

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.Search.Elasticsearch"
      ],
      "disable": [
        "OrchardCore.Search.Lucene"
      ]
    }
  ]
}
```

## Programmatically Enabling a Feature by ID

Resolve a feature by its ID from the full list of available features, then enable it:

```csharp
public sealed class FeatureActivator
{
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public FeatureActivator(IShellFeaturesManager shellFeaturesManager)
    {
        _shellFeaturesManager = shellFeaturesManager;
    }

    public async Task EnableByIdAsync(string featureId)
    {
        var availableFeatures = await _shellFeaturesManager.GetAvailableFeaturesAsync();
        var feature = availableFeatures.FirstOrDefault(f => f.Id == featureId);

        if (feature == null)
        {
            throw new InvalidOperationException(
                $"Feature '{featureId}' is not available in this tenant.");
        }

        await _shellFeaturesManager.EnableFeaturesAsync(new[] { feature }, force: false);
    }
}
```

## Programmatically Disabling a Feature

Disable a feature only after confirming it is currently enabled:

```csharp
public sealed class FeatureDeactivator
{
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly ILogger<FeatureDeactivator> _logger;

    public FeatureDeactivator(
        IShellFeaturesManager shellFeaturesManager,
        ILogger<FeatureDeactivator> logger)
    {
        _shellFeaturesManager = shellFeaturesManager;
        _logger = logger;
    }

    public async Task<bool> DisableByIdAsync(string featureId)
    {
        var enabledFeatures = await _shellFeaturesManager.GetEnabledFeaturesAsync();
        var feature = enabledFeatures.FirstOrDefault(f => f.Id == featureId);

        if (feature == null)
        {
            _logger.LogWarning("Feature '{FeatureId}' is not currently enabled.", featureId);

            return false;
        }

        await _shellFeaturesManager.DisableFeaturesAsync(new[] { feature }, force: false);

        _logger.LogInformation("Feature '{FeatureId}' has been disabled.", featureId);

        return true;
    }
}
```

## Checking If Multiple Features Are Enabled

Verify that a set of required features are all enabled before proceeding:

```csharp
public sealed class FeaturePrerequisiteChecker
{
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public FeaturePrerequisiteChecker(IShellFeaturesManager shellFeaturesManager)
    {
        _shellFeaturesManager = shellFeaturesManager;
    }

    public async Task<FeatureCheckResult> CheckRequiredFeaturesAsync(
        IEnumerable<string> requiredFeatureIds)
    {
        var enabledFeatures = await _shellFeaturesManager.GetEnabledFeaturesAsync();
        var enabledIds = new HashSet<string>(enabledFeatures.Select(f => f.Id));

        var missingFeatures = requiredFeatureIds
            .Where(id => !enabledIds.Contains(id))
            .ToList();

        return new FeatureCheckResult
        {
            AllSatisfied = missingFeatures.Count == 0,
            MissingFeatureIds = missingFeatures,
        };
    }
}

public sealed class FeatureCheckResult
{
    public bool AllSatisfied { get; set; }
    public List<string> MissingFeatureIds { get; set; } = [];
}
```

## Feature Event Handler for Data Seeding

Seed default data when a feature is first installed:

```csharp
public sealed class BlogFeatureEventHandler : IFeatureEventHandler
{
    private readonly IContentManager _contentManager;

    public BlogFeatureEventHandler(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public Task EnablingAsync(IFeatureInfo feature) => Task.CompletedTask;

    public async Task EnabledAsync(IFeatureInfo feature)
    {
        if (feature.Id != "MyModule.Blog")
        {
            return;
        }

        // Create a default blog content item when the feature is first enabled.
        var blog = await _contentManager.NewAsync("Blog");
        blog.DisplayText = "Main Blog";

        await _contentManager.CreateAsync(blog, VersionOptions.Published);
    }

    public Task DisablingAsync(IFeatureInfo feature) => Task.CompletedTask;
    public Task DisabledAsync(IFeatureInfo feature) => Task.CompletedTask;
    public Task InstallingAsync(IFeatureInfo feature) => Task.CompletedTask;
    public Task InstalledAsync(IFeatureInfo feature) => Task.CompletedTask;
    public Task UninstallingAsync(IFeatureInfo feature) => Task.CompletedTask;
    public Task UninstalledAsync(IFeatureInfo feature) => Task.CompletedTask;
}
```

## Conditional Startup with RequireFeatures

Register services only when specific features are active. This startup class only activates when both `OrchardCore.Contents` and `OrchardCore.Media` are enabled:

```csharp
[RequireFeatures("OrchardCore.Contents", "OrchardCore.Media")]
public sealed class MediaContentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentHandler, MediaAttachmentHandler>();
    }
}

public sealed class MediaAttachmentHandler : ContentHandlerBase
{
    private readonly IMediaFileStore _mediaFileStore;
    private readonly ILogger<MediaAttachmentHandler> _logger;

    public MediaAttachmentHandler(
        IMediaFileStore mediaFileStore,
        ILogger<MediaAttachmentHandler> logger)
    {
        _mediaFileStore = mediaFileStore;
        _logger = logger;
    }

    public override async Task RemovedAsync(RemoveContentContext context)
    {
        // Clean up associated media files when a content item is removed.
        var mediaPath = $"/content-attachments/{context.ContentItem.ContentItemId}";

        if (await _mediaFileStore.GetDirectoryInfoAsync(mediaPath) != null)
        {
            await _mediaFileStore.TryDeleteDirectoryAsync(mediaPath);

            _logger.LogInformation(
                "Cleaned up media attachments for content item '{ContentItemId}'.",
                context.ContentItem.ContentItemId);
        }
    }
}
```

## Declaring Feature Dependencies in a Module Manifest

A module manifest that declares two features where one depends on the other:

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    name: "My Custom Module",
    author: "My Organization",
    version: "1.0.0",
    description: "Provides custom reporting and analytics."
)]

[assembly: Feature(
    id: "MyModule.Core",
    name: "My Module Core",
    description: "Core services for the custom module.",
    category: "Content"
)]

[assembly: Feature(
    id: "MyModule.Reporting",
    name: "My Module Reporting",
    description: "Reporting dashboards built on top of core services.",
    category: "Content",
    dependencies:
    [
        "MyModule.Core",
        "OrchardCore.Contents",
        "OrchardCore.Queries"
    ]
)]
```

## Building a Feature Management API Endpoint

Expose an API endpoint for listing and toggling features. This is useful for external tools or CI/CD pipelines:

```csharp
public sealed class FeatureManagementController : Controller
{
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IAuthorizationService _authorizationService;

    public FeatureManagementController(
        IShellFeaturesManager shellFeaturesManager,
        IAuthorizationService authorizationService)
    {
        _shellFeaturesManager = shellFeaturesManager;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageFeatures))
        {
            return Forbid();
        }

        var enabled = await _shellFeaturesManager.GetEnabledFeaturesAsync();
        var disabled = await _shellFeaturesManager.GetDisabledFeaturesAsync();

        var result = new
        {
            Enabled = enabled.Select(f => new { f.Id, f.Name, f.Category }).OrderBy(f => f.Id),
            Disabled = disabled.Select(f => new { f.Id, f.Name, f.Category }).OrderBy(f => f.Id),
        };

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Enable([FromBody] string[] featureIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageFeatures))
        {
            return Forbid();
        }

        var available = await _shellFeaturesManager.GetAvailableFeaturesAsync();
        var toEnable = available.Where(f => featureIds.Contains(f.Id)).ToList();

        if (toEnable.Count == 0)
        {
            return BadRequest("No matching features found.");
        }

        await _shellFeaturesManager.EnableFeaturesAsync(toEnable, force: false);

        return Ok(new { Enabled = toEnable.Select(f => f.Id) });
    }
}
```

## Listing Features per Tenant in a Multi-Tenant Setup

Build a summary of all tenants and their feature states from the default tenant:

```csharp
public sealed class MultiTenantFeatureInspector
{
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _currentShellSettings;

    public MultiTenantFeatureInspector(
        IShellHost shellHost,
        ShellSettings currentShellSettings)
    {
        _shellHost = shellHost;
        _currentShellSettings = currentShellSettings;
    }

    public async Task<IReadOnlyList<TenantFeatureInfo>> GetAllTenantFeaturesAsync()
    {
        var allSettings = _shellHost.GetAllSettings();
        var results = new List<TenantFeatureInfo>();

        foreach (var settings in allSettings)
        {
            var scope = await _shellHost.GetScopeAsync(settings);

            await scope.UsingAsync(async serviceProvider =>
            {
                var featuresManager = serviceProvider
                    .GetRequiredService<IShellFeaturesManager>();

                var enabled = await featuresManager.GetEnabledFeaturesAsync();

                results.Add(new TenantFeatureInfo
                {
                    TenantName = settings.Name,
                    EnabledFeatureIds = enabled.Select(f => f.Id).OrderBy(id => id).ToList(),
                });
            });
        }

        return results;
    }
}

public sealed class TenantFeatureInfo
{
    public string TenantName { get; set; }
    public List<string> EnabledFeatureIds { get; set; } = [];
}
```

## Enabling Features for a New Tenant via Setup Recipe

When creating a new tenant, include a setup recipe that pre-configures the required features:

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        "OrchardCore.Title",
        "OrchardCore.Autoroute",
        "OrchardCore.Html",
        "OrchardCore.Media",
        "OrchardCore.Users",
        "OrchardCore.Roles",
        "OrchardCore.Themes",
        "OrchardCore.Navigation",
        "OrchardCore.Menu",
        "OrchardCore.Settings",
        "OrchardCore.Recipes",
        "OrchardCore.Email",
        "OrchardCore.Workflows",
        "OrchardCore.BackgroundTasks"
      ]
    }
  ]
}
```

## Runtime Feature Guard Pattern

Check feature availability at runtime when `[RequireFeatures]` cannot be used (for example, inside a shared handler):

```csharp
public sealed class NotificationOnPublishHandler : ContentHandlerBase
{
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IServiceProvider _serviceProvider;

    public NotificationOnPublishHandler(
        IShellFeaturesManager shellFeaturesManager,
        IServiceProvider serviceProvider)
    {
        _shellFeaturesManager = shellFeaturesManager;
        _serviceProvider = serviceProvider;
    }

    public override async Task PublishedAsync(PublishContentContext context)
    {
        var enabledFeatures = await _shellFeaturesManager.GetEnabledFeaturesAsync();
        var isNotificationsEnabled = enabledFeatures.Any(f => f.Id == "OrchardCore.Notifications");

        if (!isNotificationsEnabled)
        {
            return;
        }

        var notificationService = _serviceProvider.GetRequiredService<INotificationService>();

        var notification = new Notification
        {
            Summary = $"Content item '{context.ContentItem.DisplayText}' was published.",
        };

        await notificationService.SendAsync(notification);
    }
}
```

## Always-On Feature Declaration

Declare a feature that cannot be disabled by administrators:

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Feature(
    id: "MyModule.Infrastructure",
    name: "My Module Infrastructure",
    description: "Core infrastructure services that must always be available.",
    category: "Infrastructure",
    isAlwaysEnabled: true
)]
```

When a feature is declared with `isAlwaysEnabled: true`, the disable button is hidden in the admin UI and programmatic attempts to disable it are rejected.

## Enabling Features from a Migration

Enable dependent features from within a data migration:

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public Migrations(IShellFeaturesManager shellFeaturesManager)
    {
        _shellFeaturesManager = shellFeaturesManager;
    }

    public async Task<int> CreateAsync()
    {
        // Ensure required features are enabled during migration.
        var available = await _shellFeaturesManager.GetAvailableFeaturesAsync();
        var requiredIds = new[] { "OrchardCore.Title", "OrchardCore.Autoroute" };

        var toEnable = available
            .Where(f => requiredIds.Contains(f.Id))
            .ToList();

        if (toEnable.Count > 0)
        {
            await _shellFeaturesManager.EnableFeaturesAsync(toEnable, force: false);
        }

        return 1;
    }
}
```

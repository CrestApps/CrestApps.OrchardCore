# Admin Panel Practical Examples

This reference provides additional hands-on examples for working with the Orchard Core admin panel.

## Complete Admin Controller with CRUD Operations

This example shows a full admin controller for managing a custom entity with list, create, edit, and delete actions:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using YesSql;

[Admin("Announcements/{action}/{id?}", "Announcements")]
public sealed class AnnouncementController : Controller
{
    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer H;

    public AnnouncementController(
        ISession session,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<AnnouncementController> htmlLocalizer)
    {
        _session = session;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAnnouncements))
        {
            return Forbid();
        }

        var announcements = await _session.Query<Announcement>().ListAsync();

        var model = new AnnouncementIndexViewModel
        {
            Announcements = announcements.ToList(),
        };

        return View(model);
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAnnouncements))
        {
            return Forbid();
        }

        return View(new AnnouncementEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAnnouncements))
        {
            return Forbid();
        }

        if (ModelState.IsValid)
        {
            var announcement = new Announcement
            {
                Title = model.Title,
                Message = model.Message,
                IsActive = model.IsActive,
            };

            await _session.SaveAsync(announcement);
            await _notifier.SuccessAsync(H["Announcement created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAnnouncements))
        {
            return Forbid();
        }

        var announcement = await _session.GetAsync<Announcement>(long.Parse(id));

        if (announcement == null)
        {
            return NotFound();
        }

        var model = new AnnouncementEditViewModel
        {
            Title = announcement.Title,
            Message = announcement.Message,
            IsActive = announcement.IsActive,
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAnnouncements))
        {
            return Forbid();
        }

        var announcement = await _session.GetAsync<Announcement>(long.Parse(id));

        if (announcement == null)
        {
            return NotFound();
        }

        _session.Delete(announcement);
        await _notifier.SuccessAsync(H["Announcement deleted successfully."]);

        return RedirectToAction(nameof(Index));
    }
}
```

## Admin Index View with Table and Actions

`Views/Announcement/Index.cshtml`:

```html
@model AnnouncementIndexViewModel

<zone Name="Title">
    <h1>@T["Announcements"]</h1>
</zone>

<zone Name="Content">
    <a asp-action="Create" class="btn btn-primary btn-sm mb-3">@T["New Announcement"]</a>

    @if (Model.Announcements.Any())
    {
        <table class="table table-hover">
            <thead>
                <tr>
                    <th>@T["Title"]</th>
                    <th>@T["Status"]</th>
                    <th>@T["Actions"]</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var item in Model.Announcements)
                {
                    <tr>
                        <td>@item.Title</td>
                        <td>
                            @if (item.IsActive)
                            {
                                <span class="badge bg-success">@T["Active"]</span>
                            }
                            else
                            {
                                <span class="badge bg-secondary">@T["Inactive"]</span>
                            }
                        </td>
                        <td>
                            <a asp-action="Edit" asp-route-id="@item.Id" class="btn btn-sm btn-secondary">@T["Edit"]</a>
                            <form asp-action="Delete" asp-route-id="@item.Id" method="post" class="d-inline">
                                @Html.AntiForgeryToken()
                                <button type="submit" class="btn btn-sm btn-danger" data-title="@T["Delete"]" data-message="@T["Are you sure you want to delete this announcement?"]">
                                    @T["Delete"]
                                </button>
                            </form>
                        </td>
                    </tr>
                }
            </tbody>
        </table>
    }
    else
    {
        <p class="alert alert-info">@T["No announcements found."]</p>
    }
</zone>
```

## Hierarchical Admin Menu with Multiple Sections

This example demonstrates a navigation provider that registers items across multiple top-level groups with explicit ordering:

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

public sealed class AdminMenu : INavigationProvider
{
    private readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> localizer)
    {
        S = localizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!NavigationHelper.IsAdminMenu(name))
        {
            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Content Management"], NavigationConstants.AdminMenuContentManagementPosition, content => content
                .AddClass("content-management")
                .Id("contentmanagement")
                .Add(S["Announcements"], S["Announcements"].PrefixPosition(), announcements => announcements
                    .Permission(Permissions.ManageAnnouncements)
                    .Action("Index", "Announcement", new { area = "MyModule" })
                    .LocalNav()
                )
                .Add(S["Newsletters"], S["Newsletters"].PrefixPosition(), newsletters => newsletters
                    .Permission(Permissions.ManageNewsletters)
                    .Action("Index", "Newsletter", new { area = "MyModule" })
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                    .Add(S["Announcement Defaults"], S["Announcement Defaults"].PrefixPosition(), defaults => defaults
                        .Permission(Permissions.ManageAnnouncementSettings)
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = "announcements" })
                        .LocalNav()
                    )
            );

        return ValueTask.CompletedTask;
    }
}
```

## Multi-Section Settings Page with Display Driver

This example shows how to build a tabbed settings page using the `Location` syntax with tab notation:

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

public sealed class EmailDeliverySettingsDisplayDriver : SiteDisplayDriver<EmailDeliverySettings>
{
    public const string GroupId = "email-delivery";

    protected override string SettingsGroupId => GroupId;

    public override IDisplayResult Edit(ISite site, EmailDeliverySettings settings, BuildEditorContext context)
    {
        return Combine(
            Initialize<EmailDeliverySettingsViewModel>("EmailDeliverySettings_Edit__General", model =>
            {
                model.SenderAddress = settings.SenderAddress;
                model.SenderName = settings.SenderName;
            }).Location("Content:1#General")
              .OnGroup(SettingsGroupId),

            Initialize<EmailDeliverySettingsViewModel>("EmailDeliverySettings_Edit__Retry", model =>
            {
                model.MaxRetries = settings.MaxRetries;
                model.RetryDelaySeconds = settings.RetryDelaySeconds;
            }).Location("Content:2#Retry Policy")
              .OnGroup(SettingsGroupId)
        );
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, EmailDeliverySettings settings, UpdateEditorContext context)
    {
        var model = new EmailDeliverySettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.SenderAddress = model.SenderAddress;
        settings.SenderName = model.SenderName;
        settings.MaxRetries = model.MaxRetries;
        settings.RetryDelaySeconds = model.RetryDelaySeconds;

        return Edit(site, settings, context);
    }
}
```

## Custom Dashboard Widget with Data

This example shows a dashboard widget that queries data and displays it in a card on the admin dashboard:

```csharp
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class ContentStatsDashboardWidgetDriver : DisplayDriver<DashboardCard>
{
    private readonly IContentStatisticsService _statisticsService;

    public ContentStatsDashboardWidgetDriver(IContentStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    public override IDisplayResult Display(DashboardCard model, BuildDisplayContext context)
    {
        return Initialize<ContentStatsViewModel>("ContentStatsDashboard", async viewModel =>
        {
            var stats = await _statisticsService.GetStatisticsAsync();
            viewModel.TotalPublished = stats.TotalPublished;
            viewModel.TotalDraft = stats.TotalDraft;
            viewModel.RecentlyModified = stats.RecentlyModified;
        }).Location("DetailAdmin", "Content:2.5");
    }
}
```

Dashboard widget view `Views/ContentStatsDashboard.cshtml`:

```html
@model ContentStatsViewModel

<div class="col-sm-6 col-lg-3">
    <div class="card text-bg-light mb-3">
        <div class="card-header">
            <h5 class="card-title mb-0">@T["Content Overview"]</h5>
        </div>
        <div class="card-body">
            <div class="row text-center">
                <div class="col">
                    <h3 class="mb-0">@Model.TotalPublished</h3>
                    <small class="text-muted">@T["Published"]</small>
                </div>
                <div class="col">
                    <h3 class="mb-0">@Model.TotalDraft</h3>
                    <small class="text-muted">@T["Drafts"]</small>
                </div>
                <div class="col">
                    <h3 class="mb-0">@Model.RecentlyModified</h3>
                    <small class="text-muted">@T["Modified Today"]</small>
                </div>
            </div>
        </div>
    </div>
</div>
```

## Admin Result Filter for Injecting Layout Content

This example injects a maintenance notification banner into the admin `Messages` zone when a flag is active:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.Settings;

public sealed class MaintenanceBannerAdminFilter : IAsyncResultFilter
{
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISiteService _siteService;

    public MaintenanceBannerAdminFilter(
        ILayoutAccessor layoutAccessor,
        IShapeFactory shapeFactory,
        ISiteService siteService)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
        _siteService = siteService;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is not ViewResult || !AdminAttribute.IsApplied(context.HttpContext))
        {
            await next();
            return;
        }

        var settings = await _siteService.GetSettingsAsync<MaintenanceSettings>();

        if (settings.IsMaintenanceScheduled)
        {
            var layout = await _layoutAccessor.GetLayoutAsync();
            var messagesZone = layout.Zones["Messages"];

            var banner = await _shapeFactory.CreateAsync("MaintenanceBanner", new
            {
                ScheduledDate = settings.ScheduledDate,
            });

            await messagesZone.AddAsync(banner, "0");
        }

        await next();
    }
}
```

## Complete Startup Registration Example

This ties all admin components together in a single module startup:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

[RequireFeatures("OrchardCore.Admin")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<AdminMenu>();
        services.AddDisplayDriver<ISite, NotificationSettingsDisplayDriver>();
        services.AddDisplayDriver<DashboardCard, ContentStatsDashboardWidgetDriver>();
        services.AddMvcFilter<MaintenanceBannerAdminFilter>();
    }
}
```

## Recipe: Configure Admin Settings at Setup

Use the following recipe to configure admin panel preferences when provisioning a new tenant:

```json
{
    "steps": [
        {
            "name": "settings",
            "AdminSettings": {
                "DisplayMenuFilter": true,
                "DisplayDarkMode": true,
                "DisplayThemeToggler": true
            }
        }
    ]
}
```

## Recipe: Enable Admin Features

Enable specific admin-related features as part of site setup:

```json
{
    "steps": [
        {
            "name": "feature",
            "enable": [
                "OrchardCore.Admin",
                "OrchardCore.AdminDashboard",
                "OrchardCore.Settings",
                "OrchardCore.Navigation"
            ]
        }
    ]
}
```

## Admin Card View Pattern

Admin card-based layouts are useful for overview pages. Here is a pattern for rendering items as cards:

```html
@model AnnouncementIndexViewModel

<zone Name="Title">
    <h1>@T["Announcements"]</h1>
</zone>

<div class="row row-cols-1 row-cols-md-2 row-cols-lg-3 g-3">
    @foreach (var item in Model.Announcements)
    {
        <div class="col">
            <div class="card h-100">
                <div class="card-body">
                    <h5 class="card-title">@item.Title</h5>
                    <p class="card-text text-muted">@item.Message</p>
                </div>
                <div class="card-footer d-flex justify-content-between align-items-center">
                    <span class="badge @(item.IsActive ? "bg-success" : "bg-secondary")">
                        @(item.IsActive ? T["Active"] : T["Inactive"])
                    </span>
                    <a asp-action="Edit" asp-route-id="@item.Id" class="btn btn-sm btn-outline-primary">@T["Edit"]</a>
                </div>
            </div>
        </div>
    }
</div>
```

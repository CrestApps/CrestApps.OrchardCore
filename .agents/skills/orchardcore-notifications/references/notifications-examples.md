# Notification Examples

## Example 1: Notify Admins on Content Submission

Send a notification to all administrators when a new content item is submitted for review.

### Content Event Handler

```csharp
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Notifications;
using OrchardCore.Users.Services;

public sealed class ContentReviewNotificationHandler : ContentHandlerBase
{
    private readonly INotificationService _notificationService;
    private readonly IUserService _userService;
    private readonly IStringLocalizer S;

    public ContentReviewNotificationHandler(
        INotificationService notificationService,
        IUserService userService,
        IStringLocalizer<ContentReviewNotificationHandler> stringLocalizer)
    {
        _notificationService = notificationService;
        _userService = userService;
        S = stringLocalizer;
    }

    public override async Task PublishedAsync(PublishContentContext context)
    {
        if (context.ContentItem.ContentType != "BlogPost")
        {
            return;
        }

        var notification = new Notification
        {
            Summary = S["New blog post submitted: {0}", context.ContentItem.DisplayText],
            Body = S["A new blog post has been submitted and is ready for review."],
        };

        var admins = await _userService.GetUsersInRoleAsync("Administrator");

        foreach (var admin in admins)
        {
            await _notificationService.SendAsync(admin, notification);
        }
    }
}
```

### Registration

```csharp
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentHandler, ContentReviewNotificationHandler>();
    }
}
```

## Example 2: Scheduled Notification Digest

A background task that sends a daily digest notification summarizing activity.

### Background Task

```csharp
using OrchardCore.BackgroundTasks;
using OrchardCore.Notifications;
using OrchardCore.Users.Services;

[BackgroundTask(
    Schedule = "0 8 * * *",
    Description = "Sends a daily activity digest notification to editors.")]
public sealed class DailyDigestNotificationTask : IBackgroundTask
{
    private readonly INotificationService _notificationService;
    private readonly IUserService _userService;
    private readonly ISession _session;

    public DailyDigestNotificationTask(
        INotificationService notificationService,
        IUserService userService,
        ISession session)
    {
        _notificationService = notificationService;
        _userService = userService;
        _session = session;
    }

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var yesterday = DateTime.UtcNow.AddDays(-1);

        var recentCount = await _session.Query<ContentItem>()
            .Where(ci => ci.CreatedUtc >= yesterday)
            .CountAsync();

        if (recentCount == 0)
        {
            return;
        }

        var notification = new Notification
        {
            Summary = $"Daily Digest: {recentCount} new items published yesterday",
            Body = $"There were {recentCount} content items published in the last 24 hours.",
        };

        var editors = await _userService.GetUsersInRoleAsync("Editor");

        foreach (var editor in editors)
        {
            await _notificationService.SendAsync(editor, notification);
        }
    }
}
```

## Example 3: Notification with Email Delivery

Configure notifications to also be delivered via email by enabling the email notification method.

### Enabling Email Notifications via Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Notifications",
        "OrchardCore.Notifications.Email",
        "OrchardCore.Email.Smtp"
      ],
      "disable": []
    }
  ]
}
```

### Sending a Notification with Email Fallback

```csharp
using OrchardCore.Notifications;

public sealed class OrderNotificationSender
{
    private readonly INotificationService _notificationService;
    private readonly IStringLocalizer S;

    public OrderNotificationSender(
        INotificationService notificationService,
        IStringLocalizer<OrderNotificationSender> stringLocalizer)
    {
        _notificationService = notificationService;
        S = stringLocalizer;
    }

    public async Task NotifyOrderCompletedAsync(IUser customer, string orderId)
    {
        var notification = new Notification
        {
            Summary = S["Order {0} has been completed", orderId],
            Body = S["Your order has been processed and shipped. Thank you for your purchase."],
        };

        // The notification will be delivered through all enabled methods
        // (in-app and email if OrchardCore.Notifications.Email is enabled).
        await _notificationService.SendAsync(customer, notification);
    }
}
```

## Example 4: Custom Notification Controller

An admin controller that lets administrators send targeted notifications.

### Controller

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Notifications;
using OrchardCore.Users.Services;

[Admin]
public sealed class NotificationAdminController : Controller
{
    private readonly INotificationService _notificationService;
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IStringLocalizer S;

    public NotificationAdminController(
        INotificationService notificationService,
        IUserService userService,
        IAuthorizationService authorizationService,
        IStringLocalizer<NotificationAdminController> stringLocalizer)
    {
        _notificationService = notificationService;
        _userService = userService;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    [HttpPost]
    public async Task<IActionResult> Send(string userName, string summary, string body)
    {
        if (!await _authorizationService.AuthorizeAsync(
            User,
            NotificationPermissions.ManageNotifications))
        {
            return Forbid();
        }

        var targetUser = await _userService.GetUserByNameAsync(userName);

        if (targetUser == null)
        {
            ModelState.AddModelError(nameof(userName), S["User not found."]);
            return BadRequest(ModelState);
        }

        var notification = new Notification
        {
            Summary = summary,
            Body = body,
        };

        await _notificationService.SendAsync(targetUser, notification);

        return Ok();
    }
}
```

## Example 5: Notification Workflow Integration

A workflow that triggers a notification when a content approval event occurs.

### Workflow Structure

```
ContentPublishedEvent (Article) → CustomNotifyTask (notify author) → LogTask (log result)
```

### Workflow Activity Registration

```csharp
using OrchardCore.Modules;
using OrchardCore.Workflows.Activities;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<CustomNotifyTask, CustomNotifyTaskDisplayDriver>();
    }
}
```

### Workflow Activity Display Driver

```csharp
using OrchardCore.Workflows.Display;

public sealed class CustomNotifyTaskDisplayDriver : ActivityDisplayDriver<CustomNotifyTask>
{
    public override IDisplayResult Edit(CustomNotifyTask activity)
    {
        return Initialize<CustomNotifyTaskViewModel>("CustomNotifyTask_Edit", model =>
        {
            model.UserName = activity.UserName;
            model.NotificationSummary = activity.NotificationSummary;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(
        CustomNotifyTask activity,
        IUpdateModel updater)
    {
        var model = new CustomNotifyTaskViewModel();
        await updater.TryUpdateModelAsync(model, Prefix);
        activity.UserName = model.UserName;
        activity.NotificationSummary = model.NotificationSummary;

        return Edit(activity);
    }
}
```

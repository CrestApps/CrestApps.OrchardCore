using System.Security.Claims;
using CrestApps.OrchardCore.Users.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Navigation;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;

namespace CrestApps.OrchardCore.Users.Controllers;

/// <summary>
/// The site's Notifications page: a signed-in user's own Orchard Core notifications, read and unread, newest first, for
/// people who don't use the admin, where the notification center is. It keeps no notifications of its own: it reads
/// them with Orchard Core's <see cref="INotificationManager"/>, hides those its visibility handlers hide, renders each
/// with the module's own notification shape (the <c>Header</c> display type the navbar uses, whose link opens it through
/// the module's open endpoint, marking it read), and marks all read with the manager. It needs the module's
/// <see cref="NotificationPermissions.ManageNotifications"/> permission, which signed-in users have by default, and is
/// not found while the Notifications feature is off.
/// </summary>
[Authorize]
public sealed class UserNotificationsController : Controller
{
    private const string UnreadStatus = "unread";

    private readonly IAuthorizationService _authorizationService;
    private readonly IServiceProvider _services;
    private readonly IDisplayManager<Notification> _notificationDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ShellDescriptor _shellDescriptor;
    private readonly PagerOptions _pagerOptions;

    public UserNotificationsController(
        IAuthorizationService authorizationService,
        IServiceProvider services,
        IDisplayManager<Notification> notificationDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        ShellDescriptor shellDescriptor,
        IOptions<PagerOptions> pagerOptions)
    {
        _authorizationService = authorizationService;
        _services = services;
        _notificationDisplayManager = notificationDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _shellDescriptor = shellDescriptor;
        _pagerOptions = pagerOptions.Value;
    }

    /// <summary>
    /// Shows a page of the user's notifications.
    /// </summary>
    /// <param name="status"><c>unread</c> for the unread notifications only, otherwise all.</param>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The page.</returns>
    [HttpGet("notifications", Name = UsersConstants.Notifications.RouteName)]
    public async Task<IActionResult> Index(string status, int page = 1)
    {
        if (_services.GetService<INotificationManager>() is not { } notificationManager)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, NotificationPermissions.ManageNotifications))
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var unreadOnly = string.Equals(status, UnreadStatus, StringComparison.OrdinalIgnoreCase);
        var pageSize = Math.Max(1, _pagerOptions.PageSize);

        page = Math.Max(1, page);

        // One more than a page tells whether there are older notifications.
        var notifications = await notificationManager.ListAsync(new NotificationFilter
        {
            UserId = userId,
            IsRead = unreadOnly ? false : null,
            Skip = (page - 1) * pageSize,
            Take = pageSize + 1,
        }, HttpContext.RequestAborted);

        var visible = notifications.Take(pageSize).ToList();

        if (_services.GetService<INotificationVisibilityEvaluator>() is { HasHandlers: true } visibilityEvaluator)
        {
            visible = await visibilityEvaluator.FilterAsync(visible, User, HttpContext.RequestAborted);
        }

        var model = new UserNotificationsViewModel
        {
            UnreadOnly = unreadOnly,
            Page = page,
            HasOlder = notifications.Count > pageSize,
            HasUnread = (await notificationManager.ListAsync(new NotificationFilter { UserId = userId, IsRead = false, Take = 1 }, HttpContext.RequestAborted)).Count > 0,
            InAccount = _shellDescriptor.Features.Any(feature => feature.Id == UsersConstants.Feature.Account),
            CanOpenProfile = await _authorizationService.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel),
        };

        foreach (var notification in visible)
        {
            model.Notifications.Add(await _notificationDisplayManager.BuildDisplayAsync(notification, _updateModelAccessor.ModelUpdater, NotificationConstants.DisplayTypes.Header));
        }

        return View(model);
    }

    /// <summary>
    /// Marks all the user's notifications read.
    /// </summary>
    /// <returns>Back to the page.</returns>
    [HttpPost("notifications/mark-all-read", Name = UsersConstants.Notifications.MarkAllReadRouteName)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        if (_services.GetService<INotificationManager>() is not { } notificationManager)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, NotificationPermissions.ManageNotifications))
        {
            return Forbid();
        }

        await notificationManager.MarkAsReadAsync(new NotificationFilter
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            IsRead = false,
        }, HttpContext.RequestAborted);

        return RedirectToRoute(UsersConstants.Notifications.RouteName);
    }
}

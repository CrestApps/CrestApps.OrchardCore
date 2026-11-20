using System.Security.Claims;
using CrestApps.OrchardCore.Users.Services;
using CrestApps.OrchardCore.Users.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Navigation;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Controllers;

/// <summary>
/// The site's Notifications page: a signed-in user's own Orchard Core notifications, read and unread, newest first, for
/// people who don't use the admin, where the notification center is. It keeps no notifications of its own: it reads
/// them with Orchard Core's <see cref="INotificationManager"/>, hides those its visibility handlers hide, renders each
/// with the module's own notification shape (the <c>Header</c> display type the navbar uses, whose link opens it through
/// the module's open endpoint, marking it read), and marks all read with the manager. It needs the module's
/// <see cref="NotificationPermissions.ManageNotifications"/> permission, which signed-in users have by default, and is
/// not found while the Notifications feature is off. Its preferences page edits the Notifications module's own
/// preferences of the user (the methods they receive, and the methods on for each registered type), which the module
/// otherwise shows only on the admin profile.
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
    private readonly UserManager<IUser> _userManager;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public UserNotificationsController(
        IAuthorizationService authorizationService,
        IServiceProvider services,
        IDisplayManager<Notification> notificationDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        ShellDescriptor shellDescriptor,
        IOptions<PagerOptions> pagerOptions,
        UserManager<IUser> userManager,
        IStringLocalizerFactory localizerFactory,
        INotifier notifier,
        IHtmlLocalizer<UserNotificationsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _services = services;
        _notificationDisplayManager = notificationDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _shellDescriptor = shellDescriptor;
        _pagerOptions = pagerOptions.Value;
        _userManager = userManager;
        _localizerFactory = localizerFactory;
        _notifier = notifier;
        H = htmlLocalizer;
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

    /// <summary>
    /// Shows how each kind of notification reaches the user.
    /// </summary>
    /// <returns>The page.</returns>
    [HttpGet("notifications/preferences", Name = UsersConstants.Notifications.PreferencesRouteName)]
    public async Task<IActionResult> Preferences()
    {
        if (await FindPreferencesUserAsync() is not { } user)
        {
            return RefusePreferences();
        }

        var (methods, types) = PreferencesChoices();
        var choices = UserNotificationPreferencesForm.Read(user.GetOrCreate<UserNotificationPreferencesPart>(), methods.Select(method => method.Method), types);
        var model = new UserNotificationPreferencesViewModel
        {
            InAccount = _shellDescriptor.Features.Any(feature => feature.Id == UsersConstants.Feature.Account),
        };

        foreach (var method in methods)
        {
            model.Methods.Add(new UserNotificationMethodOption
            {
                Method = method.Method,
                Name = method.Name.Value,
                Enabled = choices.EnabledMethods.Contains(method.Method, StringComparer.OrdinalIgnoreCase),
            });
        }

        foreach (var type in types)
        {
            var option = new UserNotificationTypeOption
            {
                Name = type.Name,
                DisplayName = _localizerFactory.Localize(type.DisplayName).Value,
                Description = type.Description is null ? null : _localizerFactory.Localize(type.Description).Value,
            };

            option.EnabledMethods.UnionWith(choices.TypeMethods[type.Name]);
            model.Types.Add(option);
        }

        // By name, as the Notifications module's own editor lists them.
        var sorted = model.Types.OrderBy(type => type.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        model.Types.Clear();

        foreach (var type in sorted)
        {
            model.Types.Add(type);
        }

        return View(model);
    }

    /// <summary>
    /// Saves how each kind of notification reaches the user.
    /// </summary>
    /// <param name="input">The methods the user receives, and the methods on for each type.</param>
    /// <returns>Back to the page.</returns>
    [HttpPost("notifications/preferences")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Preferences))]
    public async Task<IActionResult> PreferencesPost(UserNotificationPreferencesInput input)
    {
        if (await FindPreferencesUserAsync() is not { } user)
        {
            return RefusePreferences();
        }

        var (methods, types) = PreferencesChoices();
        var part = user.GetOrCreate<UserNotificationPreferencesPart>();

        UserNotificationPreferencesForm.Apply(part, methods.Select(method => method.Method), types, input?.EnabledMethods, input?.TypeMethods);
        user.Put(part);

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Your notification preferences are saved."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Your notification preferences could not be saved. Try again."]);
        }

        return RedirectToRoute(UsersConstants.Notifications.PreferencesRouteName);
    }

    // The signed-in user, when Notifications is on and they may manage their notifications; otherwise null.
    private async Task<User> FindPreferencesUserAsync()
    {
        if (_services.GetService<INotificationManager>() is null ||
            !await _authorizationService.AuthorizeAsync(User, NotificationPermissions.ManageNotifications))
        {
            return null;
        }

        return await _userManager.GetUserAsync(User) as User;
    }

    private IActionResult RefusePreferences()
        => _services.GetService<INotificationManager>() is null ? NotFound() : Forbid();

    private (IReadOnlyList<INotificationMethodProvider> Methods, IReadOnlyList<NotificationTypeDescriptor> Types) PreferencesChoices()
        => (
            _services.GetServices<INotificationMethodProvider>().OrderBy(provider => provider.Name.Value, StringComparer.CurrentCultureIgnoreCase).ToList(),
            _services.GetRequiredService<IOptions<NotificationTypeOptions>>().Value.Types.Values.ToList());
}

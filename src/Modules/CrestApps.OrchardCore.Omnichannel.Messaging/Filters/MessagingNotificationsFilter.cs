using System.Security.Claims;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Controllers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Filters;

/// <summary>
/// Puts the messaging notifications on every admin page but the workspace: a toast when a customer writes or a
/// conversation is handed to the user, and the count on the Messaging menu item. Without it an agent reading some
/// other admin page heard nothing until they went back to the inbox, because only the workspace listened.
/// </summary>
public sealed class MessagingNotificationsFilter : IAsyncResultFilter
{
    /// <summary>
    /// The name of the shape that carries the notifications' settings and toast container.
    /// </summary>
    public const string ShapeType = "MessagingNotifications";

    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IResourceManager _resourceManager;
    private readonly AdminOptions _adminOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingNotificationsFilter"/> class.
    /// </summary>
    /// <param name="layoutAccessor">The layout accessor used to add the notifications to the footer zone.</param>
    /// <param name="shapeFactory">The shape factory used to create the notifications shape.</param>
    /// <param name="authorizationService">The authorization service used to limit the notifications to workspace users.</param>
    /// <param name="agentProfileManager">The agent profile manager used to tell the page which agent it is.</param>
    /// <param name="resourceManager">The resource manager used to register the notifications script.</param>
    /// <param name="adminOptions">The admin options used to detect admin pages.</param>
    /// <param name="logger">The logger.</param>
    public MessagingNotificationsFilter(
        ILayoutAccessor layoutAccessor,
        IShapeFactory shapeFactory,
        IAuthorizationService authorizationService,
        IAgentProfileManager agentProfileManager,
        IResourceManager resourceManager,
        IOptions<AdminOptions> adminOptions,
        ILogger<MessagingNotificationsFilter> logger)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
        _authorizationService = authorizationService;
        _agentProfileManager = agentProfileManager;
        _resourceManager = resourceManager;
        _adminOptions = adminOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Only a full admin page has a layout to add to; a JSON payload, a partial or a redirect has none, and an
        // anonymous request has nobody to notify.
        if (context.Result is not (ViewResult or PageResult) ||
            context.HttpContext.User.Identity?.IsAuthenticated != true ||
            !IsAdminPage(context) ||
            IsWorkspacePage(context) ||
            IsSoftPhonePage(context))
        {
            await next();

            return;
        }

        var user = context.HttpContext.User;

        if (!await _authorizationService.AuthorizeAsync(user, MessagingPermissions.UseMessagingWorkspace))
        {
            await next();

            return;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var agent = string.IsNullOrEmpty(userId)
            ? null
            : await _agentProfileManager.FindByUserIdAsync(userId, context.HttpContext.RequestAborted);

        // Without an agent profile the hub puts the connection in no agent or queue group, and without the
        // supervisor permission not in the triage group either, so there would be nothing to hear. The profile is
        // created the first time the user opens the workspace; from then on every page carries the notifications.
        if (agent is null && !await _authorizationService.AuthorizeAsync(user, MessagingPermissions.ViewAllConversations))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Messaging notifications are not added for user {UserId}: they have no agent profile yet, so no notification could reach them.",
                    userId.SanitizeLogValue());
            }

            await next();

            return;
        }

        _resourceManager.RegisterResource("script", MessagingResourceConfiguration.NotificationsScript).AtFoot();

        var shape = await _shapeFactory.CreateAsync(ShapeType);
        shape.Properties["AgentId"] = agent?.ItemId;

        var layout = await _layoutAccessor.GetLayoutAsync();
        await layout.Zones["Footer"].AddAsync(shape, "997");

        await next();
    }

    private bool IsAdminPage(ResultExecutingContext context)
    {
        return context.HttpContext.Request.Path.StartsWithSegments('/' + _adminOptions.AdminUrlPrefix, StringComparison.OrdinalIgnoreCase);
    }

    // The workspace keeps its own connection and toasts, and is the connection that counts as the agent being
    // present. A second, passive one beside it would only raise every toast twice.
    private static bool IsWorkspacePage(ResultExecutingContext context)
        => context.Controller is AdminController;

    // The standalone soft phone page is the phone itself, hosted in its own window or the browser extension. Toasts
    // there would cover the call controls, and a click would navigate the phone window away from a live call.
    private static bool IsSoftPhonePage(ResultExecutingContext context)
    {
        return string.Equals(
            context.ActionDescriptor.AttributeRouteInfo?.Name,
            TelephonyConstants.RouteNames.SoftPhonePage,
            StringComparison.Ordinal);
    }
}

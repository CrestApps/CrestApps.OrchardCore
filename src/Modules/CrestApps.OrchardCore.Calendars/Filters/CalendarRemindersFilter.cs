using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Calendars.Filters;

/// <summary>
/// Puts the calendar reminders on every admin page of a user who uses calendars: the page asks for the user's reminders and
/// shows each as a notice that stays until it is dismissed.
/// </summary>
public sealed class CalendarRemindersFilter : IAsyncResultFilter
{
    /// <summary>
    /// The shape that carries the reminders' settings and the place they show.
    /// </summary>
    public const string ShapeType = "CalendarReminders";

    // The standalone soft phone page is the phone itself; a notice there would cover the call controls.
    private const string SoftPhonePageRouteName = "TelephonySoftPhonePage";

    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly IResourceManager _resourceManager;
    private readonly AdminOptions _adminOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarRemindersFilter"/> class.
    /// </summary>
    /// <param name="layoutAccessor">The layout accessor.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="resourceManager">The resource manager.</param>
    /// <param name="adminOptions">The admin options.</param>
    public CalendarRemindersFilter(
        ILayoutAccessor layoutAccessor,
        IShapeFactory shapeFactory,
        IAuthorizationService authorizationService,
        IResourceManager resourceManager,
        IOptions<AdminOptions> adminOptions)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
        _authorizationService = authorizationService;
        _resourceManager = resourceManager;
        _adminOptions = adminOptions.Value;
    }

    /// <inheritdoc />
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Only a full admin page has a layout to add to; a JSON payload, a partial (such as the event modal) or a redirect
        // has none.
        if (context.Result is not (ViewResult or PageResult) ||
            context.HttpContext.User.Identity?.IsAuthenticated != true ||
            !context.HttpContext.Request.Path.StartsWithSegments('/' + _adminOptions.AdminUrlPrefix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(context.ActionDescriptor.AttributeRouteInfo?.Name, SoftPhonePageRouteName, StringComparison.Ordinal) ||
            !await _authorizationService.AuthorizeAsync(context.HttpContext.User, CalendarPermissions.AccessCalendars))
        {
            await next();

            return;
        }

        _resourceManager.RegisterResource("script", CalendarResourceConfiguration.RemindersScript).AtFoot();

        var layout = await _layoutAccessor.GetLayoutAsync();

        await layout.Zones["Footer"].AddAsync(await _shapeFactory.CreateAsync(ShapeType), "998");

        await next();
    }
}

using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Workforce.Portal;

/// <summary>
/// The worker's punch buttons (clock in, start a meal or a break, clock out), shown on Time Tracker's My Time page so
/// a worker can punch there as well as in My Work.
/// </summary>
public sealed class TimeClockControlsViewComponent : ViewComponent
{
    /// <summary>
    /// The component's name.
    /// </summary>
    public const string Name = "TimeClockControls";

    private readonly MyWorkPortal _portal;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeClockControlsViewComponent"/> class.
    /// </summary>
    /// <param name="portal">The portal, for the signed-in worker.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public TimeClockControlsViewComponent(MyWorkPortal portal, IAuthorizationService authorizationService)
    {
        _portal = portal;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Shows the buttons, or nothing when the person can't punch on the web.
    /// </summary>
    /// <returns>The buttons.</returns>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = HttpContext.User;
        var timeClock = HttpContext.RequestServices.GetService<ITimeClockService>();

        if (timeClock is null
            || !await _authorizationService.AuthorizeAsync(user, WorkforcePermissions.UseWorkforcePortal)
            || !await _authorizationService.AuthorizeAsync(user, WorkforcePermissions.PunchTime)
            || await _portal.GetWorkerAsync(user) is not { } worker)
        {
            return Content(string.Empty);
        }

        var status = await timeClock.GetStatusAsync(worker, HttpContext.RequestAborted);

        return status.Policy is null ? Content(string.Empty) : View(status);
    }
}

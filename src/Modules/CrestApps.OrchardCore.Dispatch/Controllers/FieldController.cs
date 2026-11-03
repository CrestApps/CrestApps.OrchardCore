using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.ViewModels;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Portal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dispatch.Controllers;

/// <summary>
/// The Field page of My Work: the technician's visits, kept on the device so they work offline.
/// </summary>
[Feature(DispatchConstants.Features.Mobile)]
public sealed class FieldController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly MyWorkPortal _portal;
    private readonly DispatchMobileOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="portal">The My Work portal.</param>
    /// <param name="options">The technician app's settings.</param>
    public FieldController(IAuthorizationService authorizationService, MyWorkPortal portal, IOptions<DispatchMobileOptions> options)
    {
        _authorizationService = authorizationService;
        _portal = portal;
        _options = options.Value;
    }

    /// <summary>
    /// Shows the page; its script loads the visits and keeps them for offline use.
    /// </summary>
    /// <returns>The page.</returns>
    [HttpGet("my-work/field", Name = "MyWorkField")]
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.UseWorkforcePortal))
        {
            return Forbid();
        }

        var worker = await _portal.GetWorkerAsync(User);

        if (worker is null)
        {
            return Redirect("~/my-work");
        }

        return View(new FieldViewModel
        {
            Nav = await _portal.NavAsync(User, worker, "field", HttpContext.RequestAborted),
            WorkerId = worker.ItemId,
            OfflineDays = _options.OfflineDays,
            RetentionDays = _options.LocationRetentionDays,
        });
    }
}

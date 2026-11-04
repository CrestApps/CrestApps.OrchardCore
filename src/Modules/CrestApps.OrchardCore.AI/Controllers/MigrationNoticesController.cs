using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Migrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// Dismisses the warning about data migrations that skipped records.
/// </summary>
[Admin]
public sealed class MigrationNoticesController : Controller
{
    private readonly IMigrationSkipLog _skipLog;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationNoticesController"/> class.
    /// </summary>
    /// <param name="skipLog">The log of migration skips.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public MigrationNoticesController(
        IMigrationSkipLog skipLog,
        IAuthorizationService authorizationService)
    {
        _skipLog = skipLog;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Dismisses every migration skip, then returns to the page the warning was on.
    /// </summary>
    /// <param name="returnUrl">The page to return to.</param>
    /// <returns>A redirect.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("ai/migration-notices/dismiss", "AIMigrationNoticesDismiss")]
    public async Task<IActionResult> Dismiss(string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        await _skipLog.DismissAllAsync(HttpContext.RequestAborted);

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : Redirect("~/");
    }
}

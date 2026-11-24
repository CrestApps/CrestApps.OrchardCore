using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// The Push now button of Settings › Commerce › Agent feeds: pushes the products to agent platforms with the saved
/// settings, now, instead of waiting for the daily push.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Feeds)]
public sealed class AgentFeedsAdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly AgentFeedPushService _pushService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentFeedsAdminController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="pushService">The push of the feed over SFTP and to the ACP feed APIs.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AgentFeedsAdminController(
        IAuthorizationService authorizationService,
        AgentFeedPushService pushService,
        INotifier notifier,
        IHtmlLocalizer<AgentFeedsAdminController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _pushService = pushService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Pushes the products now, and goes back to the settings page with how it went.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("agent-feeds/push", "AgentFeedsPush")]
    public async Task<IActionResult> Push()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AgentFeedPermissions.ManageAgentFeeds))
        {
            return Forbid();
        }

        var run = await _pushService.PushAsync(HttpContext.RequestAborted);

        if (run.Sftp is { } result)
        {
            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The feed was pushed over SFTP: {0}", result.Message]);
            }
            else
            {
                await _notifier.ErrorAsync(H["The feed couldn't be pushed over SFTP: {0}", result.Message]);
            }
        }

        foreach (var target in run.Targets)
        {
            if (target.Result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The products were pushed to {0}: {1}", target.Name, target.Result.Message]);
            }
            else
            {
                await _notifier.ErrorAsync(H["The products couldn't be pushed to {0}: {1}", target.Name, target.Result.Message]);
            }
        }

        if (!run.Pushed)
        {
            await _notifier.WarningAsync(H["Nothing to push: turn on the SFTP push or enable a feed API, and save, first."]);
        }

        return RedirectToAction("Index", "Admin", new { area = "OrchardCore.Settings", groupId = AgentFeedSettingsDisplayDriver.GroupId });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// The Push now button of Settings › Commerce › Agent feeds: pushes the products to agent platforms with the saved
/// settings, now, instead of waiting for the daily push.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Feeds)]
public sealed class AgentFeedsAdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;
    private readonly AgentFeedSftpPusher _sftpPusher;
    private readonly AcpFeedPusher _acpPusher;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentFeedsAdminController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="siteService">The site service, for the agent feed settings.</param>
    /// <param name="sftpPusher">The SFTP push.</param>
    /// <param name="acpPusher">The ACP feed API push.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AgentFeedsAdminController(
        IAuthorizationService authorizationService,
        ISiteService siteService,
        AgentFeedSftpPusher sftpPusher,
        AcpFeedPusher acpPusher,
        INotifier notifier,
        IHtmlLocalizer<AgentFeedsAdminController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _siteService = siteService;
        _sftpPusher = sftpPusher;
        _acpPusher = acpPusher;
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

        var settings = await _siteService.GetSettingsAsync<AgentFeedSettings>();
        var pushed = false;

        if (settings.Sftp?.Enabled == true)
        {
            pushed = true;

            var result = await _sftpPusher.PushAsync(HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The feed was pushed over SFTP: {0}", result.Message]);
            }
            else
            {
                await _notifier.ErrorAsync(H["The feed couldn't be pushed over SFTP: {0}", result.Message]);
            }
        }

        if (await _acpPusher.HasTargetsAsync())
        {
            pushed = true;

            var targets = (settings.FeedApiTargets ?? []).ToDictionary(target => target.Id ?? string.Empty, target => target.Name, StringComparer.Ordinal);

            foreach (var (targetId, result) in await _acpPusher.PushAllAsync(HttpContext.RequestAborted))
            {
                var name = targets.GetValueOrDefault(targetId) ?? targetId;

                if (result.Succeeded)
                {
                    await _notifier.SuccessAsync(H["The products were pushed to {0}: {1}", name, result.Message]);
                }
                else
                {
                    await _notifier.ErrorAsync(H["The products couldn't be pushed to {0}: {1}", name, result.Message]);
                }
            }
        }

        if (!pushed)
        {
            await _notifier.WarningAsync(H["Nothing to push: turn on the SFTP push or enable a feed API, and save, first."]);
        }

        return RedirectToAction("Index", "Admin", new { area = "OrchardCore.Settings", groupId = AgentFeedSettingsDisplayDriver.GroupId });
    }
}

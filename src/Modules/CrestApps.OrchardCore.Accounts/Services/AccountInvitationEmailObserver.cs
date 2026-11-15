using System.Net;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// E-mails the invitation link to the address invited.
/// </summary>
internal sealed class AccountInvitationEmailObserver : IAccountInvitationObserver
{
    private readonly IEmailService _emailService;
    private readonly ISiteService _siteService;
    private readonly IContentManager _contentManager;
    private readonly ILogger _logger;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public AccountInvitationEmailObserver(IEmailService emailService, ISiteService siteService, IContentManager contentManager, ILocalClock localClock, ILogger<AccountInvitationEmailObserver> logger, IStringLocalizer<AccountInvitationEmailObserver> stringLocalizer)
    {
        _emailService = emailService;
        _siteService = siteService;
        _contentManager = contentManager;
        _logger = logger;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public async Task InvitedAsync(AccountInvitation invitation, string token, CancellationToken cancellationToken = default)
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var account = await _contentManager.GetAsync(invitation.AccountId, VersionOptions.Published);
        var link = $"{site.BaseUrl?.TrimEnd('/')}/account/invitations/{Uri.EscapeDataString(token)}";
        var expires = await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(invitation.ExpiresUtc, DateTimeKind.Utc)));
        var accountName = WebUtility.HtmlEncode(account?.DisplayText ?? string.Empty);
        var message = string.IsNullOrWhiteSpace(invitation.Message) ? string.Empty : $"<p>{WebUtility.HtmlEncode(invitation.Message)}</p>";
        var result = await _emailService.SendAsync(new MailMessage
        {
            To = invitation.Email,
            Subject = S["You are invited to join {0}", account?.DisplayText ?? site.SiteName],
            HtmlBody = $"<p>{S["You are invited to join {0} on {1}.", accountName, WebUtility.HtmlEncode(site.SiteName ?? string.Empty)]}</p>{message}<p><a href=\"{WebUtility.HtmlEncode(link)}\">{S["Accept the invitation"]}</a></p><p>{S["The link works until {0}. Sign in, or create an account, with this e-mail address to accept it.", expires.ToString("d")]}</p>",
        }, cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("The e-mail of an account invitation could not be sent.");
        }
    }
}

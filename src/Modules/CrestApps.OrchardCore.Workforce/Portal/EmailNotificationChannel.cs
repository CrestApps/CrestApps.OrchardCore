using System.Net;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Email;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.Portal;

/// <summary>
/// Sends workforce notifications to the worker's email address.
/// </summary>
internal sealed class EmailNotificationChannel : IWorkforceNotificationChannel
{
    private readonly IEmailService _emailService;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public EmailNotificationChannel(IEmailService emailService, ISiteService siteService, IStringLocalizer<EmailNotificationChannel> stringLocalizer)
    {
        _emailService = emailService;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public string Name => "Email";

    public string DisplayName => S["Email"];

    public async Task SendAsync(WorkerProfile worker, WorkforceNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(worker.Email))
        {
            return;
        }

        var baseUrl = (await _siteService.GetSiteSettingsAsync()).BaseUrl?.TrimEnd('/');
        var link = string.IsNullOrEmpty(baseUrl) ? null : $"{baseUrl}/{(notification.Url ?? "my-work").TrimStart('/')}";
        var body = $"<p>{WebUtility.HtmlEncode(notification.Body)}</p>";

        if (link is not null)
        {
            body += $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(S["Open My Work"])}</a></p>";
        }

        await _emailService.SendAsync(new MailMessage
        {
            To = worker.Email,
            Subject = notification.Title,
            HtmlBody = body,
        }, cancellationToken: cancellationToken);
    }
}

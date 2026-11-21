using System.Text.Encodings.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundJobs;
using OrchardCore.Email;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// The default <see cref="IEmailSignInCodeSender"/>: the site's email service, after the request.
/// </summary>
internal sealed class EmailSignInCodeSender : IEmailSignInCodeSender
{
    private readonly IEmailService _emailService;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public EmailSignInCodeSender(
        IEmailService emailService,
        ISiteService siteService,
        ILogger<EmailSignInCodeSender> logger,
        IStringLocalizer<EmailSignInCodeSender> stringLocalizer)
    {
        _emailService = emailService;
        _siteService = siteService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task QueueAsync(string userName, string email, string code, CancellationToken cancellationToken = default)
        => HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
            "email-sign-in-code",
            (userName, email, code),
            static (scope, message) => scope.ServiceProvider.GetRequiredService<IEmailSignInCodeSender>().SendAsync(message.userName, message.email, message.code));

    /// <inheritdoc/>
    public async Task SendAsync(string userName, string email, string code, CancellationToken cancellationToken = default)
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var siteName = string.IsNullOrWhiteSpace(site.SiteName) ? S["the site"].Value : site.SiteName;
        var encoder = HtmlEncoder.Default;

        var result = await _emailService.SendAsync(new MailMessage
        {
            To = email,
            Subject = S["Your sign-in code for {0}: {1}", siteName, code],
            TextBody = S["Your sign-in code is {0}. It works for 10 minutes. If you didn't ask for it, ignore this email.", code],
            HtmlBody = $"<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#212529\"><p>{encoder.Encode(S["Your sign-in code for {0} is:", siteName].Value)}</p><p style=\"font-size:28px;font-weight:bold;letter-spacing:6px\">{code}</p><p style=\"color:#6c757d\">{encoder.Encode(S["It works for 10 minutes. If you didn't ask for it, ignore this email.", code].Value)}</p></div>",
        }, cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("A sign-in code couldn't be emailed to user {UserName}.", userName);
        }
    }
}

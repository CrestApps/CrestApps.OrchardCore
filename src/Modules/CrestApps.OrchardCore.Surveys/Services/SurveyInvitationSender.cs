using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Liquid;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Delivers survey invitations and reminders: a notification to a user of the site, an e-mail to anyone else. The
/// collector's subject and message are Liquid with the model Survey, Invitation and Link.
/// </summary>
internal sealed class SurveyInvitationSender : ISurveyInvitationSender
{
    private readonly INotificationService _notifications;
    private readonly IUserService _users;
    private readonly ITemplatedEmailSender _emails;
    private readonly ILiquidTemplateManager _liquid;
    private readonly TemplateOptions _templateOptions;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationSender"/> class.
    /// </summary>
    /// <param name="notifications">The notification service.</param>
    /// <param name="users">The user service.</param>
    /// <param name="emails">The e-mail sender.</param>
    /// <param name="liquid">The Liquid template manager.</param>
    /// <param name="templateOptions">The Liquid template options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyInvitationSender(
        INotificationService notifications,
        IUserService users,
        ITemplatedEmailSender emails,
        ILiquidTemplateManager liquid,
        IOptions<TemplateOptions> templateOptions,
        ILogger<SurveyInvitationSender> logger,
        IStringLocalizer<SurveyInvitationSender> stringLocalizer)
    {
        _notifications = notifications;
        _users = users;
        _emails = emails;
        _liquid = liquid;
        _templateOptions = templateOptions.Value;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(Survey survey, SurveyCollector collector, SurveyInvitation invitation, string link, bool isReminder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(invitation);

        var settings = SurveyInvitationService.Settings(collector);
        var title = string.IsNullOrWhiteSpace(survey.Title) ? survey.Name : survey.Title;
        var subject = await RenderAsync(settings.Subject, survey, invitation, link)
            ?? (isReminder ? S["Reminder: {0}", title].Value : S["You are invited: {0}", title].Value);
        var message = await RenderAsync(settings.Message, survey, invitation, link)
            ?? S["Please take a few minutes to answer {0}: {1}", title, link].Value;

        if (!message.Contains(link, StringComparison.Ordinal))
        {
            message += Environment.NewLine + Environment.NewLine + link;
        }

        if (!string.IsNullOrEmpty(invitation.UserId))
        {
            if (await _users.GetUserByUniqueIdAsync(invitation.UserId) is not { } user)
            {
                return false;
            }

            await _notifications.SendAsync(user, new NotificationMessage
            {
                Subject = subject,
                Summary = subject,
                TextBody = message,
                Type = isReminder ? SurveyNotificationTypes.Reminder : SurveyNotificationTypes.Invitation,
                ReferenceId = invitation.ItemId,
                Url = link,
            }, cancellationToken);

            return true;
        }

        if (string.IsNullOrWhiteSpace(invitation.Email))
        {
            return false;
        }

        return await _emails.SendAsync(new TemplatedEmail
        {
            To = invitation.Email,
            Reference = invitation.ItemId,
        }, _ => Task.FromResult(new TemplatedEmailContent
        {
            Subject = subject,
            Text = message,
        }), cancellationToken);
    }

    private async Task<string> RenderAsync(string template, Survey survey, SurveyInvitation invitation, string link)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        try
        {
            return await _liquid.RenderStringAsync(template, NullEncoder.Default, null, new Dictionary<string, FluidValue>
            {
                ["Survey"] = FluidValue.Create(new Dictionary<string, object> { ["Name"] = survey.Name, ["Title"] = survey.Title }, _templateOptions),
                ["Invitation"] = FluidValue.Create(new Dictionary<string, object> { ["DisplayName"] = invitation.DisplayName, ["Email"] = invitation.Email, ["ExpiresUtc"] = invitation.ExpiresUtc }, _templateOptions),
                ["Link"] = FluidValue.Create(link, _templateOptions),
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "A survey invitation template failed to render.");

            return null;
        }
    }
}

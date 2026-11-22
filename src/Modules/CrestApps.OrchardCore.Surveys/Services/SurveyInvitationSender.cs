using CrestApps.OrchardCore.Core.Liquid;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Delivers survey invitations and reminders: a notification to a user of the site, an e-mail to anyone else. The
/// collector's subject and message are restricted Liquid (<see cref="RestrictedLiquid"/>): a fixed set of filters, a
/// step limit, the site's time zone, and only the values Survey (Name, Title), Invitation (DisplayName, Email,
/// ExpiresUtc), Link and UnsubscribeLink; the site's Liquid objects and filters (users, content, queries) are out of
/// reach of a message that may go to thousands of people.
/// </summary>
internal sealed class SurveyInvitationSender : ISurveyInvitationSender
{
    private readonly INotificationService _notifications;
    private readonly IUserService _users;
    private readonly ITemplatedEmailSender _emails;
    private readonly ILocalClock _localClock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationSender"/> class.
    /// </summary>
    /// <param name="notifications">The notification service.</param>
    /// <param name="users">The user service.</param>
    /// <param name="emails">The e-mail sender.</param>
    /// <param name="localClock">The local clock, for the time zone templates show dates in.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyInvitationSender(
        INotificationService notifications,
        IUserService users,
        ITemplatedEmailSender emails,
        ILocalClock localClock,
        ILogger<SurveyInvitationSender> logger,
        IStringLocalizer<SurveyInvitationSender> stringLocalizer)
    {
        _notifications = notifications;
        _users = users;
        _emails = emails;
        _localClock = localClock;
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

        // Every invitation e-mail carries an unsubscribe link, whatever the collector's message says.
        message += Environment.NewLine + Environment.NewLine + S["To stop getting survey invitations and reminders, open {0}", UnsubscribeLink(link)].Value;

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

    /// <summary>
    /// Gets the unsubscribe link of an invitation link.
    /// </summary>
    /// <param name="link">The invitation link, which ends with the invitation token.</param>
    /// <returns>The unsubscribe link.</returns>
    internal static string UnsubscribeLink(string link)
        => link.TrimEnd('/') + "/unsubscribe";

    private async Task<string> RenderAsync(string template, Survey survey, SurveyInvitation invitation, string link)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        try
        {
            var options = RestrictedLiquid.Options;
            var values = new Dictionary<string, FluidValue>(StringComparer.Ordinal)
            {
                ["Survey"] = FluidValue.Create(new Dictionary<string, object> { ["Name"] = survey.Name, ["Title"] = survey.Title }, options),
                ["Invitation"] = FluidValue.Create(new Dictionary<string, object> { ["DisplayName"] = invitation.DisplayName, ["Email"] = invitation.Email, ["ExpiresUtc"] = invitation.ExpiresUtc }, options),
                ["Link"] = StringValue.Create(link),
                ["UnsubscribeLink"] = StringValue.Create(UnsubscribeLink(link)),
            };

            // Null when the template doesn't parse or uses a filter the restricted set doesn't have; the default text
            // is sent instead.
            var rendered = await RestrictedLiquid.RenderAsync(template, values, NullEncoder.Default, await RestrictedLiquidContexts.CreateAsync(_localClock, S));

            if (rendered is null)
            {
                _logger.LogWarning("A survey invitation template doesn't parse or uses a filter that isn't available; the default text was sent.");
            }

            return rendered;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "A survey invitation template failed to render.");

            return null;
        }
    }
}

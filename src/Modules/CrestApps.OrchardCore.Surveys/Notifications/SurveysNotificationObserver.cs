using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Surveys.Notifications;

/// <summary>
/// Tells respondents when a person graded their response, and the survey's owner when a response needs grading.
/// Anonymous and test responses are never notified.
/// </summary>
internal sealed class SurveysNotificationObserver : ISurveyResponseObserver
{
    private readonly INotificationService _notifications;
    private readonly IUserService _users;
    private readonly AdminOptions _adminOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysNotificationObserver"/> class.
    /// </summary>
    /// <param name="notifications">The notification service.</param>
    /// <param name="users">The user service.</param>
    /// <param name="adminOptions">The admin options.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveysNotificationObserver(
        INotificationService notifications,
        IUserService users,
        IOptions<AdminOptions> adminOptions,
        IStringLocalizer<SurveysNotificationObserver> stringLocalizer)
    {
        _notifications = notifications;
        _users = users;
        _adminOptions = adminOptions.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task GradedAsync(SurveyResponseEventContext context)
    {
        var response = context?.Response;

        // A response scored automatically shows its result at once; only a person's grading is worth a notice.
        if (response is null || response.IsTest || string.IsNullOrEmpty(response.GradedBy) || string.IsNullOrEmpty(response.UserId)
            || context.Survey?.Access?.Anonymity == AnonymityMode.Anonymous)
        {
            return;
        }

        var title = Title(context.Survey);

        await SendAsync(response.UserId, new NotificationMessage
        {
            Subject = S["{0} is graded", title],
            Summary = S["Your response to {0} is graded. Open it to see your result.", title],
            TextBody = S["Your response to {0} is graded. Open it to see your result.", title],
            Type = SurveyNotificationTypes.ResponseGraded,
            ReferenceId = response.ItemId,
            Url = $"~/s/r/{response.ItemId}/review",
        });
    }

    /// <inheritdoc />
    public async Task NeedsGradingAsync(SurveyResponseEventContext context)
    {
        var response = context?.Response;
        var ownerId = context?.Survey?.OwnerId;

        // An anonymous survey sends no notice per response: its time, to the second, would tell the owner when that
        // person handed in (the queue and Grade page show the day only). Its responses wait in the grading queue.
        if (response is null || response.IsTest || string.IsNullOrEmpty(ownerId) || SurveyAnonymity.IsAnonymous(context.Survey))
        {
            return;
        }

        var title = Title(context.Survey);

        await SendAsync(ownerId, new NotificationMessage
        {
            Subject = S["A response to {0} needs grading", title],
            Summary = S["Response {0} to {1} has answers to grade.", response.Number, title],
            TextBody = S["Response {0} to {1} has answers a person must grade.", response.Number, title],
            Type = SurveyNotificationTypes.NeedsGrading,
            ReferenceId = response.ItemId,
            Url = $"~/{_adminOptions.AdminUrlPrefix}/surveys/responses/{response.ItemId}",
        });
    }

    private string Title(Survey survey)
        => string.IsNullOrWhiteSpace(survey?.Title) ? S["your survey"].Value : survey.Title;

    private async Task SendAsync(string userId, NotificationMessage message)
    {
        if (await _users.GetUserByUniqueIdAsync(userId) is not { } user)
        {
            return;
        }

        await _notifications.SendAsync(user, message);
    }
}

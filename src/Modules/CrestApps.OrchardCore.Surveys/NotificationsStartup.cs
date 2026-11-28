using CrestApps.OrchardCore.Surveys.Notifications;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Adds the Surveys notification types: a response graded, and a response to grade.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(
            SurveyNotificationTypes.ResponseGraded,
            LocalizationSource.Create<NotificationsStartup>("Survey response graded"),
            type => type.Description = LocalizationSource.Create<NotificationsStartup>("When a person has graded a survey or quiz response of yours."));
        services.AddNotificationType(
            SurveyNotificationTypes.NeedsGrading,
            LocalizationSource.Create<NotificationsStartup>("Survey responses to grade"),
            type => type.Description = LocalizationSource.Create<NotificationsStartup>("When a response to a survey you own has answers a person must grade."));
        services.AddSurveyResponseObserver<SurveysNotificationObserver>();
    }
}

/// <summary>
/// Adds the invitation notification types when the Survey Invitations feature is on.
/// </summary>
[Feature(SurveysConstants.Features.Invitations)]
public sealed class InvitationNotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(
            SurveyNotificationTypes.Invitation,
            LocalizationSource.Create<InvitationNotificationsStartup>("Survey invitations"),
            type => type.Description = LocalizationSource.Create<InvitationNotificationsStartup>("When you are invited to respond to a survey."));
        services.AddNotificationType(
            SurveyNotificationTypes.Reminder,
            LocalizationSource.Create<InvitationNotificationsStartup>("Survey reminders"),
            type => type.Description = LocalizationSource.Create<InvitationNotificationsStartup>("Reminders of survey invitations you have not answered yet."));
    }
}

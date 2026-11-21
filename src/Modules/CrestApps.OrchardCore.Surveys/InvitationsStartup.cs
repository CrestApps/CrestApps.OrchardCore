using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers personal invitations: links, delivery, reminders and their admin pages.
/// </summary>
[Feature(SurveysConstants.Features.Invitations)]
public sealed class InvitationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveysInvitations();
        services.AddTemplatedEmails();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveyInvitationSender, SurveyInvitationSender>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SurveyInvitationReminderBackgroundTask>());
        services.AddDisplayDriver<SurveyInvitation, SurveyInvitationDisplayDriver>();
    }
}

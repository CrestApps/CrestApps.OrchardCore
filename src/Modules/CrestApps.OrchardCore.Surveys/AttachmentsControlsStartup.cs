using CrestApps.OrchardCore.Attachments;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Drivers.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the file upload question type, and the sweep of the files no answer kept, when Attachments is enabled.
/// </summary>
[RequireFeatures(AttachmentsConstants.Features.Attachments)]
public sealed class AttachmentsControlsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveysFileUploadControl();
        services.AddDisplayDriver<SurveyQuestion, FileUploadSettingsDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SurveyResponseOrphanFilesBackgroundTask>());
    }
}

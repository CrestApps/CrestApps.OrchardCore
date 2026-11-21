using CrestApps.OrchardCore.Attachments;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Drivers.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the file upload question type when Attachments is enabled.
/// </summary>
[RequireFeatures(AttachmentsConstants.Features.Attachments)]
public sealed class AttachmentsControlsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveysFileUploadControl();
        services.AddDisplayDriver<SurveyQuestion, FileUploadSettingsDisplayDriver>();
    }
}

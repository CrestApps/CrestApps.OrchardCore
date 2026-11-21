using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the QTI import format of question banks.
/// </summary>
[Feature(SurveysConstants.Features.Qti)]
public sealed class QtiStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveyImportFormat<QtiQuestionImportFormat>();
    }
}

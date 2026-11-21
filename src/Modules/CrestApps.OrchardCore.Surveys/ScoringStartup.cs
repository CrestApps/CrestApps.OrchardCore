using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers scoring, review, grading, the grading queue and accommodations, and their admin pages.
/// </summary>
public sealed class ScoringStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveysScoring();
        services.AddDisplayDriver<GradingQueueItem, GradingQueueItemDisplayDriver>();
        services.AddDisplayDriver<SurveyOverrideListItem, SurveyOverrideListItemDisplayDriver>();
    }
}

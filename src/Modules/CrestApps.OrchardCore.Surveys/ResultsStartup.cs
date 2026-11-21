using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the answer index and the results aggregates of Surveys.
/// </summary>
public sealed class ResultsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveysResults();
        services.AddScopedIndexProvider<SurveyAnswerIndexProvider>();
        services.AddDataMigration<SurveyAnswerIndexMigrations>();
    }
}

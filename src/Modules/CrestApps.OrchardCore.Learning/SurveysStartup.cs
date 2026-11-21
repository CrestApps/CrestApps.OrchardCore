using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Surveys;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// Ties Learning to Surveys when Surveys is on: course items own surveys, learners answer them through their enrollment,
/// course staff manage and grade them, and responses record course item results.
/// </summary>
[RequireFeatures(SurveysConstants.Features.Surveys)]
public sealed class SurveysStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningSurveysBridge();
    }
}

/// <summary>
/// Adds the course and enrollment of learners' responses to the Surveys data set when Data Pipelines is on.
/// </summary>
[RequireFeatures(SurveysConstants.Features.Surveys, OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class SurveysDataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveyResponseFieldProvider<LearningSurveyResponseFieldProvider>();
    }
}

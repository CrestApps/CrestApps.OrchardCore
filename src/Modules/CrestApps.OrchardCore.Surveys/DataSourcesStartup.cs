using CrestApps.OrchardCore.Surveys.DataSources;
using CrestApps.OrchardCore.Surveys.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// With Data Pipelines Core on, the Surveys data source for reports and data pipelines.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<SurveyDataSetAccess>();
        services.TryAddScoped<SurveyRespondentNames>();
        services.TryAddScoped<SurveyWideDataSetFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveysDataSet, SurveysDataSet>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveysDataSet, SurveyVersionsDataSet>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveysDataSet, SurveyQuestionsDataSet>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveysDataSet, SurveyResponsesDataSet>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveysDataSet, SurveyAnswersDataSet>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveysDataSet, SurveyCategoryScoresDataSet>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveysDataSet, SurveyPageVisitsDataSet>());
        services.AddDataSource<SurveysDataSource>();
    }
}

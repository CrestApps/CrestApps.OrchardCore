using CrestApps.OrchardCore.Surveys.DataSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// With Survey Invitations and Data Pipelines Core on, the SurveyInvitations data set.
/// </summary>
[Feature(SurveysConstants.Features.Invitations)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class InvitationsDataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveysDataSet, SurveyInvitationsDataSet>());
}

using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers survey counters, evenly presented pools, quotas and page time limits.
/// </summary>
public sealed class CollectingExtrasStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveysCollectingExtras();
        services.AddDataMigration<SurveyCountersMigrations>();
        services.AddIndexProvider<SurveyCounterIndexProvider>();
        services.AddSurveyDefinitionValidationContributor<SurveyQuotaValidationContributor>();
        services.AddDisplayDriver<SurveyDefinition, SurveyDefinitionQuotasDisplayDriver>();
    }
}

/// <summary>
/// Raises the quota reached workflow event.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class QuotaWorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveyQuotaObserver, SurveyQuotaWorkflowObserver>());
}

using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the runtime of Surveys: the response service, the runner pages, the responses pages, the ending renderer
/// and the deadline and upkeep tasks.
/// </summary>
public sealed class RuntimeStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(SurveyDeadlineWork.CollectionName));
        services.AddDataMigration<SurveysRuntimeMigrations>();
        services.AddSurveysRuntime();
        services.AddHttpContextAccessor();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveyEndingRenderer, LiquidSurveyEndingRenderer>());
        services.TryAddScoped<SurveyRespondentIdentityFactory>();
        services.TryAddScoped<SurveyErrorMessages>();
        services.TryAddScoped<SurveyRunnerShapes>();
        services.AddDisplayDriver<SurveyResponse, SurveyResponseDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SurveyDeadlineBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SurveyMaintenanceBackgroundTask>());
    }
}

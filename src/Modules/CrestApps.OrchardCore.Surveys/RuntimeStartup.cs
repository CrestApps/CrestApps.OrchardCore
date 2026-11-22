using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Users.Events;

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
        services.AddSurveyEndingRenderer<LiquidSurveyEndingRenderer>();
        services.TryAddScoped<SurveyRespondentIdentityFactory>();
        services.TryAddScoped<SurveyErrorMessages>();
        services.TryAddScoped<SurveyRunnerShapes>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILoginFormEvent, SurveyResumeCookiesLoginEvent>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILogoutFormEvent, SurveyResumeCookiesLogoutEvent>());
        services.AddDisplayDriver<SurveyResponse, SurveyResponseDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SurveyDeadlineBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SurveyMaintenanceBackgroundTask>());
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The respondent pages carry tokens in their addresses (collector and invitation links), so no page under /s
        // tells another site where the visitor came from. Set as the response starts, so it wins over site defaults.
        app.Use(next => context =>
        {
            if (context.Request.Path.StartsWithSegments(SurveyResumeCookies.RespondentPath, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers["Referrer-Policy"] = "no-referrer";

                    return Task.CompletedTask;
                });
            }

            return next(context);
        });
    }
}

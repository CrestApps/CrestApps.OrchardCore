using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Formulas;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Surveys.Core;

/// <summary>
/// Registers the runtime of Surveys: logic, formulas, plans, access rules, participation and the response service.
/// </summary>
public static class SurveysRuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the runtime services.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveysRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMemoryCache();
        services.AddNumberSequence();
        services.AddLeasedWorkQueue<SurveyDeadlineWork>(options =>
        {
            options.CollectionName = SurveyDeadlineWork.CollectionName;
            options.RemoveCompletedItems = true;
        });

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISurveyExpressionFunctionProvider, SurveyFormulaFunctions>());
        services.TryAddScoped<SurveyExpressionFunctionCatalog>();
        services.TryAddScoped<SurveyFormulaService>();

        // Replaces the default that compiles nothing: publishing checks formulas with the real compiler.
        services.RemoveAll<ISurveyFormulaValidator>();
        services.AddScoped<ISurveyFormulaValidator, SurveyFormulaValidator>();

        services.RemoveAll<ISurveyScoreFormulaEvaluator>();
        services.AddScoped<ISurveyScoreFormulaEvaluator, SurveyFormulaScoreEvaluator>();

        services.TryAddScoped<SurveyLogicEngine>();
        services.RemoveAll<ISurveyHiddenQuestionResolver>();
        services.AddScoped<ISurveyHiddenQuestionResolver, SurveyLogicHiddenQuestionResolver>();
        services.TryAddScoped<SurveyPipingRenderer>();
        services.TryAddSingleton<ISurveyPoolCounter, NullSurveyPoolCounter>();
        services.TryAddScoped<SurveyPlanBuilder>();

        // Upkeep follows up survey deletes, so the purger is there wherever upkeep runs.
        services.TryAddScoped<SurveyDataPurger>();
        services.TryAddScoped<SurveyRuntimeVersions>();
        services.TryAddScoped<SurveyRespondentProjector>();
        services.TryAddSingleton<SurveyDefinitionLocalizer>();
        services.TryAddScoped<SurveyPageViewBuilder>();
        services.TryAddScoped<SurveyEndingService>();
        services.AddSurveyDefinitionValidationContributor<SurveyTranslationValidationContributor>();
        services.TryAddScoped<ISurveyWeekStartProvider, SiteCultureWeekStartProvider>();
        services.TryAddScoped<SurveyParticipationService>();
        services.TryAddScoped<SurveyResponseCounter>();
        services.TryAddScoped<SurveyAccessEvaluator>();
        services.TryAddScoped<ISurveyStartCheck, SurveyStartCheck>();
        services.TryAddScoped<SurveyContextGuard>();
        services.TryAddScoped<SurveyResponseService>();
        services.TryAddScoped<ISurveyResponseService>(provider => provider.GetRequiredService<SurveyResponseService>());
        services.TryAddScoped<SurveyMaintenanceService>();
        services.TryAddScoped<SurveyResponseFiles>();
        services.TryAddScoped<SurveyResponseOrphanFileSweeper>();
        services.AddHttpContextAccessor();
        services.TryAddScoped<SurveyResumeCookies>();

        services.TryAddScoped<SecretAttemptLimiter>();
        services.AddSurveyAccessRule<SurveyStatusRule>()
            .AddSurveyAccessRule<AvailabilityWindowRule>()
            .AddSurveyAccessRule<WeeklyScheduleRule>()
            .AddSurveyAccessRule<AudienceRule>()
            .AddSurveyAccessRule<StatementRule>()
            .AddSurveyAccessRule<PasswordRule>()
            .AddSurveyAccessRule<InvitationRule>()
            .AddSurveyAccessRule<RespondentLimitsRule>()
            .AddSurveyAccessRule<MaxResponsesRule>();

        return services;
    }

    /// <summary>
    /// Registers the renderer of ending messages. The last registered renderer renders every ending, so a module
    /// replaces the built-in Liquid renderer by registering its own after it.
    /// </summary>
    /// <typeparam name="TRenderer">The renderer type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveyEndingRenderer<TRenderer>(this IServiceCollection services)
        where TRenderer : class, ISurveyEndingRenderer
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISurveyEndingRenderer, TRenderer>());

        return services;
    }
}

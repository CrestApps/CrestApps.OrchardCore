using CrestApps.Core.AI;
using CrestApps.OrchardCore.Surveys.AI.Drivers;
using CrestApps.OrchardCore.Surveys.AI.Services;
using CrestApps.OrchardCore.Surveys.AI.Tools;
using CrestApps.OrchardCore.Surveys.Core.Schemas;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Surveys.AI;

/// <summary>
/// Registers the Surveys AI services, tools, admin actions and scripts.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<ISurveysAICompleter, SurveysAICompleter>();
        services.TryAddScoped<SurveyDefinitionSchemaBuilder>();
        services.TryAddScoped<SurveyScoringAssistant>();
        services.TryAddScoped<SurveyDraftAssistant>();
        services.TryAddScoped<SurveyAnswerSummarizer>();
        services.TryAddScoped<SurveyTranslationAssistant>();
        services.TryAddScoped<SurveyResultsReader>();
        services.AddDisplayDriver<Survey, SurveyAIActionsDisplayDriver>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, SurveysAIResourceManagementOptionsConfiguration>();
        services.AddShapeTableProvider<SurveyGradingToolsShapeTableProvider>();

        services.AddCoreAITool<ListSurveysTool>(SurveysAIConstants.Tools.ListSurveys)
            .WithTitle(S["List surveys"])
            .WithDescription(S["Lists the surveys, polls and quizzes the user manages or may read the results of."])
            .WithCategory(S["Surveys"])
            .Selectable();

        services.AddCoreAITool<GetSurveyResultsTool>(SurveysAIConstants.Tools.GetSurveyResults)
            .WithTitle(S["Get survey results"])
            .WithDescription(S["Gets the results of a survey the user may read: counts, statistics and a few written answers per question."])
            .WithCategory(S["Surveys"])
            .Selectable();

        services.AddCoreAITool<CreateSurveyDraftTool>(SurveysAIConstants.Tools.CreateSurveyDraft)
            .WithTitle(S["Create survey draft"])
            .WithDescription(S["Drafts a new survey, poll or quiz from instructions or a document, for the user to review and publish."])
            .WithCategory(S["Surveys"])
            .Selectable();
    }
}

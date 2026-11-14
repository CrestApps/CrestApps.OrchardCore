using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Drivers;
using CrestApps.OrchardCore.Learning.Assessments.Migrations;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Navigation;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Assessments;

/// <summary>
/// Registers questions, banks, the quiz item, attempts, their deadlines and the grading pages.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(AttemptDeadlineWork.CollectionName));
        services.AddDataMigration<AssessmentsMigrations>()
            .AddIndexProvider<AssessmentAttemptIndexProvider>()
            .AddIndexProvider<AssessmentOverrideIndexProvider>()
            .AddIndexProvider<GradeScaleIndexProvider>();

        services.AddLearningAssessmentsCore();

        services.AddContentPart<QuestionPart>()
            .UseDisplayDriver<QuestionPartDisplayDriver>();
        services.AddContentPart<ChoiceQuestionPart>()
            .UseDisplayDriver<ChoiceQuestionPartDisplayDriver>();
        services.AddContentPart<TrueFalseQuestionPart>()
            .UseDisplayDriver<TrueFalseQuestionPartDisplayDriver>();
        services.AddContentPart<ShortTextQuestionPart>()
            .UseDisplayDriver<ShortTextQuestionPartDisplayDriver>();
        services.AddContentPart<NumericQuestionPart>()
            .UseDisplayDriver<NumericQuestionPartDisplayDriver>();
        services.AddContentPart<MatchingQuestionPart>()
            .UseDisplayDriver<MatchingQuestionPartDisplayDriver>();
        services.AddContentPart<OrderingQuestionPart>()
            .UseDisplayDriver<OrderingQuestionPartDisplayDriver>();
        services.AddContentPart<FillInBlanksQuestionPart>()
            .UseDisplayDriver<FillInBlanksQuestionPartDisplayDriver>();
        services.AddContentPart<EssayQuestionPart>()
            .UseDisplayDriver<EssayQuestionPartDisplayDriver>();
        services.AddContentPart<QuizPart>()
            .UseDisplayDriver<QuizPartDisplayDriver>()
            .UseDisplayDriver<QuizPartLearnerDisplayDriver>();

        services.AddScoped<GradingScope>();
        services.AddDisplayDriver<AssessmentAttempt, AttemptRowDisplayDriver>();
        services.AddPermissionProvider<AssessmentsPermissionsProvider>();
        services.AddNavigationProvider<AssessmentsAdminMenu>();
        services.AddSingleton<IBackgroundTask, AttemptDeadlineBackgroundTask>();
    }
}

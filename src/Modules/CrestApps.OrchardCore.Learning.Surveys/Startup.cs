using CrestApps.OrchardCore.Learning.Surveys.Core;
using CrestApps.OrchardCore.Learning.Surveys.Drivers;
using CrestApps.OrchardCore.Learning.Surveys.Migrations;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Surveys;

/// <summary>
/// Registers the course survey and choice course items.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<LearningSurveysMigrations>();
        services.AddLearningSurveysCore();
        services.AddContentPart<CourseSurveyPart>()
            .UseDisplayDriver<CourseSurveyPartDisplayDriver>()
            .UseDisplayDriver<CourseSurveyPartLearnerDisplayDriver>()
            .AddHandler<CourseSurveyPartHandler>();
    }
}

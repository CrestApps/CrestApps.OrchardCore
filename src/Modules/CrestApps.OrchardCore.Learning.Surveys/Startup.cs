using CrestApps.OrchardCore.Learning.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Learning.Surveys.Drivers;
using CrestApps.OrchardCore.Learning.Surveys.Migrations;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.Services;
using CrestApps.OrchardCore.PersonalData.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Surveys;

/// <summary>
/// Registers Learning surveys: the feedback survey and choice course items with the survey part, the survey panel of
/// the learner player, the totals page, and personal data.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(SurveysConstants.CollectionName));
        services.AddDataMigration<SurveysMigrations>()
            .AddIndexProvider<SurveyResponseIndexProvider>();

        services.AddLearningSurveysCore();
        services.AddContentPart<SurveyPart>()
            .UseDisplayDriver<SurveyPartDisplayDriver>()
            .UseDisplayDriver<SurveyPartLearnerDisplayDriver>();

        services.AddPersonalDataContributor<SurveysPersonalDataContributor>();
    }
}

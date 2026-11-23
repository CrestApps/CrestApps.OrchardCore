using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Navigation;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the storage, permissions and menu of Surveys. Each area of the module (designer, runtime, scoring,
/// results, integrations) registers its own services in a startup of its own.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options =>
        {
            options.Collections.Add(SurveysConstants.CollectionName);
            options.Collections.Add(SurveysConstants.ResponsesCollectionName);
        });

        services.AddSurveysStorage();

        // The stores sanitize the HTML texts of drafts, versions, templates and bank questions on every save.
        services.TryAddScoped<SurveyContentSanitizer>();
        services.AddDataMigration<SurveysMigrations>();
        services.AddDataMigration<SurveyAnonymityMigrations>();
        services.AddDataMigration<SurveyContentSanitizingMigrations>();

        // Data the migrations change on existing records is upgraded in the background, a page at a time.
        services.AddResumableStep<SurveyInvitationsReindexStep>();
        services.AddResumableStep<SurveyAnonymityUpgradeStep>();
        services.AddResumableStep<SurveyContentSanitizingStep>();
        services.AddIndexProvider<SurveyIndexProvider>()
            .AddIndexProvider<SurveyTemplateIndexProvider>()
            .AddIndexProvider<SurveyVersionIndexProvider>()
            .AddIndexProvider<QuestionBankIndexProvider>()
            .AddIndexProvider<BankQuestionIndexProvider>()
            .AddIndexProvider<BankQuestionTagIndexProvider>()
            .AddIndexProvider<SurveyCollectorIndexProvider>()
            .AddIndexProvider<SurveyResponseIndexProvider>()
            .AddIndexProvider<SurveyParticipationIndexProvider>()
            .AddIndexProvider<SurveyInvitationIndexProvider>()
            .AddIndexProvider<SurveyOverrideIndexProvider>();

        services.AddPermissionProvider<SurveysPermissionProvider>();
        services.AddNavigationProvider<SurveysAdminMenu>();
    }
}

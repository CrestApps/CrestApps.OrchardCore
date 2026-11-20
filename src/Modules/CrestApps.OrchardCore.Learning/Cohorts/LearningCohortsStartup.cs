using CrestApps.OrchardCore.Learning.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Cohorts;

/// <summary>
/// Registers enrollment by cohort: the Cohorts card of the course editor, the sync that enrolls the people who join a
/// linked cohort and takes off the ones who leave, and the course target. Learning Programs adds the path target.
/// </summary>
[Feature(LearningConstants.Features.Cohorts)]
public sealed class LearningCohortsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<LearningCohortsMigrations>()
            .AddIndexProvider<CohortLearningLinkIndexProvider>();
        services.AddScoped<CohortLearningSync>();
        services.AddSingleton<IBackgroundTask, LearningCohortCatchUpBackgroundTask>();
        services.AddCohortMembershipObserver<LearningCohortMembershipObserver>();
        services.AddCohortUsage<LearningCohortUsage>();
        services.AddScoped<IContentHandler, LearningCohortsContentHandler>();
        services.AddScoped<IContentDisplayDriver, LearningCohortsDisplayDriver>();
        services.AddCohortLearningTarget<CourseCohortLearningTarget>();
        services.AddEnrollmentSource(LearningConstants.EnrollmentSources.Cohort, LocalizationSource.Create<LearningCohortsStartup>("Cohort"));
    }
}

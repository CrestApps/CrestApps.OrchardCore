using CrestApps.OrchardCore.KnowledgeBase.Drivers;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Migrations;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Recipes;
using CrestApps.OrchardCore.KnowledgeBase.Reports;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.KnowledgeBase;

/// <summary>
/// Registers the Knowledge Base.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<KnowledgeArticlePart>()
            .UseDisplayDriver<KnowledgeArticlePartDisplayDriver>();
        services.AddIndexProvider<KnowledgeArticleIndexProvider>();
        services.AddDataMigration<KnowledgeBaseMigrations>();
        services.AddScoped<KnowledgeArticleSearch>();
        services.AddScoped<KnowledgeBaseCategories>();
        services.AddScoped<KnowledgeBaseHelpCenter>();

        // Readers' answers to "Was this helpful?", kept in the Knowledge Base's own collection.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(KnowledgeBaseConstants.CollectionName));
        services.AddIndexProvider<KnowledgeArticleFeedbackIndexProvider>();
        services.AddScoped<KnowledgeArticleFeedbackService>();
        services.AddPersonalDataContributor<KnowledgeArticleFeedbackPersonalDataContributor>();
        services.AddPermissionProvider<KnowledgeBasePermissionProvider>();

        // The help center's widgets.
        services.AddScoped<KnowledgeBasePageContext>();
        services.AddContentPart<HelpCenterSearchPart>()
            .UseDisplayDriver<HelpCenterSearchPartDisplayDriver>();
        services.AddContentPart<KnowledgeBaseCategoriesPart>()
            .UseDisplayDriver<KnowledgeBaseCategoriesPartDisplayDriver>();
        services.AddContentPart<RelatedArticlesPart>()
            .UseDisplayDriver<RelatedArticlesPartDisplayDriver>();
    }
}

/// <summary>
/// The Knowledge Base Feedback report, under Reports.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReport<KnowledgeBaseFeedbackReport>();
    }
}

/// <summary>
/// Readers' answers to "Was this helpful?" as a data set for Data Pipelines and the Report Builder.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services
            .AddDataSource<KnowledgeBaseReportDataSource>()
            .AddScoped<KnowledgeArticleFeedbackDataSet>();
}

/// <summary>
/// The KnowledgeBaseZones recipe step, which the Knowledge Base recipe runs.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<KnowledgeBaseZonesRecipeStep>();
}

/// <summary>
/// Describes the KnowledgeBaseZones recipe step to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IRecipeStep, KnowledgeBaseZonesRecipeStepSchema>();
}

/// <summary>
/// Holds each visitor address to a number of "was this helpful?" answers, when Orchard Core's Rate Limits feature is on,
/// so a script can't push an article up the help center.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RateLimitsStartup : StartupBase
{
    /// <summary>
    /// Answers a visitor address may give in ten minutes.
    /// </summary>
    public const int FeedbackPerTenMinutes = 20;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.Configure<RateLimitsOptions>(options => options.AddGroupRateLimit(
            KnowledgeBaseConstants.FeedbackRateLimitGroup,
            RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(KnowledgeBaseConstants.FeedbackRateLimitGroup, FeedbackPerTenMinutes, TimeSpan.FromMinutes(10))));
}

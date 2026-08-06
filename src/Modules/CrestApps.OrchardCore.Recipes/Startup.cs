using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu.Nodes;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps.Sources;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Mvc.LocationExpander;

namespace CrestApps.OrchardCore.Recipes;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RecipeExecutionService>();
        services.AddScoped<RecipeSchemaService>();
        services.AddScoped<IContentItemSchemaService, ContentItemSchemaService>();
        services.AddSingleton<IViewLocationExpanderProvider, DeploymentJsonViewLocationExpander>();

        services.AddScoped<IRecipeStep, SettingsRecipeStep>();
        services.AddScoped<IRecipeStep, CustomSettingsRecipeStep>();
        services.AddScoped<IRecipeStep, CommandRecipeStep>();
        services.AddScoped<IRecipeStep, RecipesRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the FeatureRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Features")]
public sealed class FeatureRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IFeatureSchemaProvider, OrchardFeatureSchemaProvider>();
        services.AddScoped<IRecipeStep, FeatureRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the ThemesRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Themes")]
public sealed class ThemesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ThemesRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the ContentTypes feature.
/// </summary>
[RequireFeatures("OrchardCore.ContentTypes")]
public sealed class ContentTypesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaProvider, OrchardContentSchemaProvider>();
        services.AddScoped<IRecipeStep, ContentDefinitionRecipeStep>();
        services.AddScoped<IRecipeStep, ReplaceContentDefinitionRecipeStep>();
        services.AddScoped<IRecipeStep, DeleteContentDefinitionRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the ContentRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Contents")]
public sealed class ContentRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ContentRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the UsersRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Users")]
public sealed class UsersRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, UsersRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the CustomUserSettingsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Users.CustomUserSettings")]
public sealed class CustomUserSettingsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, CustomUserSettingsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the MediaRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Media")]
public sealed class MediaRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, MediaRecipeStep>();
        services.AddScoped<IRecipeStep, MediaProfilesRecipeStep>();
        services.AddScoped<IRecipeStep, MoveAttachedMediaFieldsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the RolesRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Roles")]
public sealed class RolesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, RolesRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the WorkflowRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowActivitySchemaService, WorkflowActivitySchemaService>();
        services.AddScoped<IRecipeStep, WorkflowTypeRecipeStep>();

        services
            .AddWorkflowActivitySchema<CommitTransactionTaskSchema>()
            .AddWorkflowActivitySchema<CorrelateTaskSchema>()
            .AddWorkflowActivitySchema<ForEachTaskSchema>()
            .AddWorkflowActivitySchema<ForkTaskSchema>()
            .AddWorkflowActivitySchema<ForLoopTaskSchema>()
            .AddWorkflowActivitySchema<IfElseTaskSchema>()
            .AddWorkflowActivitySchema<JoinTaskSchema>()
            .AddWorkflowActivitySchema<LiquidTaskSchema>()
            .AddWorkflowActivitySchema<LogTaskSchema>()
            .AddWorkflowActivitySchema<NotifyTaskSchema>()
            .AddWorkflowActivitySchema<ScriptTaskSchema>()
            .AddWorkflowActivitySchema<SetOutputTaskSchema>()
            .AddWorkflowActivitySchema<SetPropertyTaskSchema>()
            .AddWorkflowActivitySchema<WhileLoopTaskSchema>()
            .AddWorkflowActivitySchema<WorkflowFaultEventSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the WorkflowHttpRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Workflows.Http")]
public sealed class WorkflowHttpRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<HttpRedirectTaskSchema>()
            .AddWorkflowActivitySchema<HttpRequestEventSchema>()
            .AddWorkflowActivitySchema<HttpRequestFilterEventSchema>()
            .AddWorkflowActivitySchema<HttpRequestTaskSchema>()
            .AddWorkflowActivitySchema<HttpResponseTaskSchema>()
            .AddWorkflowActivitySchema<SignalEventSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the WorkflowTimersRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Workflows.Timers")]
public sealed class WorkflowTimersRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<TimerEventSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the WorkflowUserTasksRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Contents", "OrchardCore.Roles")]
public sealed class WorkflowUserTasksRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<UserTaskEventSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schemas for the content activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Contents")]
public sealed class ContentWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<ContentCreatedEventSchema>()
            .AddWorkflowActivitySchema<ContentDeletedEventSchema>()
            .AddWorkflowActivitySchema<ContentDraftSavedEventSchema>()
            .AddWorkflowActivitySchema<ContentPublishedEventSchema>()
            .AddWorkflowActivitySchema<ContentUnpublishedEventSchema>()
            .AddWorkflowActivitySchema<ContentUpdatedEventSchema>()
            .AddWorkflowActivitySchema<ContentVersionedEventSchema>()
            .AddWorkflowActivitySchema<CreateContentTaskSchema>()
            .AddWorkflowActivitySchema<DeleteContentTaskSchema>()
            .AddWorkflowActivitySchema<PublishContentTaskSchema>()
            .AddWorkflowActivitySchema<RetrieveContentTaskSchema>()
            .AddWorkflowActivitySchema<UnpublishContentTaskSchema>()
            .AddWorkflowActivitySchema<UpdateContentTaskSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schema for the email activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Email")]
public sealed class EmailWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<EmailTaskSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schema for the SMS activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Sms")]
public sealed class SmsWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<SmsTaskSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schemas for the form activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Forms")]
public sealed class FormsWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<AddModelValidationErrorTaskSchema>()
            .AddWorkflowActivitySchema<BindModelStateTaskSchema>()
            .AddWorkflowActivitySchema<HttpRedirectToFormLocationTaskSchema>()
            .AddWorkflowActivitySchema<ValidateAntiforgeryTokenTaskSchema>()
            .AddWorkflowActivitySchema<ValidateFormFieldTaskSchema>()
            .AddWorkflowActivitySchema<ValidateFormTaskSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schemas for the notification activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Notifications")]
public sealed class NotificationsWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<NotifyContentOwnerTaskSchema>()
            .AddWorkflowActivitySchema<NotifyUserTaskSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schema for the ReCaptcha activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.ReCaptcha")]
public sealed class ReCaptchaWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<ValidateReCaptchaTaskSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schemas for the role activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Roles")]
public sealed class RolesWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<GetUsersByRoleTaskSchema>()
            .AddWorkflowActivitySchema<UnassignUserRoleTaskSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schemas for the user activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Users")]
public sealed class UsersWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<AssignUserRoleTaskSchema>()
            .AddWorkflowActivitySchema<RegisterUserTaskSchema>()
            .AddWorkflowActivitySchema<UserConfirmedEventSchema>()
            .AddWorkflowActivitySchema<UserCreatedEventSchema>()
            .AddWorkflowActivitySchema<UserDeletedEventSchema>()
            .AddWorkflowActivitySchema<UserDisabledEventSchema>()
            .AddWorkflowActivitySchema<UserEnabledEventSchema>()
            .AddWorkflowActivitySchema<UserLoggedInEventSchema>()
            .AddWorkflowActivitySchema<UserUpdatedEventSchema>()
            .AddWorkflowActivitySchema<ValidateUserTaskSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schemas for the tenant activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Tenants")]
public sealed class TenantsWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<CreateTenantTaskSchema>()
            .AddWorkflowActivitySchema<DisableTenantTaskSchema>()
            .AddWorkflowActivitySchema<EnableTenantTaskSchema>()
            .AddWorkflowActivitySchema<SetupTenantTaskSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schema for the Twitter activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Twitter")]
public sealed class TwitterWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<UpdateTwitterStatusTaskSchema>();
    }
}

/// <summary>
/// Registers the workflow activity schemas for the artificial intelligence activities.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", "CrestApps.OrchardCore.AI")]
public sealed class AIWorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<AIChatSessionAllFieldsExtractedEventSchema>()
            .AddWorkflowActivitySchema<AIChatSessionClosedEventSchema>()
            .AddWorkflowActivitySchema<AIChatSessionFieldExtractedEventSchema>()
            .AddWorkflowActivitySchema<AIChatSessionPostProcessedEventSchema>()
            .AddWorkflowActivitySchema<AICompletionFromProfileTaskSchema>()
            .AddWorkflowActivitySchema<AICompletionWithConfigTaskSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the LayersRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Layers")]
public sealed class LayersRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRuleSchemaService, RuleSchemaService>();

        services
            .AddRuleConditionSchema<AllConditionGroupSchema>()
            .AddRuleConditionSchema<AnyConditionGroupSchema>()
            .AddRuleConditionSchema<BooleanConditionSchema>()
            .AddRuleConditionSchema<ContentTypeConditionSchema>()
            .AddRuleConditionSchema<CultureConditionSchema>()
            .AddRuleConditionSchema<HomepageConditionSchema>()
            .AddRuleConditionSchema<IsAnonymousConditionSchema>()
            .AddRuleConditionSchema<IsAuthenticatedConditionSchema>()
            .AddRuleConditionSchema<JavascriptConditionSchema>()
            .AddRuleConditionSchema<RoleConditionSchema>()
            .AddRuleConditionSchema<UrlConditionSchema>();

        services
            .AddRuleConditionOperatorSchema<StringContainsOperatorSchema>()
            .AddRuleConditionOperatorSchema<StringEndsWithOperatorSchema>()
            .AddRuleConditionOperatorSchema<StringEqualsOperatorSchema>()
            .AddRuleConditionOperatorSchema<StringNotContainsOperatorSchema>()
            .AddRuleConditionOperatorSchema<StringNotEndsWithOperatorSchema>()
            .AddRuleConditionOperatorSchema<StringNotEqualsOperatorSchema>()
            .AddRuleConditionOperatorSchema<StringNotStartsWithOperatorSchema>()
            .AddRuleConditionOperatorSchema<StringStartsWithOperatorSchema>();

        services.AddScoped<IRecipeStep, LayersRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the QueriesRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Queries")]
public sealed class QueriesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, QueriesRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the TemplatesRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Templates")]
public sealed class TemplatesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TemplatesRecipeStep>();
        services.AddScoped<IRecipeStep, AdminTemplatesRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the ShortcodeTemplatesRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Shortcodes.Templates")]
public sealed class ShortcodeTemplatesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ShortcodeTemplatesRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the PlacementsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Placements")]
public sealed class PlacementsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, PlacementsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the AdminMenuRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.AdminMenu")]
public sealed class AdminMenuRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAdminMenuSchemaService, AdminMenuSchemaService>();

        services
            .AddAdminNodeSchema<LinkAdminNodeSchema>()
            .AddAdminNodeSchema<PlaceholderAdminNodeSchema>();

        services.AddScoped<IRecipeStep, AdminMenuRecipeStep>();
    }
}

/// <summary>
/// Registers the admin menu node schema contributed by the content types feature.
/// </summary>
[RequireFeatures("OrchardCore.AdminMenu", "OrchardCore.Contents")]
public sealed class AdminMenuContentsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAdminNodeSchema<ContentTypesAdminNodeSchema>();
    }
}

/// <summary>
/// Registers the admin menu node schema contributed by the lists feature.
/// </summary>
[RequireFeatures("OrchardCore.AdminMenu", "OrchardCore.Lists")]
public sealed class AdminMenuListsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAdminNodeSchema<ListsAdminNodeSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the DeploymentRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDeploymentSchemaService, DeploymentSchemaService>();

        services.AddDeploymentStepSchema<AllContentDeploymentStepSchema>();
        services.AddDeploymentStepSchema<ContentDeploymentStepSchema>();
        services.AddDeploymentStepSchema<ContentItemDeploymentStepSchema>();
        services.AddDeploymentStepSchema<ContentDefinitionDeploymentStepSchema>();
        services.AddDeploymentStepSchema<ReplaceContentDefinitionDeploymentStepSchema>();
        services.AddDeploymentStepSchema<DeleteContentDefinitionDeploymentStepSchema>();
        services.AddDeploymentStepSchema<CustomFileDeploymentStepSchema>();
        services.AddDeploymentStepSchema<RecipeFileDeploymentStepSchema>();
        services.AddDeploymentStepSchema<JsonRecipeDeploymentStepSchema>();
        services.AddDeploymentStepSchema<DeploymentPlanDeploymentStepSchema>();
        services.AddDeploymentStepSchema<MediaDeploymentStepSchema>();
        services.AddDeploymentStepSchema<CustomSettingsDeploymentStepSchema>();
        services.AddDeploymentStepSchema<CustomUserSettingsDeploymentStepSchema>();
        services.AddDeploymentStepSchema<QueryBasedContentDeploymentStepSchema>();
        services.AddDeploymentStepSchema<AllTemplatesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<AllAdminTemplatesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<AllFeaturesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<SiteSettingsDeploymentStepSchema>();
        services.AddDeploymentStepSchema<TranslationsDeploymentStepSchema>();
        services.AddDeploymentStepSchema<LuceneIndexDeploymentStepSchema>();
        services.AddDeploymentStepSchema<LuceneIndexRebuildDeploymentStepSchema>();
        services.AddDeploymentStepSchema<LuceneIndexResetDeploymentStepSchema>();
        services.AddDeploymentStepSchema<ElasticsearchIndexDeploymentStepSchema>();
        services.AddDeploymentStepSchema<ElasticsearchIndexRebuildDeploymentStepSchema>();
        services.AddDeploymentStepSchema<ElasticsearchIndexResetDeploymentStepSchema>();
        services.AddDeploymentStepSchema<AzureAISearchIndexDeploymentStepSchema>();
        services.AddDeploymentStepSchema<AzureAISearchIndexRebuildDeploymentStepSchema>();
        services.AddDeploymentStepSchema<AzureAISearchIndexResetDeploymentStepSchema>();
        services.AddDeploymentStepSchema<IndexProfileDeploymentStepSchema>();
        services.AddDeploymentStepSchema<ResetIndexDeploymentStepSchema>();
        services.AddDeploymentStepSchema<RebuildIndexDeploymentStepSchema>();

        services.AddScoped<IRecipeStep, DeploymentRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the SitemapsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Sitemaps")]
public sealed class SitemapsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISitemapSchemaService, SitemapSchemaService>();

        services
            .AddSitemapSourceSchema<ContentTypesSitemapSourceSchema>()
            .AddSitemapSourceSchema<CustomPathSitemapSourceSchema>()
            .AddSitemapSourceSchema<SitemapIndexSourceSchema>();

        services.AddScoped<IRecipeStep, SitemapsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the UrlRewritingRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.UrlRewriting")]
public sealed class UrlRewritingRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, UrlRewritingRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the TranslationsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.DataLocalization")]
public sealed class TranslationsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TranslationsRecipeStep>();
        services.AddScoped<IRecipeStep, DynamicDataTranslationsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the FeatureProfilesRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Tenants.FeatureProfiles")]
public sealed class FeatureProfilesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, FeatureProfilesRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the LuceneRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Lucene")]
public sealed class LuceneRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LuceneIndexRecipeStep>();
        services.AddScoped<IRecipeStep, LuceneIndexResetRecipeStep>();
        services.AddScoped<IRecipeStep, LuceneIndexRebuildRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the ElasticRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Elasticsearch")]
public sealed class ElasticRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ElasticIndexSettingsRecipeStep>();
        services.AddScoped<IRecipeStep, ElasticIndexResetRecipeStep>();
        services.AddScoped<IRecipeStep, ElasticIndexRebuildRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the AzureAISearchRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.AzureAI")]
public sealed class AzureAISearchRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AzureAIIndexCreateRecipeStep>();
        services.AddScoped<IRecipeStep, AzureAIIndexResetRecipeStep>();
        services.AddScoped<IRecipeStep, AzureAIIndexRebuildRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the IndexProfileRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Indexing")]
public sealed class IndexProfileRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, CreateOrUpdateIndexProfileRecipeStep>();
        services.AddScoped<IRecipeStep, ResetIndexRecipeStep>();
        services.AddScoped<IRecipeStep, RebuildIndexRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the AzureADSettingsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Microsoft.Authentication.AzureAD")]
public sealed class AzureADSettingsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AzureADSettingsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the MicrosoftAccountSettingsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Microsoft.Authentication.MicrosoftAccount")]
public sealed class MicrosoftAccountSettingsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, MicrosoftAccountSettingsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the FacebookCoreSettingsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Facebook")]
public sealed class FacebookCoreSettingsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, FacebookCoreSettingsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the FacebookLoginSettingsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Facebook.Login")]
public sealed class FacebookLoginSettingsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, FacebookLoginSettingsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the GitHubAuthenticationSettingsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.GitHub.Authentication")]
public sealed class GitHubAuthenticationSettingsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, GitHubAuthenticationSettingsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the TwitterSettingsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Twitter.Signin")]
public sealed class TwitterSettingsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TwitterSettingsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the OpenIdManagementRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.OpenId.Management")]
public sealed class OpenIdManagementRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, OpenIdApplicationRecipeStep>();
        services.AddScoped<IRecipeStep, OpenIdScopeRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the OpenIdClientRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.OpenId.Client")]
public sealed class OpenIdClientRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, OpenIdClientSettingsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the OpenIdServerRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.OpenId.Server")]
public sealed class OpenIdServerRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, OpenIdServerSettingsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the OpenIdValidationRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.OpenId.Validation")]
public sealed class OpenIdValidationRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, OpenIdValidationSettingsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the ContentsSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.Contents")]
public sealed class ContentsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, CommonPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the content fields schema feature.
/// </summary>
[RequireFeatures("OrchardCore.ContentFields")]
public sealed class ContentFieldsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, BooleanFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, ContentPickerFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, DateFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, DateTimeFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, HtmlFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, LinkFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, MultiTextFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, NumericFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, TextFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, TimeFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, UserPickerFieldSchema>();
        services.AddScoped<IContentSchemaDefinition, YoutubeFieldSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the TitleSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.Title")]
public sealed class TitleSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, TitlePartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AutorouteSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.Autoroute")]
public sealed class AutorouteSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, AutoroutePartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AliasSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.Alias")]
public sealed class AliasSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, AliasPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the HtmlSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.Html")]
public sealed class HtmlSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, HtmlBodyPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the MarkdownSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.Markdown")]
public sealed class MarkdownSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, MarkdownBodyPartSchema>();
        services.AddScoped<IContentSchemaDefinition, MarkdownFieldSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the media schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Media")]
public sealed class MediaSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, MediaFieldSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the ListSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.List")]
public sealed class ListSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, ListPartSchema>();
        services.AddScoped<IContentSchemaDefinition, ContainedPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the FlowsSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.Flows")]
public sealed class FlowsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, FlowPartSchema>();
        services.AddScoped<IContentSchemaDefinition, BagPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the WidgetsSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.Widgets")]
public sealed class WidgetsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, WidgetsListPartSchema>();
        services.AddScoped<IContentSchemaDefinition, LayerMetadataSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the PreviewSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.ContentPreview")]
public sealed class PreviewSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, PreviewPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the publish later schema feature.
/// </summary>
[RequireFeatures("OrchardCore.PublishLater")]
public sealed class PublishLaterSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, PublishLaterPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the menu schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Menu")]
public sealed class MenuSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, HtmlMenuItemPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the SeoSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.Seo")]
public sealed class SeoSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, SeoMetaPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the spatial schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Spatial")]
public sealed class SpatialSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, GeoPointFieldSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AuditTrailSchema feature.
/// </summary>
[RequireFeatures("OrchardCore.AuditTrail")]
public sealed class AuditTrailSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, AuditTrailPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the taxonomy schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Taxonomies")]
public sealed class TaxonomiesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, TaxonomyFieldSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the content localization schema feature.
/// </summary>
[RequireFeatures("OrchardCore.ContentLocalization")]
public sealed class ContentLocalizationSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, LocalizationSetContentPickerFieldSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Admin settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Admin")]
public sealed class AdminSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AdminSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Azure AD settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Microsoft.Authentication.AzureAD")]
public sealed class AzureADSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AzureADSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Microsoft Account settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Microsoft.Authentication.MicrosoftAccount")]
public sealed class MicrosoftAccountSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, MicrosoftAccountSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Facebook settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Facebook")]
public sealed class FacebookSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, FacebookSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Facebook login settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Facebook.Login")]
public sealed class FacebookLoginSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, FacebookLoginSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the GitHub authentication settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.GitHub.Authentication")]
public sealed class GitHubAuthenticationSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, GitHubAuthenticationSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Twitter settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Twitter")]
public sealed class TwitterSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, TwitterSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the OpenID client settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.OpenId.Client")]
public sealed class OpenIdClientSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, OpenIdClientSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the OpenID server settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.OpenId.Server")]
public sealed class OpenIdServerSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, OpenIdServerSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the OpenID validation settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.OpenId.Validation")]
public sealed class OpenIdValidationSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, OpenIdValidationSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the audit trail settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.AuditTrail")]
public sealed class AuditTrailSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AuditTrailSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, AuditTrailTrimmingSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Azure AI Search settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.AzureAI")]
public sealed class AzureAISearchSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AzureAISearchDefaultSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the content culture picker settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.ContentLocalization.ContentCulturePicker")]
public sealed class ContentCulturePickerSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ContentCulturePickerSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, ContentRequestCultureProviderSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the content audit trail settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Contents", "OrchardCore.AuditTrail")]
public sealed class ContentAuditTrailSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ContentAuditTrailSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the export content to deployment target settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Contents.Deployment.ExportContentToDeploymentTarget")]
public sealed class ExportContentToDeploymentTargetSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ExportContentToDeploymentTargetSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the email settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Email")]
public sealed class EmailSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, EmailSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Azure email settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Email.Azure")]
public sealed class AzureEmailSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AzureEmailSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the SMTP settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Email.Smtp")]
public sealed class SmtpSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, SmtpSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Facebook Pixel settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Facebook.Pixel")]
public sealed class FacebookPixelSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, FacebookPixelSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Google Authentication settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Google.GoogleAuthentication")]
public sealed class GoogleAuthenticationSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, GoogleAuthenticationSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Google Analytics settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Google.Analytics")]
public sealed class GoogleAnalyticsSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, GoogleAnalyticsSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Google Tag Manager settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Google.TagManager")]
public sealed class GoogleTagManagerSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, GoogleTagManagerSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the HTTPS settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Https")]
public sealed class HttpsSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, HttpsSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the layers settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Layers")]
public sealed class LayersSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, LayerSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the localization settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Localization")]
public sealed class LocalizationSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, LocalizationSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the reCAPTCHA settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.ReCaptcha")]
public sealed class ReCaptchaSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ReCaptchaSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the reverse proxy settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.ReverseProxy")]
public sealed class ReverseProxySiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ReverseProxySettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the search settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Search")]
public sealed class SearchSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, SearchSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the security settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Security")]
public sealed class SecuritySiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, SecuritySettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the robots settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Seo")]
public sealed class RobotsSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, RobotsSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the sitemaps robots settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Sitemaps")]
public sealed class SitemapsRobotsSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, SitemapsRobotsSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the SMS settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Sms")]
public sealed class SmsSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, SmsSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, TwilioSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Azure SMS settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Sms.Azure")]
public sealed class AzureSmsSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AzureSmsSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the taxonomy admin list settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Taxonomies.ContentsAdminList")]
public sealed class TaxonomyContentsAdminListSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, TaxonomyContentsAdminListSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Twitter signin settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Twitter.Signin")]
public sealed class TwitterSigninSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, TwitterSigninSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the workflow trimming settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowTrimmingSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, WorkflowTrimmingSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the login settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Users")]
public sealed class LoginSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, LoginSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the external authentication settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Users.ExternalAuthentication")]
public sealed class ExternalAuthenticationSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ExternalRegistrationSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, ExternalLoginSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the change email settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Users.ChangeEmail")]
public sealed class ChangeEmailSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ChangeEmailSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the registration settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Users.Registration")]
public sealed class RegistrationSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, RegistrationSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the reset password settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Users.ResetPassword")]
public sealed class ResetPasswordSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ResetPasswordSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the two-factor authentication settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Users.2FA")]
public sealed class TwoFactorLoginSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, TwoFactorLoginSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, RoleLoginSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the authenticator app settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Users.2FA.AuthenticatorApp")]
public sealed class AuthenticatorAppSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AuthenticatorAppLoginSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the email authenticator settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Users.2FA.Email")]
public sealed class EmailAuthenticatorSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, EmailAuthenticatorLoginSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the SMS authenticator settings schema feature.
/// </summary>
[RequireFeatures("OrchardCore.Users.2FA.Sms")]
public sealed class SmsAuthenticatorSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, SmsAuthenticatorLoginSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AI site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AISiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, GeneralAISettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, DefaultAIDeploymentSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AI chat core site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Chat.Core")]
public sealed class AIChatCoreSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, DefaultOrchestratorSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AI chat admin widget site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Chat.AdminWidget")]
public sealed class AIChatAdminWidgetSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AIChatAdminWidgetSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Copilot site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Chat.Copilot")]
public sealed class CopilotSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, CopilotSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Claude site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Chat.Claude")]
public sealed class ClaudeSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ClaudeSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AI documents site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Documents")]
public sealed class AIDocumentsSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, InteractionDocumentSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AI data sources site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.DataSources")]
public sealed class AIDataSourcesSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AIDataSourceSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AI chat interactions site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Chat.Interactions")]
public sealed class AIChatInteractionsSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ChatInteractionChatModeSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, ChatInteractionMemorySettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AI memory site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Memory")]
public sealed class AIMemorySiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AIMemorySettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the DNC registry site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.DncRegistry")]
public sealed class DncRegistrySiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, DncRegistrySettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the USA FTC DNC registry site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.DncRegistry.UsaFtc")]
public sealed class UsaFtcDncRegistrySiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, UsaFtcDncRegistrySettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the Canada DNCL registry site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.DncRegistry.CanadaDncl")]
public sealed class CanadaDnclRegistrySiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, CanadaDnclRegistrySettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the display name site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Users.DisplayName")]
public sealed class DisplayNameSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, DisplayNameSettingsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the avatar site settings schema feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Users.Avatars")]
public sealed class AvatarSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, UserAvatarOptionsSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AIRecipe feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AIRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AIProfileRecipeStep>();
        services.AddScoped<IRecipeStep, CreateAIProfileFromTemplateRecipeStep>();
        services.AddScoped<IRecipeStep, AIProfileTemplateRecipeStep>();
        services.AddScoped<IRecipeStep, AIDeploymentRecipeStep>();
        services.AddScoped<IRecipeStep, DeleteAIDeploymentsRecipeStep>();
        services.AddScoped<IRecipeStep, AIProviderConnectionsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the AI data sources recipe feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.DataSources")]
public sealed class AIDataSourcesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AIDataSourceRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the MCP connection recipe feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Mcp")]
public sealed class McpConnectionsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, McpConnectionRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the MCP server recipe feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Mcp.Server")]
public sealed class McpServerRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, McpPromptRecipeStep>();
        services.AddScoped<IRecipeStep, McpResourceRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the A2A recipe feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.A2A")]
public sealed class A2ARecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, A2AConnectionRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the TimeZones recipe feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.TimeZones")]
public sealed class TimeZonesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TimeZoneMapsRecipeStep>();
    }
}

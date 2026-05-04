using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
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
        services.AddSingleton<IViewLocationExpanderProvider, DeploymentJsonViewLocationExpander>();
    }
}

/// <summary>
/// Registers services and configuration for the SettingsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class SettingsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, SettingsRecipeStep>();
        services.AddScoped<IRecipeStep, CustomSettingsRecipeStep>();
        services.AddScoped<IRecipeStep, CommandRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the FeatureRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Features", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Themes", "OrchardCore.Recipes.Core")]
public sealed class ThemesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ThemesRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the RecipesStep feature.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStepStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, RecipesRecipeStep>();
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
    }
}

/// <summary>
/// Registers services and configuration for the ContentDefinitionRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.ContentTypes", "OrchardCore.Recipes.Core")]
public sealed class ContentDefinitionRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ContentDefinitionRecipeStep>();
        services.AddScoped<IRecipeStep, ReplaceContentDefinitionRecipeStep>();
        services.AddScoped<IRecipeStep, DeleteContentDefinitionRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the ContentRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Contents", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Users", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Users.CustomUserSettings", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Media", "OrchardCore.Recipes.Core")]
public sealed class MediaRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, MediaRecipeStep>();
        services.AddScoped<IRecipeStep, MediaProfilesRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the RolesRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Roles", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Recipes.Core")]
public sealed class WorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, WorkflowTypeRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the LayersRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Layers", "OrchardCore.Recipes.Core")]
public sealed class LayersRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LayersRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the QueriesRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Queries", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Templates", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Shortcodes.Templates", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Placements", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.AdminMenu", "OrchardCore.Recipes.Core")]
public sealed class AdminMenuRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AdminMenuRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the DeploymentRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Deployment", "OrchardCore.Recipes.Core")]
public sealed class DeploymentRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, DeploymentRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the SitemapsRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.Sitemaps", "OrchardCore.Recipes.Core")]
public sealed class SitemapsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, SitemapsRecipeStep>();
    }
}

/// <summary>
/// Registers services and configuration for the UrlRewritingRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.UrlRewriting", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.DataLocalization", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Tenants.FeatureProfiles", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Lucene", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Elasticsearch", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.AzureAI", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Indexing", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Microsoft.Authentication.AzureAD", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Microsoft.Authentication.MicrosoftAccount", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Facebook", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Facebook.Login", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.GitHub.Authentication", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.Twitter.Signin", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.OpenId.Management", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.OpenId.Client", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.OpenId.Server", "OrchardCore.Recipes.Core")]
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
[RequireFeatures("OrchardCore.OpenId.Validation", "OrchardCore.Recipes.Core")]
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
        services.AddScoped<IContentDefinitionSchemaDefinition, CommonPartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, TitlePartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, AutoroutePartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, AliasPartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, HtmlBodyPartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, MarkdownBodyPartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, ListPartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, FlowPartSchema>();
        services.AddScoped<IContentDefinitionSchemaDefinition, BagPartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, WidgetsListPartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, PreviewPartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, SeoMetaPartSchema>();
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
        services.AddScoped<IContentDefinitionSchemaDefinition, AuditTrailPartSchema>();
    }
}

/// <summary>
/// Registers services and configuration for the AIRecipe feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI", "OrchardCore.Recipes.Core")]
public sealed class AIRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AIProfileRecipeStep>();
        services.AddScoped<IRecipeStep, AIProfileTemplateRecipeStep>();
        services.AddScoped<IRecipeStep, AIDeploymentRecipeStep>();
        services.AddScoped<IRecipeStep, DeleteAIDeploymentsRecipeStep>();
        services.AddScoped<IRecipeStep, AIProviderConnectionsRecipeStep>();
    }
}

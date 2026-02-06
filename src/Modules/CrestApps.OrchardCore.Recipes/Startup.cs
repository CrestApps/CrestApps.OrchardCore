using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Recipes;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RecipeExecutionService>();
        services.AddScoped<RecipeStepsService>();
    }
}

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

[RequireFeatures("OrchardCore.Features", "OrchardCore.Recipes.Core")]
public sealed class FeatureRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, FeatureRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Themes", "OrchardCore.Recipes.Core")]
public sealed class ThemesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ThemesRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStepStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, RecipesRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.ContentTypes")]
public sealed class ContentTypesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaProvider, OrchardContentSchemaProvider>();
    }
}

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

[RequireFeatures("OrchardCore.Contents", "OrchardCore.Recipes.Core")]
public sealed class ContentRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ContentRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Media", "OrchardCore.Recipes.Core")]
public sealed class MediaRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, MediaRecipeStep>();
        services.AddScoped<IRecipeStep, MediaProfilesRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Roles", "OrchardCore.Recipes.Core")]
public sealed class RolesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, RolesRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Recipes.Core")]
public sealed class WorkflowRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, WorkflowTypeRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Layers", "OrchardCore.Recipes.Core")]
public sealed class LayersRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LayersRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Queries", "OrchardCore.Recipes.Core")]
public sealed class QueriesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, QueriesRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Templates", "OrchardCore.Recipes.Core")]
public sealed class TemplatesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TemplatesRecipeStep>();
        services.AddScoped<IRecipeStep, AdminTemplatesRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Shortcodes.Templates", "OrchardCore.Recipes.Core")]
public sealed class ShortcodeTemplatesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ShortcodeTemplatesRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Placements", "OrchardCore.Recipes.Core")]
public sealed class PlacementsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, PlacementsRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.AdminMenu", "OrchardCore.Recipes.Core")]
public sealed class AdminMenuRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AdminMenuRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Deployment", "OrchardCore.Recipes.Core")]
public sealed class DeploymentRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, DeploymentRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Sitemaps", "OrchardCore.Recipes.Core")]
public sealed class SitemapsRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, SitemapsRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.UrlRewriting", "OrchardCore.Recipes.Core")]
public sealed class UrlRewritingRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, UrlRewritingRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Tenants.FeatureProfiles", "OrchardCore.Recipes.Core")]
public sealed class FeatureProfilesRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, FeatureProfilesRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Search.Lucene", "OrchardCore.Recipes.Core")]
public sealed class LuceneRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LuceneIndexRecipeStep>();
        services.AddScoped<IRecipeStep, LuceneIndexResetRecipeStep>();
        services.AddScoped<IRecipeStep, LuceneIndexRebuildRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Search.Elasticsearch", "OrchardCore.Recipes.Core")]
public sealed class ElasticRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ElasticIndexSettingsRecipeStep>();
        services.AddScoped<IRecipeStep, ElasticIndexResetRecipeStep>();
        services.AddScoped<IRecipeStep, ElasticIndexRebuildRecipeStep>();
    }
}

[RequireFeatures("OrchardCore.Search.AzureAI", "OrchardCore.Recipes.Core")]
public sealed class AzureAISearchRecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AzureAIIndexCreateRecipeStep>();
        services.AddScoped<IRecipeStep, AzureAIIndexResetRecipeStep>();
        services.AddScoped<IRecipeStep, AzureAIIndexRebuildRecipeStep>();
    }
}

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

[RequireFeatures("OrchardCore.Contents")]
public sealed class ContentsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, CommonPartSchema>();
    }
}

[RequireFeatures("OrchardCore.Title")]
public sealed class TitleSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, TitlePartSchema>();
    }
}

[RequireFeatures("OrchardCore.Autoroute")]
public sealed class AutorouteSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, AutoroutePartSchema>();
    }
}

[RequireFeatures("OrchardCore.Alias")]
public sealed class AliasSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, AliasPartSchema>();
    }
}

[RequireFeatures("OrchardCore.Html")]
public sealed class HtmlSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, HtmlBodyPartSchema>();
    }
}

[RequireFeatures("OrchardCore.Markdown")]
public sealed class MarkdownSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, MarkdownBodyPartSchema>();
    }
}

[RequireFeatures("OrchardCore.List")]
public sealed class ListSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, ListPartSchema>();
    }
}

[RequireFeatures("OrchardCore.Flows")]
public sealed class FlowsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, FlowPartSchema>();
        services.AddScoped<IContentDefinitionSchemaDefinition, BagPartSchema>();
    }
}

[RequireFeatures("OrchardCore.Widgets")]
public sealed class WidgetsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, WidgetsListPartSchema>();
    }
}

[RequireFeatures("OrchardCore.ContentPreview")]
public sealed class PreviewSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, PreviewPartSchema>();
    }
}

[RequireFeatures("OrchardCore.Seo")]
public sealed class SeoSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, SeoMetaPartSchema>();
    }
}

[RequireFeatures("OrchardCore.AuditTrail")]
public sealed class AuditTrailSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, AuditTrailPartSchema>();
    }
}

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
public sealed class RecipeCoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, SettingsRecipeStep>();
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

using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
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
    }
}

/// <summary>
/// Registers services and configuration for the ContentDefinitionRecipe feature.
/// </summary>
[RequireFeatures("OrchardCore.ContentTypes")]
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
        services.AddScoped<IRecipeStep, WorkflowTypeRecipeStep>();
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
        services.AddScoped<IRecipeStep, AdminMenuRecipeStep>();
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
        services.AddScoped<IRecipeStep, AIProfileTemplateRecipeStep>();
        services.AddScoped<IRecipeStep, AIDeploymentRecipeStep>();
        services.AddScoped<IRecipeStep, DeleteAIDeploymentsRecipeStep>();
        services.AddScoped<IRecipeStep, AIProviderConnectionsRecipeStep>();
    }
}

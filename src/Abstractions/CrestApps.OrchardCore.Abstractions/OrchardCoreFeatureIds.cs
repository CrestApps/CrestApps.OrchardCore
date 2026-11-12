namespace CrestApps.OrchardCore;

/// <summary>
/// The Orchard Core feature ids that CrestApps modules depend on or require, so a feature id is never repeated as a
/// string literal in a manifest, a <c>[Feature]</c> or a <c>[RequireFeatures]</c> attribute.
/// </summary>
public static class OrchardCoreFeatureIds
{
    public const string Admin = "OrchardCore.Admin";

    public const string AdminDashboard = "OrchardCore.AdminDashboard";

    public const string AdminMenu = "OrchardCore.AdminMenu";

    public const string Alias = "OrchardCore.Alias";

    public const string ArchiveLater = "OrchardCore.ArchiveLater";

    public const string AuditTrail = "OrchardCore.AuditTrail";

    public const string Autoroute = "OrchardCore.Autoroute";

    public const string AzureAI = "OrchardCore.AzureAI";

    public const string ContentFields = "OrchardCore.ContentFields";

    public const string ContentLocalization = "OrchardCore.ContentLocalization";

    public const string ContentLocalizationContentCulturePicker = "OrchardCore.ContentLocalization.ContentCulturePicker";

    public const string ContentManagement = "OrchardCore.ContentManagement";

    public const string ContentPreview = "OrchardCore.ContentPreview";

    public const string Contents = "OrchardCore.Contents";

    public const string ContentsDeploymentExportContentToDeploymentTarget = "OrchardCore.Contents.Deployment.ExportContentToDeploymentTarget";

    public const string ContentTypes = "OrchardCore.ContentTypes";

    public const string DataLocalization = "OrchardCore.DataLocalization";

    public const string DataPipelines = "OrchardCore.DataPipelines";

    public const string DataPipelinesCore = "OrchardCore.DataPipelines.Core";

    public const string Deployment = "OrchardCore.Deployment";

    public const string DynamicCache = "OrchardCore.DynamicCache";

    public const string Elasticsearch = "OrchardCore.Elasticsearch";

    public const string Email = "OrchardCore.Email";

    public const string EmailAzure = "OrchardCore.Email.Azure";

    public const string EmailSmtp = "OrchardCore.Email.Smtp";

    public const string Facebook = "OrchardCore.Facebook";

    public const string FacebookLogin = "OrchardCore.Facebook.Login";

    public const string FacebookPixel = "OrchardCore.Facebook.Pixel";

    public const string Features = "OrchardCore.Features";

    public const string Feeds = "OrchardCore.Feeds";

    public const string Flows = "OrchardCore.Flows";

    public const string Forms = "OrchardCore.Forms";

    public const string GitHubAuthentication = "OrchardCore.GitHub.Authentication";

    public const string GoogleAnalytics = "OrchardCore.Google.Analytics";

    public const string GoogleGoogleAuthentication = "OrchardCore.Google.GoogleAuthentication";

    public const string GoogleTagManager = "OrchardCore.Google.TagManager";

    public const string HealthChecks = "OrchardCore.HealthChecks";

    public const string Html = "OrchardCore.Html";

    public const string Https = "OrchardCore.Https";

    public const string Indexing = "OrchardCore.Indexing";

    public const string Layers = "OrchardCore.Layers";

    public const string Liquid = "OrchardCore.Liquid";

    public const string List = "OrchardCore.List";

    public const string Lists = "OrchardCore.Lists";

    public const string Localization = "OrchardCore.Localization";

    public const string Lucene = "OrchardCore.Lucene";

    public const string Markdown = "OrchardCore.Markdown";

    public const string Media = "OrchardCore.Media";

    public const string Menu = "OrchardCore.Menu";

    public const string MicrosoftAuthenticationAzureAD = "OrchardCore.Microsoft.Authentication.AzureAD";

    public const string MicrosoftAuthenticationMicrosoftAccount = "OrchardCore.Microsoft.Authentication.MicrosoftAccount";

    public const string Notifications = "OrchardCore.Notifications";

    public const string OpenIdClient = "OrchardCore.OpenId.Client";

    public const string OpenIdManagement = "OrchardCore.OpenId.Management";

    public const string OpenIdSecrets = "OrchardCore.OpenId.Secrets";

    public const string OpenIdServer = "OrchardCore.OpenId.Server";

    public const string OpenIdValidation = "OrchardCore.OpenId.Validation";

    public const string Placements = "OrchardCore.Placements";

    public const string PublishLater = "OrchardCore.PublishLater";

    public const string Queries = "OrchardCore.Queries";

    public const string QueriesSql = "OrchardCore.Queries.Sql";

    public const string RateLimits = "OrchardCore.RateLimits";

    public const string ReCaptcha = "OrchardCore.ReCaptcha";

    public const string RecipesCore = "OrchardCore.Recipes.Core";

    public const string Redis = "OrchardCore.Redis";

    public const string Resources = "OrchardCore.Resources";

    public const string ReverseProxy = "OrchardCore.ReverseProxy";

    public const string Roles = "OrchardCore.Roles";

    public const string Search = "OrchardCore.Search";

    public const string Secrets = "OrchardCore.Secrets";

    public const string Security = "OrchardCore.Security";

    public const string Seo = "OrchardCore.Seo";

    public const string ShortcodesTemplates = "OrchardCore.Shortcodes.Templates";

    public const string SignalR = "OrchardCore.SignalR";

    public const string SignalRAzure = "OrchardCore.SignalR.Azure";

    public const string SignalRRedis = "OrchardCore.SignalR.Redis";

    public const string Sitemaps = "OrchardCore.Sitemaps";

    public const string Sms = "OrchardCore.Sms";

    public const string SmsAzure = "OrchardCore.Sms.Azure";

    public const string Spatial = "OrchardCore.Spatial";

    public const string Taxonomies = "OrchardCore.Taxonomies";

    public const string TaxonomiesContentsAdminList = "OrchardCore.Taxonomies.ContentsAdminList";

    public const string Templates = "OrchardCore.Templates";

    public const string Tenants = "OrchardCore.Tenants";

    public const string TenantsFeatureProfiles = "OrchardCore.Tenants.FeatureProfiles";

    public const string Themes = "OrchardCore.Themes";

    public const string Title = "OrchardCore.Title";

    public const string Twitter = "OrchardCore.Twitter";

    public const string TwitterSignin = "OrchardCore.Twitter.Signin";

    public const string UrlRewriting = "OrchardCore.UrlRewriting";

    public const string Users = "OrchardCore.Users";

    public const string Users2FA = "OrchardCore.Users.2FA";

    public const string Users2FAAuthenticatorApp = "OrchardCore.Users.2FA.AuthenticatorApp";

    public const string Users2FAEmail = "OrchardCore.Users.2FA.Email";

    public const string Users2FASms = "OrchardCore.Users.2FA.Sms";

    public const string UsersChangeEmail = "OrchardCore.Users.ChangeEmail";

    public const string UsersCustomUserSettings = "OrchardCore.Users.CustomUserSettings";

    public const string UsersExternalAuthentication = "OrchardCore.Users.ExternalAuthentication";

    public const string UsersRegistration = "OrchardCore.Users.Registration";

    public const string UsersResetPassword = "OrchardCore.Users.ResetPassword";

    public const string Widgets = "OrchardCore.Widgets";

    public const string Workflows = "OrchardCore.Workflows";

    public const string WorkflowsHttp = "OrchardCore.Workflows.Http";

    public const string WorkflowsTimers = "OrchardCore.Workflows.Timers";
}

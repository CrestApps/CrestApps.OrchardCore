using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Services;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class BuiltInRecipeStepTests
{
    private static readonly string[] _testFeatureIds = ["OrchardCore.Contents", "OrchardCore.Media", "OrchardCore.Workflows"];
    private static readonly string[] _testThemeIds = ["TheAdmin", "TheTheme", "SafeMode"];

    private static IShellFeaturesManager CreateShellFeaturesManager()
    {
        var features = _testFeatureIds
            .Select(id =>
            {
                var featureInfo = new Mock<IFeatureInfo>();
                featureInfo.SetupGet(f => f.Id).Returns(id);

                return featureInfo.Object;
            })
            .ToArray();

        var manager = new Mock<IShellFeaturesManager>();
        manager.Setup(m => m.GetAvailableFeaturesAsync())
            .ReturnsAsync(features);

        return manager.Object;
    }

    private sealed class StubFeatureSchemaProvider : IFeatureSchemaProvider
    {
        public Task<IEnumerable<string>> GetFeatureIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<string>>(_testFeatureIds);

        public Task<IEnumerable<string>> GetThemeIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<string>>(_testThemeIds);
    }

    private static IPermissionService CreatePermissionService()
    {
        var permissions = new[]
        {
            new Permission("EditContent", "Edit content"),
            new Permission("ViewContent", "View content"),
            new Permission("ViewMediaContent", "View media content"),
        };

        var permissionService = new Mock<IPermissionService>();
        permissionService.Setup(service => service.GetPermissionsAsync())
            .Returns(new ValueTask<IEnumerable<Permission>>(permissions));
        permissionService.Setup(service => service.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((Permission)null);

        return permissionService.Object;
    }

    private static ISiteSettingsSchemaDefinition[] CreateAllSiteSettingsSchemaDefinitions()
        =>
        [
            new AdminSettingsSchema(),
            new AuditTrailSettingsSchema(),
            new AuditTrailTrimmingSettingsSchema(),
            new AuthenticatorAppLoginSettingsSchema(),
            new AzureADSettingsSchema(),
            new AzureAISearchDefaultSettingsSchema(),
            new AzureEmailSettingsSchema(),
            new AzureSmsSettingsSchema(),
            new CanadaDnclRegistrySettingsSchema(),
            new ChangeEmailSettingsSchema(),
            new ContentAuditTrailSettingsSchema(),
            new ContentCulturePickerSettingsSchema(),
            new ContentRequestCultureProviderSettingsSchema(),
            new DncRegistrySettingsSchema(),
            new EmailAuthenticatorLoginSettingsSchema(),
            new EmailSettingsSchema(),
            new ExportContentToDeploymentTargetSettingsSchema(),
            new ExternalLoginSettingsSchema(),
            new ExternalRegistrationSettingsSchema(),
            new FacebookLoginSettingsSchema(),
            new FacebookPixelSettingsSchema(),
            new FacebookSettingsSchema(),
            new GitHubAuthenticationSettingsSchema(),
            new GoogleAnalyticsSettingsSchema(),
            new GoogleAuthenticationSettingsSchema(),
            new GoogleTagManagerSettingsSchema(),
            new HttpsSettingsSchema(),
            new LayerSettingsSchema(),
            new LocalizationSettingsSchema(),
            new LoginSettingsSchema(),
            new MicrosoftAccountSettingsSchema(),
            new OpenIdClientSettingsSchema(),
            new OpenIdServerSettingsSchema(),
            new OpenIdValidationSettingsSchema(),
            new ReCaptchaSettingsSchema(),
            new RegistrationSettingsSchema(),
            new ResetPasswordSettingsSchema(),
            new ReverseProxySettingsSchema(),
            new RobotsSettingsSchema(),
            new RoleLoginSettingsSchema(),
            new SearchSettingsSchema(),
            new SecuritySettingsSchema(),
            new SitemapsRobotsSettingsSchema(),
            new SmsAuthenticatorLoginSettingsSchema(),
            new SmsSettingsSchema(),
            new SmtpSettingsSchema(),
            new TaxonomyContentsAdminListSettingsSchema(),
            new TwitterSettingsSchema(),
            new TwitterSigninSettingsSchema(),
            new TwilioSettingsSchema(),
            new TwoFactorLoginSettingsSchema(),
            new UsaFtcDncRegistrySettingsSchema(),
            new WorkflowTrimmingSettingsSchema(),
            new GeneralAISettingsSchema(),
            new DefaultAIDeploymentSettingsSchema(),
            new DefaultOrchestratorSettingsSchema(),
            new AIChatAdminWidgetSettingsSchema(),
            new CopilotSettingsSchema(),
            new ClaudeSettingsSchema(),
            new InteractionDocumentSettingsSchema(),
            new AIDataSourceSettingsSchema(),
            new ChatInteractionChatModeSettingsSchema(),
            new AIMemorySettingsSchema(),
            new ChatInteractionMemorySettingsSchema(),
            new DisplayNameSettingsSchema(),
            new UserAvatarOptionsSchema(),
        ];

    private static IRecipeStep CreateStep(Type stepType)
    {
        if (stepType == typeof(SettingsRecipeStep))
        {
            return new SettingsRecipeStep(CreateAllSiteSettingsSchemaDefinitions());
        }

        if (stepType == typeof(ContentDefinitionRecipeStep))
        {
            return new ContentDefinitionRecipeStep(CreateContentSchemaDefinitions(), CreateContentSchemaProvider());
        }

        if (stepType == typeof(FeatureRecipeStep))
        {
            return new FeatureRecipeStep(CreateShellFeaturesManager());
        }

        if (stepType == typeof(ThemesRecipeStep))
        {
            return new ThemesRecipeStep(new StubFeatureSchemaProvider());
        }

        if (stepType == typeof(ContentRecipeStep))
        {
            return new ContentRecipeStep(new ContentItemSchemaService(
                CreateContentDefinitionManager(),
                CreateContentSchemaDefinitions()));
        }

        if (stepType == typeof(AdminMenuRecipeStep))
        {
            var schemaService = new Mock<IContentItemSchemaService>();
            schemaService
                .Setup(x => x.GetGenericSchemaAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("ContentType", new JsonSchemaBuilder().Type(SchemaValueType.String))));

            return new AdminMenuRecipeStep(schemaService.Object);
        }

        if (stepType == typeof(ReplaceContentDefinitionRecipeStep))
        {
            return new ReplaceContentDefinitionRecipeStep(CreateContentSchemaDefinitions(), CreateContentSchemaProvider());
        }

        if (stepType == typeof(RecipesRecipeStep))
        {
            var recipeHarvester = new Mock<IRecipeHarvester>();
            recipeHarvester.Setup(h => h.HarvestRecipesAsync()).ReturnsAsync([]);

            return new RecipesRecipeStep([recipeHarvester.Object], CreateShellFeaturesManager());
        }

        if (stepType == typeof(RolesRecipeStep))
        {
            return new RolesRecipeStep(CreatePermissionService());
        }

        return (IRecipeStep)Activator.CreateInstance(stepType);
    }

    /// <summary>
    /// Verifies that every built-in recipe step returns the expected Name.
    /// </summary>
    [Theory]
    [InlineData(typeof(FeatureRecipeStep), "feature")]
    [InlineData(typeof(ThemesRecipeStep), "themes")]
    [InlineData(typeof(RecipesRecipeStep), "recipes")]
    [InlineData(typeof(ContentRecipeStep), "content")]
    [InlineData(typeof(MediaRecipeStep), "media")]
    [InlineData(typeof(MediaProfilesRecipeStep), "MediaProfiles")]
    [InlineData(typeof(MoveAttachedMediaFieldsRecipeStep), "move-attached-media-fields")]
    [InlineData(typeof(RolesRecipeStep), "Roles")]
    [InlineData(typeof(UsersRecipeStep), "Users")]
    [InlineData(typeof(SettingsRecipeStep), "settings")]
    [InlineData(typeof(CustomUserSettingsRecipeStep), "custom-user-settings")]
    [InlineData(typeof(CustomSettingsRecipeStep), "custom-settings")]
    [InlineData(typeof(AzureADSettingsRecipeStep), "AzureADSettings")]
    [InlineData(typeof(MicrosoftAccountSettingsRecipeStep), "MicrosoftAccountSettings")]
    [InlineData(typeof(FacebookCoreSettingsRecipeStep), "FacebookCoreSettings")]
    [InlineData(typeof(FacebookLoginSettingsRecipeStep), "FacebookLoginSettings")]
    [InlineData(typeof(GitHubAuthenticationSettingsRecipeStep), "GitHubAuthenticationSettings")]
    [InlineData(typeof(TwitterSettingsRecipeStep), "TwitterSettings")]
    [InlineData(typeof(OpenIdApplicationRecipeStep), "OpenIdApplication")]
    [InlineData(typeof(OpenIdClientSettingsRecipeStep), "OpenIdClientSettings")]
    [InlineData(typeof(OpenIdScopeRecipeStep), "OpenIdScope")]
    [InlineData(typeof(OpenIdServerSettingsRecipeStep), "OpenIdServerSettings")]
    [InlineData(typeof(OpenIdValidationSettingsRecipeStep), "OpenIdValidationSettings")]
    [InlineData(typeof(TranslationsRecipeStep), "Translations")]
    [InlineData(typeof(DynamicDataTranslationsRecipeStep), "DynamicDataTranslations")]
    [InlineData(typeof(LayersRecipeStep), "Layers")]
    [InlineData(typeof(QueriesRecipeStep), "Queries")]
    [InlineData(typeof(TemplatesRecipeStep), "Templates")]
    [InlineData(typeof(AdminTemplatesRecipeStep), "AdminTemplates")]
    [InlineData(typeof(ShortcodeTemplatesRecipeStep), "ShortcodeTemplates")]
    [InlineData(typeof(PlacementsRecipeStep), "Placements")]
    [InlineData(typeof(AdminMenuRecipeStep), "AdminMenu")]
    [InlineData(typeof(ReplaceContentDefinitionRecipeStep), "ReplaceContentDefinition")]
    [InlineData(typeof(DeleteContentDefinitionRecipeStep), "DeleteContentDefinition")]
    [InlineData(typeof(DeploymentRecipeStep), "deployment")]
    [InlineData(typeof(SitemapsRecipeStep), "Sitemaps")]
    [InlineData(typeof(UrlRewritingRecipeStep), "UrlRewriting")]
    [InlineData(typeof(FeatureProfilesRecipeStep), "FeatureProfiles")]
    [InlineData(typeof(LuceneIndexRecipeStep), "lucene-index")]
    [InlineData(typeof(LuceneIndexResetRecipeStep), "lucene-index-reset")]
    [InlineData(typeof(LuceneIndexRebuildRecipeStep), "lucene-index-rebuild")]
    [InlineData(typeof(ElasticIndexSettingsRecipeStep), "ElasticIndexSettings")]
    [InlineData(typeof(ElasticIndexResetRecipeStep), "elastic-index-reset")]
    [InlineData(typeof(ElasticIndexRebuildRecipeStep), "elastic-index-rebuild")]
    [InlineData(typeof(AzureAIIndexCreateRecipeStep), "azureai-index-create")]
    [InlineData(typeof(AzureAIIndexResetRecipeStep), "azureai-index-reset")]
    [InlineData(typeof(AzureAIIndexRebuildRecipeStep), "azureai-index-rebuild")]
    [InlineData(typeof(CreateOrUpdateIndexProfileRecipeStep), "CreateOrUpdateIndexProfile")]
    [InlineData(typeof(ResetIndexRecipeStep), "ResetIndex")]
    [InlineData(typeof(RebuildIndexRecipeStep), "RebuildIndex")]
    [InlineData(typeof(AIDataSourceRecipeStep), "AIDataSource")]
    [InlineData(typeof(McpConnectionRecipeStep), "McpConnection")]
    [InlineData(typeof(McpPromptRecipeStep), "McpPrompt")]
    [InlineData(typeof(McpResourceRecipeStep), "McpResource")]
    [InlineData(typeof(A2AConnectionRecipeStep), "A2AConnection")]
    [InlineData(typeof(CommandRecipeStep), "command")]
    public void Name_ReturnsExpected(Type stepType, string expectedName)
    {
        var step = CreateStep(stepType);
        Assert.Equal(expectedName, step.Name);
    }

    /// <summary>
    /// Verifies that every built-in recipe step produces a non-empty, serializable schema
    /// that contains the step's const name constraint.
    /// </summary>
    [Theory]
    [InlineData(typeof(FeatureRecipeStep))]
    [InlineData(typeof(ThemesRecipeStep))]
    [InlineData(typeof(RecipesRecipeStep))]
    [InlineData(typeof(ContentRecipeStep))]
    [InlineData(typeof(MediaRecipeStep))]
    [InlineData(typeof(MediaProfilesRecipeStep))]
    [InlineData(typeof(MoveAttachedMediaFieldsRecipeStep))]
    [InlineData(typeof(RolesRecipeStep))]
    [InlineData(typeof(UsersRecipeStep))]
    [InlineData(typeof(SettingsRecipeStep))]
    [InlineData(typeof(CustomUserSettingsRecipeStep))]
    [InlineData(typeof(CustomSettingsRecipeStep))]
    [InlineData(typeof(AzureADSettingsRecipeStep))]
    [InlineData(typeof(MicrosoftAccountSettingsRecipeStep))]
    [InlineData(typeof(FacebookCoreSettingsRecipeStep))]
    [InlineData(typeof(FacebookLoginSettingsRecipeStep))]
    [InlineData(typeof(GitHubAuthenticationSettingsRecipeStep))]
    [InlineData(typeof(TwitterSettingsRecipeStep))]
    [InlineData(typeof(OpenIdApplicationRecipeStep))]
    [InlineData(typeof(OpenIdClientSettingsRecipeStep))]
    [InlineData(typeof(OpenIdScopeRecipeStep))]
    [InlineData(typeof(OpenIdServerSettingsRecipeStep))]
    [InlineData(typeof(OpenIdValidationSettingsRecipeStep))]
    [InlineData(typeof(TranslationsRecipeStep))]
    [InlineData(typeof(DynamicDataTranslationsRecipeStep))]
    [InlineData(typeof(LayersRecipeStep))]
    [InlineData(typeof(QueriesRecipeStep))]
    [InlineData(typeof(TemplatesRecipeStep))]
    [InlineData(typeof(AdminTemplatesRecipeStep))]
    [InlineData(typeof(ShortcodeTemplatesRecipeStep))]
    [InlineData(typeof(PlacementsRecipeStep))]
    [InlineData(typeof(AdminMenuRecipeStep))]
    [InlineData(typeof(ReplaceContentDefinitionRecipeStep))]
    [InlineData(typeof(DeleteContentDefinitionRecipeStep))]
    [InlineData(typeof(DeploymentRecipeStep))]
    [InlineData(typeof(SitemapsRecipeStep))]
    [InlineData(typeof(UrlRewritingRecipeStep))]
    [InlineData(typeof(FeatureProfilesRecipeStep))]
    [InlineData(typeof(LuceneIndexRecipeStep))]
    [InlineData(typeof(LuceneIndexResetRecipeStep))]
    [InlineData(typeof(LuceneIndexRebuildRecipeStep))]
    [InlineData(typeof(ElasticIndexSettingsRecipeStep))]
    [InlineData(typeof(ElasticIndexResetRecipeStep))]
    [InlineData(typeof(ElasticIndexRebuildRecipeStep))]
    [InlineData(typeof(AzureAIIndexCreateRecipeStep))]
    [InlineData(typeof(AzureAIIndexResetRecipeStep))]
    [InlineData(typeof(AzureAIIndexRebuildRecipeStep))]
    [InlineData(typeof(CreateOrUpdateIndexProfileRecipeStep))]
    [InlineData(typeof(ResetIndexRecipeStep))]
    [InlineData(typeof(RebuildIndexRecipeStep))]
    [InlineData(typeof(AIDataSourceRecipeStep))]
    [InlineData(typeof(McpConnectionRecipeStep))]
    [InlineData(typeof(McpPromptRecipeStep))]
    [InlineData(typeof(McpResourceRecipeStep))]
    [InlineData(typeof(A2AConnectionRecipeStep))]
    [InlineData(typeof(CommandRecipeStep))]
    public async Task GetSchemaAsync_ProducesValidSerializableSchema(Type stepType)
    {
        var step = CreateStep(stepType);
        var schema = await step.GetSchemaAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(schema);

        var json = JsonSerializer.Serialize(schema);
        Assert.NotEmpty(json);
        Assert.StartsWith("{", json);
        Assert.Contains("\"const\"", json);
    }

    /// <summary>
    /// Verifies that every built-in recipe step caches the schema instance.
    /// </summary>
    [Theory]
    [InlineData(typeof(FeatureRecipeStep))]
    [InlineData(typeof(ThemesRecipeStep))]
    [InlineData(typeof(RecipesRecipeStep))]
    [InlineData(typeof(ContentRecipeStep))]
    [InlineData(typeof(MediaRecipeStep))]
    [InlineData(typeof(MediaProfilesRecipeStep))]
    [InlineData(typeof(MoveAttachedMediaFieldsRecipeStep))]
    [InlineData(typeof(RolesRecipeStep))]
    [InlineData(typeof(UsersRecipeStep))]
    [InlineData(typeof(SettingsRecipeStep))]
    [InlineData(typeof(CustomUserSettingsRecipeStep))]
    [InlineData(typeof(CustomSettingsRecipeStep))]
    [InlineData(typeof(AzureADSettingsRecipeStep))]
    [InlineData(typeof(MicrosoftAccountSettingsRecipeStep))]
    [InlineData(typeof(FacebookCoreSettingsRecipeStep))]
    [InlineData(typeof(FacebookLoginSettingsRecipeStep))]
    [InlineData(typeof(GitHubAuthenticationSettingsRecipeStep))]
    [InlineData(typeof(TwitterSettingsRecipeStep))]
    [InlineData(typeof(OpenIdApplicationRecipeStep))]
    [InlineData(typeof(OpenIdClientSettingsRecipeStep))]
    [InlineData(typeof(OpenIdScopeRecipeStep))]
    [InlineData(typeof(OpenIdServerSettingsRecipeStep))]
    [InlineData(typeof(OpenIdValidationSettingsRecipeStep))]
    [InlineData(typeof(TranslationsRecipeStep))]
    [InlineData(typeof(DynamicDataTranslationsRecipeStep))]
    [InlineData(typeof(LayersRecipeStep))]
    [InlineData(typeof(QueriesRecipeStep))]
    [InlineData(typeof(TemplatesRecipeStep))]
    [InlineData(typeof(AdminTemplatesRecipeStep))]
    [InlineData(typeof(ShortcodeTemplatesRecipeStep))]
    [InlineData(typeof(PlacementsRecipeStep))]
    [InlineData(typeof(AdminMenuRecipeStep))]
    [InlineData(typeof(ReplaceContentDefinitionRecipeStep))]
    [InlineData(typeof(DeleteContentDefinitionRecipeStep))]
    [InlineData(typeof(DeploymentRecipeStep))]
    [InlineData(typeof(SitemapsRecipeStep))]
    [InlineData(typeof(UrlRewritingRecipeStep))]
    [InlineData(typeof(FeatureProfilesRecipeStep))]
    [InlineData(typeof(LuceneIndexRecipeStep))]
    [InlineData(typeof(LuceneIndexResetRecipeStep))]
    [InlineData(typeof(LuceneIndexRebuildRecipeStep))]
    [InlineData(typeof(ElasticIndexSettingsRecipeStep))]
    [InlineData(typeof(ElasticIndexResetRecipeStep))]
    [InlineData(typeof(ElasticIndexRebuildRecipeStep))]
    [InlineData(typeof(AzureAIIndexCreateRecipeStep))]
    [InlineData(typeof(AzureAIIndexResetRecipeStep))]
    [InlineData(typeof(AzureAIIndexRebuildRecipeStep))]
    [InlineData(typeof(CreateOrUpdateIndexProfileRecipeStep))]
    [InlineData(typeof(ResetIndexRecipeStep))]
    [InlineData(typeof(RebuildIndexRecipeStep))]
    [InlineData(typeof(AIDataSourceRecipeStep))]
    [InlineData(typeof(McpConnectionRecipeStep))]
    [InlineData(typeof(McpPromptRecipeStep))]
    [InlineData(typeof(McpResourceRecipeStep))]
    [InlineData(typeof(A2AConnectionRecipeStep))]
    [InlineData(typeof(CommandRecipeStep))]
    public async Task GetSchemaAsync_CachesResult(Type stepType)
    {
        var step = CreateStep(stepType);
        var first = await step.GetSchemaAsync(TestContext.Current.CancellationToken);
        var second = await step.GetSchemaAsync(TestContext.Current.CancellationToken);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task FeatureRecipeStep_SchemaContainsEnableDisableAndFeatureEnums()
    {
        var step = new FeatureRecipeStep(CreateShellFeaturesManager());
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));
        Assert.Contains("\"enable\"", json);
        Assert.Contains("\"disable\"", json);
        Assert.Contains("\"OrchardCore.Contents\"", json);
        Assert.Contains("\"OrchardCore.Media\"", json);
    }

    [Fact]
    public async Task ThemesRecipeStep_SchemaContainsSiteAdminAndThemeEnums()
    {
        var step = new ThemesRecipeStep(new StubFeatureSchemaProvider());
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));
        Assert.Contains("\"site\"", json);
        Assert.Contains("\"admin\"", json);
        Assert.Contains("\"TheAdmin\"", json);
        Assert.Contains("\"TheTheme\"", json);
    }

    [Fact]
    public async Task ContentRecipeStep_SchemaRequiresDataWithContentType()
    {
        var step = new ContentRecipeStep(new ContentItemSchemaService(
            CreateContentDefinitionManager(),
            CreateContentSchemaDefinitions()));
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));
        Assert.Contains("\"ContentType\"", json);
        Assert.Contains("\"data\"", json);
    }

    [Fact]
    public async Task ContentRecipeStep_SchemaIncludesKnownPartAndFieldPropertiesPerContentType()
    {
        var step = new ContentRecipeStep(new ContentItemSchemaService(CreateContentDefinitionManager(
            CreateContentTypeDefinition(
                "BlogPost",
                CreateTypePartDefinition("TitlePart"),
                CreateTypePartDefinition("ContainedPart"),
                CreateTypePartDefinition("MarkdownBodyPart"),
                CreateTypePartDefinition("AutoroutePart"),
                CreateTypePartDefinition("BlogPost",
                    CreateFieldDefinition("Subtitle"),
                    CreateFieldDefinition("Image", "MediaField"),
                    CreateFieldDefinition("Tags", "TaxonomyField"),
                    CreateFieldDefinition("Category", "TaxonomyField"))),
            CreateContentTypeDefinition(
                "Article",
                CreateTypePartDefinition("TitlePart"),
                CreateTypePartDefinition("HtmlBodyPart"),
                CreateTypePartDefinition("AutoroutePart"),
                CreateTypePartDefinition("Article",
                    CreateFieldDefinition("Subtitle"),
                    CreateFieldDefinition("Image", "MediaField"),
                    CreateFieldDefinition("Location", "GeoPointField"),
                    CreateFieldDefinition("Categories", "TaxonomyField"),
                    CreateFieldDefinition("Summary", "MarkdownField"))),
            CreateContentTypeDefinition(
                "Widget",
                CreateTypePartDefinition("HtmlMenuItemPart"),
                CreateTypePartDefinition("LayerMetadata"),
                CreateTypePartDefinition("PublishLaterPart"))),
            CreateContentSchemaDefinitions()));

        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"BlogPost\"", json);
        Assert.Contains("\"ContainedPart\"", json);
        Assert.Contains("\"MarkdownBodyPart\"", json);
        Assert.Contains("\"Article\"", json);
        Assert.Contains("\"HtmlBodyPart\"", json);
        Assert.Contains("\"Title\"", json);
        Assert.Contains("\"Html\"", json);
        Assert.Contains("\"Path\"", json);
        Assert.Contains("\"SetHomepage\"", json);
        Assert.Contains("\"Subtitle\"", json);
        Assert.Contains("\"Image\"", json);
        Assert.Contains("\"Text\"", json);
        Assert.Contains("\"Paths\"", json);
        Assert.Contains("\"MediaTexts\"", json);
        Assert.Contains("\"Anchors\"", json);
        Assert.Contains("\"Latitude\"", json);
        Assert.Contains("\"Longitude\"", json);
        Assert.Contains("\"TermContentItemIds\"", json);
        Assert.Contains("\"TagNames\"", json);
        Assert.Contains("\"Markdown\"", json);
        Assert.Contains("\"ScheduledPublishUtc\"", json);
        Assert.Contains("\"RenderTitle\"", json);
        Assert.Contains("\"Position\"", json);
        Assert.Contains("\"Url\"", json);
        Assert.Contains("\"Target\"", json);
    }

    [Fact]
    public async Task ContentRecipeStep_SchemaValidatesContentItemsWithContentTypeSpecificProperties()
    {
        var step = new ContentRecipeStep(new ContentItemSchemaService(CreateContentDefinitionManager(
            CreateContentTypeDefinition(
                "Article",
                CreateTypePartDefinition("TitlePart"),
                CreateTypePartDefinition("HtmlBodyPart"),
                CreateTypePartDefinition("AutoroutePart"),
                CreateTypePartDefinition("Article",
                    CreateFieldDefinition("Subtitle"),
                    CreateFieldDefinition("Image", "MediaField")))),
            CreateContentSchemaDefinitions()));
        var schema = await step.GetSchemaAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse("""
            {
              "name": "content",
              "data": [
                {
                  "ContentType": "Article",
                  "DisplayText": "About",
                  "TitlePart": {
                    "Title": "About"
                  },
                  "HtmlBodyPart": {
                    "Html": "<p>About</p>"
                  },
                  "Article": {
                    "Subtitle": {
                      "Text": "This is what I do."
                    },
                    "Image": {
                      "Paths": [
                        "about-bg.jpg"
                      ],
                      "MediaTexts": [
                        "About background"
                      ],
                      "Anchors": [
                        {
                          "X": 0.5,
                          "Y": 0.5
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """);

        var result = schema.Evaluate(document.RootElement);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ContentRecipeStep_SchemaValidatesKnownPartPayloadProperties()
    {
        var step = new ContentRecipeStep(new ContentItemSchemaService(CreateContentDefinitionManager(
            CreateContentTypeDefinition(
                "BlogPost",
                CreateTypePartDefinition("TitlePart"),
                CreateTypePartDefinition("MarkdownBodyPart"),
                CreateTypePartDefinition("AutoroutePart"),
                CreateTypePartDefinition("ContainedPart"))),
            CreateContentSchemaDefinitions()));
        var schema = await step.GetSchemaAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse("""
            {
              "name": "content",
              "data": [
                {
                  "ContentType": "BlogPost",
                  "TitlePart": {
                    "Title": "Hello world"
                  },
                  "MarkdownBodyPart": {
                    "Markdown": "# Hello"
                  },
                  "AutoroutePart": {
                    "Path": "blog/hello-world",
                    "SetHomepage": false
                  },
                  "ContainedPart": {
                    "ListContentItemId": "abc",
                    "ListContentType": "Blog",
                    "Order": 0
                  }
                }
              ]
            }
            """);

        var result = schema.Evaluate(document.RootElement);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task RolesRecipeStep_SchemaContainsPermissionBehavior()
    {
        var step = new RolesRecipeStep(CreatePermissionService());
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));
        Assert.Contains("\"PermissionBehavior\"", json);
        Assert.Contains("\"Add\"", json);
        Assert.Contains("\"Replace\"", json);
        Assert.Contains("\"Remove\"", json);
        Assert.Contains("\"ViewContent\"", json);
        Assert.Contains("\"ViewMediaContent\"", json);
    }

    [Fact]
    public async Task RolesRecipeStep_SchemaValidatesPermissionItems()
    {
        var step = new RolesRecipeStep(CreatePermissionService());
        var schema = await step.GetSchemaAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse("""
            {
              "name": "Roles",
              "Roles": [
                {
                  "Name": "Anonymous",
                  "PermissionBehavior": "Remove",
                  "Permissions": [
                    "ViewContent",
                    "ViewMediaContent"
                  ]
                }
              ]
            }
            """);

        Assert.True(schema.Evaluate(document.RootElement).IsValid);
    }

    [Fact]
    public async Task MediaRecipeStep_SchemaContainsSourceOptions()
    {
        var step = new MediaRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));
        Assert.Contains("\"TargetPath\"", json);
        Assert.Contains("\"SourcePath\"", json);
        Assert.Contains("\"SourceUrl\"", json);
        Assert.Contains("\"Base64\"", json);
    }

    [Fact]
    public async Task MoveAttachedMediaFieldsRecipeStep_SchemaContainsContentTypes()
    {
        var step = new MoveAttachedMediaFieldsRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));
        Assert.Contains("\"ContentTypes\"", json);
        Assert.Contains("\"move-attached-media-fields\"", json);
    }

    [Fact]
    public async Task AdminMenuRecipeStep_SchemaContainsMenuItems()
    {
        var schemaService = new Mock<IContentItemSchemaService>();
        schemaService
            .Setup(x => x.GetGenericSchemaAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("ContentType", new JsonSchemaBuilder().Type(SchemaValueType.String))));

        var step = new AdminMenuRecipeStep(schemaService.Object);
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));
        Assert.Contains("\"MenuItems\"", json);
        Assert.Contains("\"ContentType\"", json);
    }

    [Fact]
    public async Task ContentItemSchemaService_BagPartUsesContainedContentTypesForNestedItems()
    {
        var bagSettings = new JsonObject
        {
            ["BagPartSettings"] = new JsonObject
            {
                ["ContainedContentTypes"] = new JsonArray("Slide"),
            },
        };
        var page = new ContentTypeDefinition(
            "Page",
            "Page",
            [new ContentTypePartDefinition("BagPart", new ContentPartDefinition("BagPart", [], new JsonObject()), bagSettings)],
            new JsonObject());
        page.Parts.First().ContentTypeDefinition = page;
        var slide = CreateContentTypeDefinition("Slide");
        var article = CreateContentTypeDefinition("Article");
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(page, slide, article),
            CreateContentSchemaDefinitions());

        var schema = await service.GetSchemaAsync("Page", TestContext.Current.CancellationToken);
        using var allowedDocument = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "BagPart": {
                "ContentItems": [
                  {
                    "ContentType": "Slide"
                  }
                ]
              }
            }
            """);
        using var disallowedDocument = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "BagPart": {
                "ContentItems": [
                  {
                    "ContentType": "Article"
                  }
                ]
              }
            }
            """);

        Assert.True(schema.Evaluate(allowedDocument.RootElement).IsValid);
        Assert.False(schema.Evaluate(disallowedDocument.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_BagPartUsesContainedStereotypesForNestedItems()
    {
        var bagSettings = new JsonObject
        {
            ["BagPartSettings"] = new JsonObject
            {
                ["ContainedStereotypes"] = new JsonArray("Widget"),
            },
        };
        var page = new ContentTypeDefinition(
            "Page",
            "Page",
            [new ContentTypePartDefinition("BagPart", new ContentPartDefinition("BagPart", [], new JsonObject()), bagSettings)],
            new JsonObject());
        page.Parts.First().ContentTypeDefinition = page;
        var widget = new ContentTypeDefinition(
            "HeroWidget",
            "HeroWidget",
            [],
            new JsonObject
            {
                ["ContentTypeSettings"] = new JsonObject
                {
                    ["Stereotype"] = "Widget",
                },
            });
        var article = CreateContentTypeDefinition("Article");
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(page, widget, article),
            CreateContentSchemaDefinitions());

        var schema = await service.GetSchemaAsync("Page", TestContext.Current.CancellationToken);
        using var allowedDocument = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "BagPart": {
                "ContentItems": [
                  {
                    "ContentType": "HeroWidget"
                  }
                ]
              }
            }
            """);
        using var disallowedDocument = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "BagPart": {
                "ContentItems": [
                  {
                    "ContentType": "Article"
                  }
                ]
              }
            }
            """);

        Assert.True(schema.Evaluate(allowedDocument.RootElement).IsValid);
        Assert.False(schema.Evaluate(disallowedDocument.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_NamedBagPartUsesBagPartSchemaContributors()
    {
        var bagSettings = new JsonObject
        {
            ["BagPartSettings"] = new JsonObject
            {
                ["ContainedContentTypes"] = new JsonArray("Slide"),
            },
        };
        var page = new ContentTypeDefinition(
            "Page",
            "Page",
            [new ContentTypePartDefinition("ContactMethods", new ContentPartDefinition("BagPart", [], new JsonObject()), bagSettings)],
            new JsonObject());
        page.Parts.First().ContentTypeDefinition = page;
        var slide = CreateContentTypeDefinition("Slide");
        var article = CreateContentTypeDefinition("Article");
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(page, slide, article),
            CreateContentSchemaDefinitions());

        var schema = await service.GetSchemaAsync("Page", TestContext.Current.CancellationToken);
        using var allowedDocument = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "ContactMethods": {
                "ContentItems": [
                  {
                    "ContentType": "Slide"
                  }
                ]
              }
            }
            """);
        using var disallowedDocument = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "ContactMethods": {
                "ContentItems": [
                  {
                    "ContentType": "Article"
                  }
                ]
              }
            }
            """);

        Assert.True(schema.Evaluate(allowedDocument.RootElement).IsValid);
        Assert.False(schema.Evaluate(disallowedDocument.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_FlowPartUsesContainedContentTypesForNestedItems()
    {
        var flowSettings = new JsonObject
        {
            ["FlowPartSettings"] = new JsonObject
            {
                ["ContainedContentTypes"] = new JsonArray("HeroWidget"),
            },
        };
        var landingPage = new ContentTypeDefinition(
            "LandingPage",
            "LandingPage",
            [new ContentTypePartDefinition("Body", new ContentPartDefinition("FlowPart", [], new JsonObject()), flowSettings)],
            new JsonObject());
        landingPage.Parts.First().ContentTypeDefinition = landingPage;
        var heroWidget = CreateContentTypeDefinition("HeroWidget");
        var article = CreateContentTypeDefinition("Article");
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(landingPage, heroWidget, article),
            CreateContentSchemaDefinitions());

        var schema = await service.GetSchemaAsync("LandingPage", TestContext.Current.CancellationToken);
        using var allowedDocument = JsonDocument.Parse("""
            {
              "ContentType": "LandingPage",
              "Body": {
                "Widgets": [
                  {
                    "ContentType": "HeroWidget"
                  }
                ]
              }
            }
            """);
        using var disallowedDocument = JsonDocument.Parse("""
            {
              "ContentType": "LandingPage",
              "Body": {
                "Widgets": [
                  {
                    "ContentType": "Article"
                  }
                ]
              }
            }
            """);

        Assert.True(schema.Evaluate(allowedDocument.RootElement).IsValid);
        Assert.False(schema.Evaluate(disallowedDocument.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_GetSchemaAsync_ContentTypeEnumContainsAllKnownTypes()
    {
        // Arrange
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(
                CreateContentTypeDefinition("Article"),
                CreateContentTypeDefinition("BlogPost"),
                CreateContentTypeDefinition("Page")),
            CreateContentSchemaDefinitions());

        // Act
        var schema = await service.GetSchemaAsync(TestContext.Current.CancellationToken);
        var json = JsonSerializer.Serialize(schema.Build());

        // Assert — ContentType enum should list all three content types.
        Assert.Contains("\"Article\"", json);
        Assert.Contains("\"BlogPost\"", json);
        Assert.Contains("\"Page\"", json);

        // Valid: any known content type.
        using var validArticle = JsonDocument.Parse("""{ "ContentType": "Article" }""");
        using var validBlogPost = JsonDocument.Parse("""{ "ContentType": "BlogPost" }""");
        using var validPage = JsonDocument.Parse("""{ "ContentType": "Page" }""");

        Assert.True(schema.Evaluate(validArticle.RootElement).IsValid);
        Assert.True(schema.Evaluate(validBlogPost.RootElement).IsValid);
        Assert.True(schema.Evaluate(validPage.RootElement).IsValid);

        // Invalid: unknown content type.
        using var invalidType = JsonDocument.Parse("""{ "ContentType": "UnknownType" }""");

        Assert.False(schema.Evaluate(invalidType.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_GetSchemaByContentType_ContentTypeEnumContainsOnlyRequestedType()
    {
        // Arrange
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(
                CreateContentTypeDefinition("Article"),
                CreateContentTypeDefinition("BlogPost"),
                CreateContentTypeDefinition("Page")),
            CreateContentSchemaDefinitions());

        // Act
        var schema = await service.GetSchemaAsync("Article", TestContext.Current.CancellationToken);

        // Assert — only "Article" is a valid content type.
        using var validArticle = JsonDocument.Parse("""{ "ContentType": "Article" }""");
        using var invalidBlogPost = JsonDocument.Parse("""{ "ContentType": "BlogPost" }""");
        using var invalidPage = JsonDocument.Parse("""{ "ContentType": "Page" }""");

        Assert.True(schema.Evaluate(validArticle.RootElement).IsValid);
        Assert.False(schema.Evaluate(invalidBlogPost.RootElement).IsValid);
        Assert.False(schema.Evaluate(invalidPage.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_GetSchemaByContentType_ReturnsNullForUnknownType()
    {
        // Arrange
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(
                CreateContentTypeDefinition("Article")),
            CreateContentSchemaDefinitions());

        // Act
        var schema = await service.GetSchemaAsync("DoesNotExist", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(schema);
    }

    [Fact]
    public async Task ContentItemSchemaService_GetGenericSchemaAsync_ConstrainsContentTypeEnum()
    {
        // Arrange
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(),
            CreateContentSchemaDefinitions());

        // Act
        var schema = await service.GetGenericSchemaAsync(["Article", "Page"], TestContext.Current.CancellationToken);

        // Assert
        using var validArticle = JsonDocument.Parse("""{ "ContentType": "Article" }""");
        using var validPage = JsonDocument.Parse("""{ "ContentType": "Page" }""");
        using var invalidType = JsonDocument.Parse("""{ "ContentType": "Widget" }""");

        Assert.True(schema.Evaluate(validArticle.RootElement).IsValid);
        Assert.True(schema.Evaluate(validPage.RootElement).IsValid);
        Assert.False(schema.Evaluate(invalidType.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_GetGenericSchemaAsync_AllowsAnyTypeWhenNoContentTypesSpecified()
    {
        // Arrange
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(),
            CreateContentSchemaDefinitions());

        // Act
        var schema = await service.GetGenericSchemaAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert — ContentType is just a string, no enum constraint.
        using var anyType = JsonDocument.Parse("""{ "ContentType": "AnythingGoes" }""");

        Assert.True(schema.Evaluate(anyType.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_BagPartWithEmptyContainedTypes_AllowsAnyContentType()
    {
        // Arrange — BagPart with no ContainedContentTypes or ContainedStereotypes.
        var bagSettings = new JsonObject
        {
            ["BagPartSettings"] = new JsonObject(),
        };
        var page = new ContentTypeDefinition(
            "Page",
            "Page",
            [new ContentTypePartDefinition("BagPart", new ContentPartDefinition("BagPart", [], new JsonObject()), bagSettings)],
            new JsonObject());
        page.Parts.First().ContentTypeDefinition = page;
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(page),
            CreateContentSchemaDefinitions());

        // Act
        var schema = await service.GetSchemaAsync("Page", TestContext.Current.CancellationToken);

        // Assert — nested items should accept any content type since nothing is restricted.
        using var anyDocument = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "BagPart": {
                "ContentItems": [
                  {
                    "ContentType": "AnythingGoes"
                  }
                ]
              }
            }
            """);

        Assert.True(schema.Evaluate(anyDocument.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_SelfReferencingBagPart_DoesNotCauseInfiniteRecursion()
    {
        // Arrange — Page has a BagPart that allows Page (self-referencing).
        var bagSettings = new JsonObject
        {
            ["BagPartSettings"] = new JsonObject
            {
                ["ContainedContentTypes"] = new JsonArray("Page"),
            },
        };
        var page = new ContentTypeDefinition(
            "Page",
            "Page",
            [new ContentTypePartDefinition("BagPart", new ContentPartDefinition("BagPart", [], new JsonObject()), bagSettings)],
            new JsonObject());
        page.Parts.First().ContentTypeDefinition = page;
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(page),
            CreateContentSchemaDefinitions());

        // Act — should not throw or hang due to cycle protection.
        var schema = await service.GetSchemaAsync("Page", TestContext.Current.CancellationToken);

        // Assert — schema should still validate basic structure.
        Assert.NotNull(schema);

        using var validDocument = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "BagPart": {
                "ContentItems": [
                  {
                    "ContentType": "Page"
                  }
                ]
              }
            }
            """);

        Assert.True(schema.Evaluate(validDocument.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_MutuallyReferencingTypes_HandlesCyclesGracefully()
    {
        // Arrange — TypeA contains TypeB, TypeB contains TypeA.
        var bagSettingsA = new JsonObject
        {
            ["BagPartSettings"] = new JsonObject
            {
                ["ContainedContentTypes"] = new JsonArray("TypeB"),
            },
        };
        var bagSettingsB = new JsonObject
        {
            ["BagPartSettings"] = new JsonObject
            {
                ["ContainedContentTypes"] = new JsonArray("TypeA"),
            },
        };
        var typeA = new ContentTypeDefinition(
            "TypeA",
            "TypeA",
            [new ContentTypePartDefinition("BagPart", new ContentPartDefinition("BagPart", [], new JsonObject()), bagSettingsA)],
            new JsonObject());
        typeA.Parts.First().ContentTypeDefinition = typeA;
        var typeB = new ContentTypeDefinition(
            "TypeB",
            "TypeB",
            [new ContentTypePartDefinition("BagPart", new ContentPartDefinition("BagPart", [], new JsonObject()), bagSettingsB)],
            new JsonObject());
        typeB.Parts.First().ContentTypeDefinition = typeB;
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(typeA, typeB),
            CreateContentSchemaDefinitions());

        // Act — should not throw or hang due to cycle protection.
        var schema = await service.GetSchemaAsync("TypeA", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(schema);

        using var validDocument = JsonDocument.Parse("""
            {
              "ContentType": "TypeA",
              "BagPart": {
                "ContentItems": [
                  {
                    "ContentType": "TypeB"
                  }
                ]
              }
            }
            """);

        Assert.True(schema.Evaluate(validDocument.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_BagPartWithMultipleContainedTypes_AllowsOnlyThoseTypes()
    {
        // Arrange
        var bagSettings = new JsonObject
        {
            ["BagPartSettings"] = new JsonObject
            {
                ["ContainedContentTypes"] = new JsonArray("Slide", "Testimonial"),
            },
        };
        var page = new ContentTypeDefinition(
            "Page",
            "Page",
            [new ContentTypePartDefinition("BagPart", new ContentPartDefinition("BagPart", [], new JsonObject()), bagSettings)],
            new JsonObject());
        page.Parts.First().ContentTypeDefinition = page;
        var slide = CreateContentTypeDefinition("Slide");
        var testimonial = CreateContentTypeDefinition("Testimonial");
        var article = CreateContentTypeDefinition("Article");
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(page, slide, testimonial, article),
            CreateContentSchemaDefinitions());

        // Act
        var schema = await service.GetSchemaAsync("Page", TestContext.Current.CancellationToken);

        // Assert
        using var validSlide = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "BagPart": {
                "ContentItems": [{ "ContentType": "Slide" }]
              }
            }
            """);
        using var validTestimonial = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "BagPart": {
                "ContentItems": [{ "ContentType": "Testimonial" }]
              }
            }
            """);
        using var invalidArticle = JsonDocument.Parse("""
            {
              "ContentType": "Page",
              "BagPart": {
                "ContentItems": [{ "ContentType": "Article" }]
              }
            }
            """);

        Assert.True(schema.Evaluate(validSlide.RootElement).IsValid);
        Assert.True(schema.Evaluate(validTestimonial.RootElement).IsValid);
        Assert.False(schema.Evaluate(invalidArticle.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_FlowPartWidgets_UsesWidgetsPropertyName()
    {
        // Arrange
        var flowSettings = new JsonObject
        {
            ["FlowPartSettings"] = new JsonObject
            {
                ["ContainedContentTypes"] = new JsonArray("Banner"),
            },
        };
        var landingPage = new ContentTypeDefinition(
            "LandingPage",
            "LandingPage",
            [new ContentTypePartDefinition("FlowPart", new ContentPartDefinition("FlowPart", [], new JsonObject()), flowSettings)],
            new JsonObject());
        landingPage.Parts.First().ContentTypeDefinition = landingPage;
        var banner = CreateContentTypeDefinition("Banner");
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(landingPage, banner),
            CreateContentSchemaDefinitions());

        // Act
        var schema = await service.GetSchemaAsync("LandingPage", TestContext.Current.CancellationToken);
        var json = JsonSerializer.Serialize(schema.Build());

        // Assert — FlowPart uses "Widgets" (not "ContentItems") as the nested items property.
        Assert.Contains("\"Widgets\"", json);

        using var validDocument = JsonDocument.Parse("""
            {
              "ContentType": "LandingPage",
              "FlowPart": {
                "Widgets": [
                  {
                    "ContentType": "Banner"
                  }
                ]
              }
            }
            """);

        Assert.True(schema.Evaluate(validDocument.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentItemSchemaService_RequiresContentTypeProperty()
    {
        // Arrange
        var service = new ContentItemSchemaService(
            CreateContentDefinitionManager(
                CreateContentTypeDefinition("Article")),
            CreateContentSchemaDefinitions());

        // Act
        var schema = await service.GetSchemaAsync(TestContext.Current.CancellationToken);

        // Assert — ContentType is required.
        using var missingContentType = JsonDocument.Parse("""{ "DisplayText": "Hello" }""");

        Assert.False(schema.Evaluate(missingContentType.RootElement).IsValid);
    }

    [Fact]
    public async Task DeleteContentDefinitionRecipeStep_SchemaHasStringArrays()
    {
        var step = new DeleteContentDefinitionRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));
        Assert.Contains("\"ContentTypes\"", json);
        Assert.Contains("\"ContentParts\"", json);
    }

    [Fact]
    public async Task ReplaceContentDefinitionRecipeStep_SchemaMatchesExpandedContentDefinitionShape()
    {
        var step = new ReplaceContentDefinitionRecipeStep(CreateContentSchemaDefinitions(), CreateContentSchemaProvider());
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"ContentTypePartDefinitionRecords\"", json);
        Assert.Contains("\"ContentPartFieldDefinitionRecords\"", json);
        Assert.Contains("\"ContentTypeSettings\"", json);
        Assert.Contains("\"AliasPartSettings\"", json);
        Assert.Contains("\"FieldName\"", json);
        Assert.Contains("\"TextFieldSettings\"", json);
        Assert.Contains("\"MediaFieldSettings\"", json);
        Assert.Contains("\"TaxonomyFieldSettings\"", json);
        Assert.Contains("\"GeoPointFieldSettings\"", json);
        Assert.Contains("\"MarkdownFieldSettings\"", json);
    }

    [Fact]
    public async Task ContentDefinitionRecipeStep_AllowsContentPartsWithoutContentTypes()
    {
        var step = new ContentDefinitionRecipeStep(CreateContentSchemaDefinitions(), CreateContentSchemaProvider());
        var schema = await step.GetSchemaAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse("""
            {
              "name": "ContentDefinition",
              "ContentParts": [
                {
                  "Name": "SeoPart",
                  "Settings": {},
                  "ContentPartFieldDefinitionRecords": []
                }
              ]
            }
            """);

        Assert.True(schema.Evaluate(document.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentDefinitionRecipeStep_RequiresAtLeastOneOfContentTypesOrContentParts()
    {
        var step = new ContentDefinitionRecipeStep(CreateContentSchemaDefinitions(), CreateContentSchemaProvider());
        var schema = await step.GetSchemaAsync(TestContext.Current.CancellationToken);

        using var missingBoth = JsonDocument.Parse("""
            {
              "name": "ContentDefinition"
            }
            """);

        Assert.False(schema.Evaluate(missingBoth.RootElement).IsValid);
    }

    [Fact]
    public async Task ContentDefinitionRecipeStep_SchemaCombinesPartSettingsDefinitionsWithSameName()
    {
        var step = new ContentDefinitionRecipeStep(
        [
            new TestSharedPartSchema(),
            new TestSharedPartAdvancedSchema(),
        ],
            new StubContentSchemaProvider(["SharedPart"], []));

        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"PartName\":{\"const\":\"SharedPart\"}", json);
        Assert.Contains("\"SharedPartSettings\"", json);
        Assert.Contains("\"SharedPartAdvancedSettings\"", json);
        Assert.Contains("\"Headline\"", json);
        Assert.Contains("\"Theme\"", json);
    }

    [Fact]
    public async Task ContentDefinitionRecipeStep_SchemaCombinesFieldSettingsDefinitionsWithSameName()
    {
        var step = new ContentDefinitionRecipeStep(
        [
            new TestSharedFieldSchema(),
            new TestSharedFieldAdvancedSchema(),
        ],
            new StubContentSchemaProvider([], ["SharedField"]));

        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"FieldName\":{\"const\":\"SharedField\"}", json);
        Assert.Contains("\"SharedFieldSettings\"", json);
        Assert.Contains("\"SharedFieldAdvancedSettings\"", json);
        Assert.Contains("\"Alpha\"", json);
        Assert.Contains("\"Beta\"", json);
    }

    [Fact]
    public async Task ContentRecipeStep_SchemaCombinesFieldValueDefinitionsWithSameName()
    {
        var step = new ContentRecipeStep(new ContentItemSchemaService(
            CreateContentDefinitionManager(
                CreateContentTypeDefinition(
                    "Article",
                    CreateTypePartDefinition("Article",
                        CreateFieldDefinition("Shared", "SharedField")))),
        [
            new TestSharedFieldSchema(),
            new TestSharedFieldAdvancedSchema(),
        ]));
        var schema = await step.GetSchemaAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse("""
            {
              "name": "content",
              "data": [
                {
                  "ContentType": "Article",
                  "Article": {
                    "Shared": {
                      "Alpha": "one",
                      "Beta": "two"
                    }
                  }
                }
              ]
            }
            """);

        var result = schema.Evaluate(document.RootElement);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task CustomUserSettingsRecipeStep_SchemaAllowsNamedCollectionsOfUserSettings()
    {
        var step = new CustomUserSettingsRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"userId\"", json);
        Assert.Contains("\"user-custom-user-settings\"", json);
        Assert.Contains("\"ContentType\"", json);
    }

    [Fact]
    public async Task OpenIdClientSettingsRecipeStep_SchemaContainsValidatedResponseEnums()
    {
        var step = new OpenIdClientSettingsRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"ResponseType\"", json);
        Assert.Contains("\"code id_token token\"", json);
        Assert.Contains("\"ResponseMode\"", json);
        Assert.Contains("\"form_post\"", json);
    }

    [Fact]
    public async Task OpenIdApplicationRecipeStep_SchemaContainsKnownClientAndConsentOptions()
    {
        var step = new OpenIdApplicationRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"ConsentType\"", json);
        Assert.Contains("\"explicit\"", json);
        Assert.Contains("\"Type\"", json);
        Assert.Contains("\"public\"", json);
    }

    [Fact]
    public async Task UsersRecipeStep_SchemaContainsUserShape()
    {
        var step = new UsersRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"Users\"", json);
        Assert.Contains("\"UserName\"", json);
        Assert.Contains("\"RoleNames\"", json);
    }

    [Fact]
    public async Task SettingsRecipeStep_SchemaContainsBuiltInAndContributedSettings()
    {
        var step = new SettingsRecipeStep(CreateAllSiteSettingsSchemaDefinitions());
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"HomeRoute\"", json);
        Assert.Contains("\"AdminSettings\"", json);
        Assert.Contains("\"GeneralAISettings\"", json);
        Assert.Contains("\"DisplayNameSettings\"", json);
    }

    [Fact]
    public async Task TranslationsRecipeStep_SchemaContainsNewFormatTranslationEntries()
    {
        var step = new TranslationsRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"translations\"", json);
        Assert.Contains("\"culture\"", json);
        Assert.Contains("\"key\"", json);
    }

    [Fact]
    public async Task DynamicDataTranslationsRecipeStep_SchemaContainsLegacyTranslationEntries()
    {
        var step = new DynamicDataTranslationsRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"Translations\"", json);
        Assert.Contains("\"Translation\"", json);
        Assert.Contains("\"Context\"", json);
    }

    private static IContentDefinitionManager CreateContentDefinitionManager(params ContentTypeDefinition[] definitions)
    {
        var manager = new Mock<IContentDefinitionManager>();

        manager.Setup(m => m.ListTypeDefinitionsAsync()).ReturnsAsync(definitions);

        return manager.Object;
    }

    private static ContentTypeDefinition CreateContentTypeDefinition(
        string name,
        params ContentTypePartDefinition[] parts)
    {
        var definition = new ContentTypeDefinition(name, name, parts, new JsonObject());

        foreach (var part in parts)
        {
            part.ContentTypeDefinition = definition;
        }

        return definition;
    }

    private static ContentTypePartDefinition CreateTypePartDefinition(
        string name,
        params ContentPartFieldDefinition[] fields)
    {
        var partDefinition = new ContentPartDefinition(name, fields, new JsonObject());

        return new ContentTypePartDefinition(name, partDefinition, new JsonObject());
    }

    private static ContentPartFieldDefinition CreateFieldDefinition(
        string fieldName,
        string fieldType = "TextField")
        => new(new ContentFieldDefinition(fieldType), fieldName, []);

    private static IContentSchemaDefinition[] CreateContentSchemaDefinitions()
        => typeof(IContentSchemaDefinition).Assembly.ExportedTypes
        .Where(type =>
    typeof(IContentSchemaDefinition).IsAssignableFrom(type) &&
        type is { IsAbstract: false, IsInterface: false })
        .OrderBy(type => type.Name, StringComparer.Ordinal)
        .Select(type => (IContentSchemaDefinition)Activator.CreateInstance(type))
        .ToArray();

    private static StubContentSchemaProvider CreateContentSchemaProvider()
        => new(
            CreateContentSchemaDefinitions()
            .Where(definition => definition.Type == ContentDefinitionSchemaType.Part)
            .Select(definition => definition.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray(),
        [
            "BooleanField",
            "ContentPickerField",
            "DateField",
            "DateTimeField",
            "GeoPointField",
            "HtmlField",
            "LinkField",
            "LocalizationSetContentPickerField",
            "MarkdownField",
            "MediaField",
            "MultiTextField",
            "NumericField",
            "TaxonomyField",
            "TextField",
            "TimeField",
            "UserPickerField",
            "YoutubeField",
        ]);

    private sealed class StubContentSchemaProvider(
        IReadOnlyList<string> partNames,
        IReadOnlyList<string> fieldTypeNames) : IContentSchemaProvider
    {
        public Task<IEnumerable<string>> GetPartNamesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<string>>(partNames);

        public Task<IEnumerable<string>> GetFieldTypeNamesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<string>>(fieldTypeNames);
    }

    private sealed class TestSharedPartSchema : PartSchemaDefinitionBase
    {
        public override string Name => "SharedPart";

        protected override JsonSchemaBuilder BuildSettingsCore()
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("SharedPartSettings", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(("Headline", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .AdditionalProperties(false)))
                .AdditionalProperties(true);
    }

    private sealed class TestSharedPartAdvancedSchema : PartSchemaDefinitionBase
    {
        public override string Name => "SharedPart";

        protected override JsonSchemaBuilder BuildSettingsCore()
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("SharedPartAdvancedSettings", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(("Theme", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .AdditionalProperties(false)))
                .AdditionalProperties(true);
    }

    private sealed class TestSharedFieldSchema : FieldSchemaDefinitionBase
    {
        public override string Name => "SharedField";

        protected override JsonSchemaBuilder BuildSettingsCore()
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("SharedFieldSettings", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                            ("Alpha", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .AdditionalProperties(false)))
                .AdditionalProperties(true);

        protected override JsonSchemaBuilder BuildFieldSchemaCore()
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("Alpha", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                .AdditionalProperties(true);
    }

    private sealed class TestSharedFieldAdvancedSchema : FieldSchemaDefinitionBase
    {
        public override string Name => "SharedField";

        protected override JsonSchemaBuilder BuildSettingsCore()
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("SharedFieldAdvancedSettings", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                            ("Beta", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .AdditionalProperties(false)))
                .AdditionalProperties(true);

        protected override JsonSchemaBuilder BuildFieldSchemaCore()
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("Beta", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                .AdditionalProperties(true);
    }
}

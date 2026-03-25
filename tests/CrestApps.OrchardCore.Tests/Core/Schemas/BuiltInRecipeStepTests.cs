using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
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
        public Task<IEnumerable<string>> GetFeatureIdsAsync()
            => Task.FromResult<IEnumerable<string>>(_testFeatureIds);

        public Task<IEnumerable<string>> GetThemeIdsAsync()
            => Task.FromResult<IEnumerable<string>>(_testThemeIds);
    }

    private static IPermissionService CreatePermissionService()
    {
        var permissions = new[]
        {
            new Permission("PermissionA", "Permission A"),
            new Permission("PermissionB", "Permission B")
        };

        var permissionService = new Mock<IPermissionService>();
        permissionService.Setup(service => service.GetPermissionsAsync())
            .Returns(new ValueTask<IEnumerable<Permission>>(permissions));
        permissionService.Setup(service => service.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((Permission)null);

        return permissionService.Object;
    }

    private static IRecipeStep CreateStep(Type stepType)
    {
        if (stepType == typeof(ContentDefinitionRecipeStep))
        {
            return new ContentDefinitionRecipeStep(CreateContentDefinitionSchemaDefinitions(), CreateContentSchemaProvider());
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
            return new ContentRecipeStep(CreateContentDefinitionManager());
        }

        if (stepType == typeof(ReplaceContentDefinitionRecipeStep))
        {
            return new ReplaceContentDefinitionRecipeStep(CreateContentDefinitionSchemaDefinitions(), CreateContentSchemaProvider());
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
    [InlineData(typeof(RolesRecipeStep), "Roles")]
    [InlineData(typeof(UsersRecipeStep), "Users")]
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
    [InlineData(typeof(RolesRecipeStep))]
    [InlineData(typeof(UsersRecipeStep))]
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
    [InlineData(typeof(CommandRecipeStep))]
    public async Task GetSchemaAsync_ProducesValidSerializableSchema(Type stepType)
    {
        var step = CreateStep(stepType);
        var schema = await step.GetSchemaAsync();
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
    [InlineData(typeof(RolesRecipeStep))]
    [InlineData(typeof(UsersRecipeStep))]
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
    [InlineData(typeof(CommandRecipeStep))]
    public async Task GetSchemaAsync_CachesResult(Type stepType)
    {
        var step = CreateStep(stepType);
        var first = await step.GetSchemaAsync();
        var second = await step.GetSchemaAsync();
        Assert.Same(first, second);
    }

    [Fact]
    public async Task FeatureRecipeStep_SchemaContainsEnableDisableAndFeatureEnums()
    {
        var step = new FeatureRecipeStep(CreateShellFeaturesManager());
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"enable\"", json);
        Assert.Contains("\"disable\"", json);
        Assert.Contains("\"OrchardCore.Contents\"", json);
        Assert.Contains("\"OrchardCore.Media\"", json);
    }

    [Fact]
    public async Task ThemesRecipeStep_SchemaContainsSiteAdminAndThemeEnums()
    {
        var step = new ThemesRecipeStep(new StubFeatureSchemaProvider());
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"site\"", json);
        Assert.Contains("\"admin\"", json);
        Assert.Contains("\"TheAdmin\"", json);
        Assert.Contains("\"TheTheme\"", json);
    }

    [Fact]
    public async Task ContentRecipeStep_SchemaRequiresDataWithContentType()
    {
        var step = new ContentRecipeStep(CreateContentDefinitionManager());
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"ContentType\"", json);
        Assert.Contains("\"data\"", json);
    }

    [Fact]
    public async Task RolesRecipeStep_SchemaContainsPermissionBehavior()
    {
        var step = new RolesRecipeStep(CreatePermissionService());
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"PermissionBehavior\"", json);
        Assert.Contains("\"Add\"", json);
        Assert.Contains("\"Replace\"", json);
        Assert.Contains("\"Remove\"", json);
    }

    [Fact]
    public async Task MediaRecipeStep_SchemaContainsSourceOptions()
    {
        var step = new MediaRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"TargetPath\"", json);
        Assert.Contains("\"SourcePath\"", json);
        Assert.Contains("\"SourceUrl\"", json);
        Assert.Contains("\"Base64\"", json);
    }

    [Fact]
    public async Task AdminMenuRecipeStep_SchemaContainsMenuItems()
    {
        var step = new AdminMenuRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"MenuItems\"", json);
        Assert.Contains("\"ContentType\"", json);
    }

    [Fact]
    public async Task DeleteContentDefinitionRecipeStep_SchemaHasStringArrays()
    {
        var step = new DeleteContentDefinitionRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"ContentTypes\"", json);
        Assert.Contains("\"ContentParts\"", json);
    }

    [Fact]
    public async Task ReplaceContentDefinitionRecipeStep_SchemaMatchesExpandedContentDefinitionShape()
    {
        var step = new ReplaceContentDefinitionRecipeStep(CreateContentDefinitionSchemaDefinitions(), CreateContentSchemaProvider());
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());

        Assert.Contains("\"ContentTypePartDefinitionRecords\"", json);
        Assert.Contains("\"ContentPartFieldDefinitionRecords\"", json);
        Assert.Contains("\"ContentTypeSettings\"", json);
        Assert.Contains("\"AliasPartSettings\"", json);
        Assert.Contains("\"FieldName\"", json);
    }

    [Fact]
    public async Task CustomUserSettingsRecipeStep_SchemaAllowsNamedCollectionsOfUserSettings()
    {
        var step = new CustomUserSettingsRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());

        Assert.Contains("\"userId\"", json);
        Assert.Contains("\"user-custom-user-settings\"", json);
        Assert.Contains("\"ContentType\"", json);
    }

    [Fact]
    public async Task OpenIdClientSettingsRecipeStep_SchemaContainsValidatedResponseEnums()
    {
        var step = new OpenIdClientSettingsRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());

        Assert.Contains("\"ResponseType\"", json);
        Assert.Contains("\"code id_token token\"", json);
        Assert.Contains("\"ResponseMode\"", json);
        Assert.Contains("\"form_post\"", json);
    }

    [Fact]
    public async Task OpenIdApplicationRecipeStep_SchemaContainsKnownClientAndConsentOptions()
    {
        var step = new OpenIdApplicationRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());

        Assert.Contains("\"ConsentType\"", json);
        Assert.Contains("\"explicit\"", json);
        Assert.Contains("\"Type\"", json);
        Assert.Contains("\"public\"", json);
    }

    [Fact]
    public async Task UsersRecipeStep_SchemaContainsUserShape()
    {
        var step = new UsersRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());

        Assert.Contains("\"Users\"", json);
        Assert.Contains("\"UserName\"", json);
        Assert.Contains("\"RoleNames\"", json);
    }

    [Fact]
    public async Task TranslationsRecipeStep_SchemaContainsNewFormatTranslationEntries()
    {
        var step = new TranslationsRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());

        Assert.Contains("\"translations\"", json);
        Assert.Contains("\"culture\"", json);
        Assert.Contains("\"key\"", json);
    }

    [Fact]
    public async Task DynamicDataTranslationsRecipeStep_SchemaContainsLegacyTranslationEntries()
    {
        var step = new DynamicDataTranslationsRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());

        Assert.Contains("\"Translations\"", json);
        Assert.Contains("\"Translation\"", json);
        Assert.Contains("\"Context\"", json);
    }

    private static IContentDefinitionManager CreateContentDefinitionManager()
    {
        var manager = new Mock<IContentDefinitionManager>();
        var definitions = Array.Empty<ContentTypeDefinition>();

        manager.Setup(m => m.ListTypeDefinitionsAsync()).ReturnsAsync(definitions);

        return manager.Object;
    }

    private static IContentDefinitionSchemaDefinition[] CreateContentDefinitionSchemaDefinitions()
        => typeof(IContentDefinitionSchemaDefinition).Assembly.ExportedTypes
            .Where(type =>
                typeof(IContentDefinitionSchemaDefinition).IsAssignableFrom(type) &&
                type is { IsAbstract: false, IsInterface: false })
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .Select(type => (IContentDefinitionSchemaDefinition)Activator.CreateInstance(type))
            .ToArray();

    private static StubContentSchemaProvider CreateContentSchemaProvider()
        => new StubContentSchemaProvider(
            CreateContentDefinitionSchemaDefinitions()
                .Select(definition => definition.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            [
                "BooleanField",
                "ContentPickerField",
                "DateField",
                "DateTimeField",
                "HtmlField",
                "LinkField",
                "LocalizationSetContentPickerField",
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
        public Task<IEnumerable<string>> GetPartNamesAsync()
            => Task.FromResult<IEnumerable<string>>(partNames);

        public Task<IEnumerable<string>> GetFieldTypeNamesAsync()
            => Task.FromResult<IEnumerable<string>>(fieldTypeNames);
    }
}

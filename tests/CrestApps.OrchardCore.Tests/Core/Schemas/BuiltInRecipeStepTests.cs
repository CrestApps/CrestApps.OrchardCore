using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class BuiltInRecipeStepTests
{
    /// <summary>
    /// Verifies that every built-in recipe step returns the expected Name.
    /// </summary>
    [Theory]
    [InlineData(typeof(FeatureRecipeStep), "Feature")]
    [InlineData(typeof(ThemesRecipeStep), "Themes")]
    [InlineData(typeof(RecipesRecipeStep), "Recipes")]
    [InlineData(typeof(ContentRecipeStep), "Content")]
    [InlineData(typeof(MediaRecipeStep), "Media")]
    [InlineData(typeof(MediaProfilesRecipeStep), "MediaProfiles")]
    [InlineData(typeof(RolesRecipeStep), "Roles")]
    [InlineData(typeof(CustomSettingsRecipeStep), "custom-settings")]
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
        var step = (IRecipeStep)Activator.CreateInstance(stepType);
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
    [InlineData(typeof(CustomSettingsRecipeStep))]
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
        var step = (IRecipeStep)Activator.CreateInstance(stepType);
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
    [InlineData(typeof(CustomSettingsRecipeStep))]
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
        var step = (IRecipeStep)Activator.CreateInstance(stepType);
        var first = await step.GetSchemaAsync();
        var second = await step.GetSchemaAsync();
        Assert.Same(first, second);
    }

    [Fact]
    public async Task FeatureRecipeStep_SchemaContainsEnableAndDisable()
    {
        var step = new FeatureRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"enable\"", json);
        Assert.Contains("\"disable\"", json);
    }

    [Fact]
    public async Task ThemesRecipeStep_SchemaContainsSiteAndAdmin()
    {
        var step = new ThemesRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"site\"", json);
        Assert.Contains("\"admin\"", json);
    }

    [Fact]
    public async Task ContentRecipeStep_SchemaRequiresDataWithContentType()
    {
        var step = new ContentRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"ContentType\"", json);
        Assert.Contains("\"data\"", json);
    }

    [Fact]
    public async Task RolesRecipeStep_SchemaContainsPermissionBehavior()
    {
        var step = new RolesRecipeStep();
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
        Assert.Contains("\"$type\"", json);
        Assert.Contains("\"LinkText\"", json);
    }

    [Fact]
    public async Task DeleteContentDefinitionRecipeStep_SchemaHasStringArrays()
    {
        var step = new DeleteContentDefinitionRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());
        Assert.Contains("\"ContentTypes\"", json);
        Assert.Contains("\"ContentParts\"", json);
    }
}

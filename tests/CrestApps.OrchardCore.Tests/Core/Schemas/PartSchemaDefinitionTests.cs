using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class PartSchemaDefinitionTests
{
    [Theory]
    [InlineData(typeof(CommonPartSchema), "CommonPart")]
    [InlineData(typeof(TitlePartSchema), "TitlePart")]
    [InlineData(typeof(AutoroutePartSchema), "AutoroutePart")]
    [InlineData(typeof(AliasPartSchema), "AliasPart")]
    [InlineData(typeof(HtmlBodyPartSchema), "HtmlBodyPart")]
    [InlineData(typeof(MarkdownBodyPartSchema), "MarkdownBodyPart")]
    [InlineData(typeof(ContainedPartSchema), "ContainedPart")]
    [InlineData(typeof(ListPartSchema), "ListPart")]
    [InlineData(typeof(FlowPartSchema), "FlowPart")]
    [InlineData(typeof(BagPartSchema), "BagPart")]
    [InlineData(typeof(WidgetsListPartSchema), "WidgetsListPart")]
    [InlineData(typeof(LayerMetadataSchema), "LayerMetadata")]
    [InlineData(typeof(PreviewPartSchema), "PreviewPart")]
    [InlineData(typeof(PublishLaterPartSchema), "PublishLaterPart")]
    [InlineData(typeof(SeoMetaPartSchema), "SeoMetaPart")]
    [InlineData(typeof(AuditTrailPartSchema), "AuditTrailPart")]
    [InlineData(typeof(HtmlMenuItemPartSchema), "HtmlMenuItemPart")]
    public void Name_ReturnsExpectedValue(Type definitionType, string expectedName)
    {
        var instance = (IContentSchemaDefinition)Activator.CreateInstance(definitionType);

        Assert.Equal(expectedName, instance.Name);
    }

    [Theory]
    [InlineData(typeof(CommonPartSchema))]
    [InlineData(typeof(TitlePartSchema))]
    [InlineData(typeof(AutoroutePartSchema))]
    [InlineData(typeof(AliasPartSchema))]
    [InlineData(typeof(HtmlBodyPartSchema))]
    [InlineData(typeof(MarkdownBodyPartSchema))]
    [InlineData(typeof(ContainedPartSchema))]
    [InlineData(typeof(ListPartSchema))]
    [InlineData(typeof(FlowPartSchema))]
    [InlineData(typeof(BagPartSchema))]
    [InlineData(typeof(WidgetsListPartSchema))]
    [InlineData(typeof(LayerMetadataSchema))]
    [InlineData(typeof(PreviewPartSchema))]
    [InlineData(typeof(PublishLaterPartSchema))]
    [InlineData(typeof(SeoMetaPartSchema))]
    [InlineData(typeof(AuditTrailPartSchema))]
    [InlineData(typeof(HtmlMenuItemPartSchema))]
    public void Type_AlwaysReturnsPart(Type definitionType)
    {
        var instance = (IContentSchemaDefinition)Activator.CreateInstance(definitionType);

        Assert.Equal(ContentDefinitionSchemaType.Part, instance.Type);
    }

    [Theory]
    [InlineData(typeof(CommonPartSchema))]
    [InlineData(typeof(TitlePartSchema))]
    [InlineData(typeof(AutoroutePartSchema))]
    [InlineData(typeof(AliasPartSchema))]
    [InlineData(typeof(HtmlBodyPartSchema))]
    [InlineData(typeof(MarkdownBodyPartSchema))]
    [InlineData(typeof(ContainedPartSchema))]
    [InlineData(typeof(ListPartSchema))]
    [InlineData(typeof(FlowPartSchema))]
    [InlineData(typeof(BagPartSchema))]
    [InlineData(typeof(WidgetsListPartSchema))]
    [InlineData(typeof(LayerMetadataSchema))]
    [InlineData(typeof(PreviewPartSchema))]
    [InlineData(typeof(PublishLaterPartSchema))]
    [InlineData(typeof(SeoMetaPartSchema))]
    [InlineData(typeof(AuditTrailPartSchema))]
    [InlineData(typeof(HtmlMenuItemPartSchema))]
    public async Task GetSettingsSchemaAsync_ReturnsNonNullSerializableSchema(Type definitionType)
    {
        var instance = (IContentSchemaDefinition)Activator.CreateInstance(definitionType);
        var schema = await instance.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(schema);

        var json = schema.Build().Root.Source.GetRawText();

        Assert.NotEmpty(json);
        Assert.StartsWith("{", json);
    }

    [Theory]
    [InlineData(typeof(CommonPartSchema))]
    [InlineData(typeof(TitlePartSchema))]
    [InlineData(typeof(AutoroutePartSchema))]
    [InlineData(typeof(AliasPartSchema))]
    [InlineData(typeof(HtmlBodyPartSchema))]
    [InlineData(typeof(MarkdownBodyPartSchema))]
    [InlineData(typeof(ContainedPartSchema))]
    [InlineData(typeof(ListPartSchema))]
    [InlineData(typeof(FlowPartSchema))]
    [InlineData(typeof(BagPartSchema))]
    [InlineData(typeof(WidgetsListPartSchema))]
    [InlineData(typeof(LayerMetadataSchema))]
    [InlineData(typeof(PreviewPartSchema))]
    [InlineData(typeof(PublishLaterPartSchema))]
    [InlineData(typeof(SeoMetaPartSchema))]
    [InlineData(typeof(AuditTrailPartSchema))]
    [InlineData(typeof(HtmlMenuItemPartSchema))]
    public async Task GetSettingsSchemaAsync_CachesResult(Type definitionType)
    {
        var instance = (IContentSchemaDefinition)Activator.CreateInstance(definitionType);
        var first = await instance.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var second = await instance.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task TitlePartSchema_ContainsExpectedOptions()
    {
        var def = new TitlePartSchema();
        var schema = await def.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var json = schema.Build().Root.Source.GetRawText();

        Assert.Contains("TitlePartSettings", json);
        Assert.Contains("Editable", json);
        Assert.Contains("GeneratedDisabled", json);
        Assert.Contains("GeneratedHidden", json);
        Assert.Contains("EditableRequired", json);
    }

    [Fact]
    public async Task AliasPartSchema_ContainsPatternAndOptions()
    {
        var def = new AliasPartSchema();
        var schema = await def.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var json = schema.Build().Root.Source.GetRawText();

        Assert.Contains("AliasPartSettings", json);
        Assert.Contains("Pattern", json);
        Assert.Contains("Options", json);
    }

    [Fact]
    public async Task HtmlBodyPartSchema_HasSanitizeDefault()
    {
        var def = new HtmlBodyPartSchema();
        var schema = await def.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var json = schema.Build().Root.Source.GetRawText();

        Assert.Contains("SanitizeHtml", json);
        Assert.Contains("\"default\":true", json);
        Assert.Contains("RenderLiquid", json);
        Assert.Contains("HtmlBodyPartMonacoEditorSettings", json);
        Assert.Contains("HtmlBodyPartTrumbowygEditorSettings", json);
    }

    [Fact]
    public async Task MarkdownBodyPartSchema_ContainsRenderLiquidAndMarkdownValue()
    {
        var def = new MarkdownBodyPartSchema();
        var settingsSchema = await def.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var settingsJson = settingsSchema.Build().Root.Source.GetRawText();

        Assert.Contains("RenderLiquid", settingsJson);
        Assert.Contains("MarkdownBodyPartWysiwygEditorSettings", settingsJson);

        var partSchema = await ((IContentPartSchemaDefinition)def).GetPartSchemaAsync(TestContext.Current.CancellationToken);
        var partJson = partSchema.Build().Root.Source.GetRawText();

        Assert.Contains("Markdown", partJson);
    }

    [Fact]
    public async Task HtmlMenuItemPartSchema_ContainsSettingsAndPayload()
    {
        var def = new HtmlMenuItemPartSchema();
        var settingsSchema = await def.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var settingsJson = settingsSchema.Build().Root.Source.GetRawText();

        Assert.Contains("HtmlMenuItemPartSettings", settingsJson);
        Assert.Contains("SanitizeHtml", settingsJson);

        var partSchema = await ((IContentPartSchemaDefinition)def).GetPartSchemaAsync(TestContext.Current.CancellationToken);
        var partJson = partSchema.Build().Root.Source.GetRawText();

        Assert.Contains("Url", partJson);
        Assert.Contains("Target", partJson);
        Assert.Contains("Html", partJson);
    }

    [Fact]
    public async Task PublishLaterAndLayerMetadataSchemas_ContainPayloadProperties()
    {
        var publishLaterSchema = await ((IContentPartSchemaDefinition)new PublishLaterPartSchema())
            .GetPartSchemaAsync(TestContext.Current.CancellationToken);
        var publishLaterJson = publishLaterSchema.Build().Root.Source.GetRawText();

        Assert.Contains("ScheduledPublishUtc", publishLaterJson);
        Assert.Contains("\"type\":\"null\"", publishLaterJson);

        var layerMetadataSchema = await ((IContentPartSchemaDefinition)new LayerMetadataSchema())
            .GetPartSchemaAsync(TestContext.Current.CancellationToken);
        var layerMetadataJson = layerMetadataSchema.Build().Root.Source.GetRawText();

        Assert.Contains("RenderTitle", layerMetadataJson);
        Assert.Contains("Position", layerMetadataJson);
        Assert.Contains("Zone", layerMetadataJson);
        Assert.Contains("Layer", layerMetadataJson);
    }
}

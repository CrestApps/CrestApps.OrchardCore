using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core.Schemas;

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
    [InlineData(typeof(ListPartSchema), "ListPart")]
    [InlineData(typeof(FlowPartSchema), "FlowPart")]
    [InlineData(typeof(BagPartSchema), "BagPart")]
    [InlineData(typeof(WidgetsListPartSchema), "WidgetsListPart")]
    [InlineData(typeof(PreviewPartSchema), "PreviewPart")]
    [InlineData(typeof(SeoMetaPartSchema), "SeoMetaPart")]
    [InlineData(typeof(AuditTrailPartSchema), "AuditTrailPart")]
    public void Name_ReturnsExpectedValue(Type definitionType, string expectedName)
    {
        var instance = (IContentDefinitionSchemaDefinition)Activator.CreateInstance(definitionType);

        Assert.Equal(expectedName, instance.Name);
    }

    [Theory]
    [InlineData(typeof(CommonPartSchema))]
    [InlineData(typeof(TitlePartSchema))]
    [InlineData(typeof(AutoroutePartSchema))]
    [InlineData(typeof(AliasPartSchema))]
    [InlineData(typeof(HtmlBodyPartSchema))]
    [InlineData(typeof(MarkdownBodyPartSchema))]
    [InlineData(typeof(ListPartSchema))]
    [InlineData(typeof(FlowPartSchema))]
    [InlineData(typeof(BagPartSchema))]
    [InlineData(typeof(WidgetsListPartSchema))]
    [InlineData(typeof(PreviewPartSchema))]
    [InlineData(typeof(SeoMetaPartSchema))]
    [InlineData(typeof(AuditTrailPartSchema))]
    public void Type_AlwaysReturnsPart(Type definitionType)
    {
        var instance = (IContentDefinitionSchemaDefinition)Activator.CreateInstance(definitionType);

        Assert.Equal(ContentDefinitionSchemaType.Part, instance.Type);
    }

    [Theory]
    [InlineData(typeof(CommonPartSchema))]
    [InlineData(typeof(TitlePartSchema))]
    [InlineData(typeof(AutoroutePartSchema))]
    [InlineData(typeof(AliasPartSchema))]
    [InlineData(typeof(HtmlBodyPartSchema))]
    [InlineData(typeof(MarkdownBodyPartSchema))]
    [InlineData(typeof(ListPartSchema))]
    [InlineData(typeof(FlowPartSchema))]
    [InlineData(typeof(BagPartSchema))]
    [InlineData(typeof(WidgetsListPartSchema))]
    [InlineData(typeof(PreviewPartSchema))]
    [InlineData(typeof(SeoMetaPartSchema))]
    [InlineData(typeof(AuditTrailPartSchema))]
    public async Task GetSettingsSchemaAsync_ReturnsNonNullSerializableSchema(Type definitionType)
    {
        var instance = (IContentDefinitionSchemaDefinition)Activator.CreateInstance(definitionType);
        var schema = await instance.GetSettingsSchemaAsync();

        Assert.NotNull(schema);

        var json = JsonSerializer.Serialize(schema);

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
    [InlineData(typeof(ListPartSchema))]
    [InlineData(typeof(FlowPartSchema))]
    [InlineData(typeof(BagPartSchema))]
    [InlineData(typeof(WidgetsListPartSchema))]
    [InlineData(typeof(PreviewPartSchema))]
    [InlineData(typeof(SeoMetaPartSchema))]
    [InlineData(typeof(AuditTrailPartSchema))]
    public async Task GetSettingsSchemaAsync_CachesResult(Type definitionType)
    {
        var instance = (IContentDefinitionSchemaDefinition)Activator.CreateInstance(definitionType);
        var first = await instance.GetSettingsSchemaAsync();
        var second = await instance.GetSettingsSchemaAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task TitlePartSchema_ContainsExpectedOptions()
    {
        var def = new TitlePartSchema();
        var schema = await def.GetSettingsSchemaAsync();
        var json = JsonSerializer.Serialize(schema);

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
        var schema = await def.GetSettingsSchemaAsync();
        var json = JsonSerializer.Serialize(schema);

        Assert.Contains("AliasPartSettings", json);
        Assert.Contains("Pattern", json);
        Assert.Contains("Options", json);
    }

    [Fact]
    public async Task HtmlBodyPartSchema_HasSanitizeDefault()
    {
        var def = new HtmlBodyPartSchema();
        var schema = await def.GetSettingsSchemaAsync();
        var json = JsonSerializer.Serialize(schema);

        Assert.Contains("SanitizeHtml", json);
        Assert.Contains("\"default\":true", json);
    }
}

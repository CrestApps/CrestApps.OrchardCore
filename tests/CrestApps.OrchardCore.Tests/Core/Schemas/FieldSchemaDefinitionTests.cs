using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class FieldSchemaDefinitionTests
{
    [Theory]
    [InlineData(typeof(BooleanFieldSchema), "BooleanField")]
    [InlineData(typeof(ContentPickerFieldSchema), "ContentPickerField")]
    [InlineData(typeof(DateFieldSchema), "DateField")]
    [InlineData(typeof(DateTimeFieldSchema), "DateTimeField")]
    [InlineData(typeof(GeoPointFieldSchema), "GeoPointField")]
    [InlineData(typeof(HtmlFieldSchema), "HtmlField")]
    [InlineData(typeof(LinkFieldSchema), "LinkField")]
    [InlineData(typeof(LocalizationSetContentPickerFieldSchema), "LocalizationSetContentPickerField")]
    [InlineData(typeof(MarkdownFieldSchema), "MarkdownField")]
    [InlineData(typeof(MediaFieldSchema), "MediaField")]
    [InlineData(typeof(MultiTextFieldSchema), "MultiTextField")]
    [InlineData(typeof(NumericFieldSchema), "NumericField")]
    [InlineData(typeof(TaxonomyFieldSchema), "TaxonomyField")]
    [InlineData(typeof(TextFieldSchema), "TextField")]
    [InlineData(typeof(TimeFieldSchema), "TimeField")]
    [InlineData(typeof(UserPickerFieldSchema), "UserPickerField")]
    [InlineData(typeof(YoutubeFieldSchema), "YoutubeField")]
    public void Name_ReturnsExpectedValue(Type definitionType, string expectedName)
    {
        var instance = (IContentSchemaDefinition)Activator.CreateInstance(definitionType);

        Assert.Equal(expectedName, instance.Name);
    }

    [Theory]
    [InlineData(typeof(BooleanFieldSchema))]
    [InlineData(typeof(ContentPickerFieldSchema))]
    [InlineData(typeof(DateFieldSchema))]
    [InlineData(typeof(DateTimeFieldSchema))]
    [InlineData(typeof(GeoPointFieldSchema))]
    [InlineData(typeof(HtmlFieldSchema))]
    [InlineData(typeof(LinkFieldSchema))]
    [InlineData(typeof(LocalizationSetContentPickerFieldSchema))]
    [InlineData(typeof(MarkdownFieldSchema))]
    [InlineData(typeof(MediaFieldSchema))]
    [InlineData(typeof(MultiTextFieldSchema))]
    [InlineData(typeof(NumericFieldSchema))]
    [InlineData(typeof(TaxonomyFieldSchema))]
    [InlineData(typeof(TextFieldSchema))]
    [InlineData(typeof(TimeFieldSchema))]
    [InlineData(typeof(UserPickerFieldSchema))]
    [InlineData(typeof(YoutubeFieldSchema))]
    public void Type_AlwaysReturnsField(Type definitionType)
    {
        var instance = (IContentSchemaDefinition)Activator.CreateInstance(definitionType);

        Assert.Equal(ContentDefinitionSchemaType.Field, instance.Type);
    }

    [Theory]
    [InlineData(typeof(BooleanFieldSchema))]
    [InlineData(typeof(ContentPickerFieldSchema))]
    [InlineData(typeof(DateFieldSchema))]
    [InlineData(typeof(DateTimeFieldSchema))]
    [InlineData(typeof(GeoPointFieldSchema))]
    [InlineData(typeof(HtmlFieldSchema))]
    [InlineData(typeof(LinkFieldSchema))]
    [InlineData(typeof(LocalizationSetContentPickerFieldSchema))]
    [InlineData(typeof(MarkdownFieldSchema))]
    [InlineData(typeof(MediaFieldSchema))]
    [InlineData(typeof(MultiTextFieldSchema))]
    [InlineData(typeof(NumericFieldSchema))]
    [InlineData(typeof(TaxonomyFieldSchema))]
    [InlineData(typeof(TextFieldSchema))]
    [InlineData(typeof(TimeFieldSchema))]
    [InlineData(typeof(UserPickerFieldSchema))]
    [InlineData(typeof(YoutubeFieldSchema))]
    public async Task GetSettingsSchemaAsync_ReturnsNonNullSerializableSchema(Type definitionType)
    {
        var instance = (IContentSchemaDefinition)Activator.CreateInstance(definitionType);
        var schema = await instance.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(schema);

        var json = schema.Build().Root.Source.GetRawText();

        Assert.NotEmpty(json);
        Assert.StartsWith("{", json);
    }

    [Fact]
    public async Task TextFieldSchema_ContainsExpectedSettings()
    {
        var schema = await new TextFieldSchema().GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var json = schema.Build().Root.Source.GetRawText();

        Assert.Contains("TextFieldSettings", json);
        Assert.Contains("TextFieldPredefinedListEditorSettings", json);
        Assert.Contains("TextFieldHeaderDisplaySettings", json);
        Assert.Contains("TextFieldMonacoEditorSettings", json);
        Assert.Contains("DefaultValue", json);
        Assert.Contains("Placeholder", json);
    }

    [Fact]
    public async Task NullableFieldSchemas_AllowNullValues()
    {
        var schemaJson = await GetFieldSchemaJsonAsync(new NumericFieldSchema());
        Assert.Contains("\"type\":\"null\"", schemaJson);

        schemaJson = await GetFieldSchemaJsonAsync(new DateFieldSchema());
        Assert.Contains("\"type\":\"null\"", schemaJson);

        schemaJson = await GetFieldSchemaJsonAsync(new DateTimeFieldSchema());
        Assert.Contains("\"type\":\"null\"", schemaJson);

        schemaJson = await GetFieldSchemaJsonAsync(new TimeFieldSchema());
        Assert.Contains("\"type\":\"null\"", schemaJson);

        schemaJson = await GetFieldSchemaJsonAsync(new GeoPointFieldSchema());
        Assert.Contains("\"type\":\"null\"", schemaJson);
    }

    private static async Task<string> GetFieldSchemaJsonAsync(object definition)
    {
        var interfaceType = typeof(IContentSchemaDefinition).Assembly.GetType(
            "CrestApps.OrchardCore.Recipes.Core.IContentFieldSchemaDefinition",
            throwOnError: true);
        var method = interfaceType.GetMethod("GetFieldSchemaAsync");
        var task = (ValueTask<Json.Schema.JsonSchemaBuilder>)method.Invoke(
            definition,
        [
            CreateFieldContext(((IContentSchemaDefinition)definition).Name),
            TestContext.Current.CancellationToken,
        ]);
        var schema = await task;

        return schema.Build().Root.Source.GetRawText();
    }

    [Fact]
    public async Task MediaFieldSchema_ContainsExpectedSettings()
    {
        var schema = await new MediaFieldSchema().GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var json = schema.Build().Root.Source.GetRawText();

        Assert.Contains("MediaFieldSettings", json);
        Assert.Contains("AllowMediaText", json);
        Assert.Contains("AllowAnchors", json);
        Assert.Contains("AllowedExtensions", json);
    }

    [Fact]
    public async Task HtmlFieldSchema_ContainsEditorSettings()
    {
        var schema = await new HtmlFieldSchema().GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var json = schema.Build().Root.Source.GetRawText();

        Assert.Contains("HtmlFieldMonacoEditorSettings", json);
        Assert.Contains("HtmlFieldTrumbowygEditorSettings", json);
        Assert.Contains("InsertMediaWithUrl", json);
    }

    [Fact]
    public async Task TaxonomyFieldSchema_ContainsExpectedSettings()
    {
        var schema = await new TaxonomyFieldSchema().GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var json = schema.Build().Root.Source.GetRawText();

        Assert.Contains("TaxonomyFieldSettings", json);
        Assert.Contains("TaxonomyFieldTagsEditorSettings", json);
        Assert.Contains("TaxonomyContentItemId", json);
        Assert.Contains("LeavesOnly", json);
    }

    [Fact]
    public async Task PhoneFieldSchemaDefinition_FieldSchemaContainsCountryCodeEnum()
    {
        // Arrange — resolve the internal PhoneFieldSchemaDefinition via reflection.
        var contentFieldsAssembly = typeof(CrestApps.OrchardCore.ContentFields.Fields.PhoneField).Assembly;
        var phoneFieldSchemaType = contentFieldsAssembly.GetType(
            "CrestApps.OrchardCore.ContentFields.Schemas.PhoneFieldSchemaDefinition",
            throwOnError: true);
        var instance = Activator.CreateInstance(phoneFieldSchemaType);

        // Act — get the field payload schema via the internal IContentFieldSchemaDefinition interface.
        var schemaJson = await GetFieldSchemaJsonAsync(instance);

        // Assert — CountryCode should be constrained to ISO 3166-1 alpha-2 country codes.
        Assert.Contains("\"CountryCode\"", schemaJson);
        Assert.Contains("\"US\"", schemaJson);
        Assert.Contains("\"CA\"", schemaJson);
        Assert.Contains("\"GB\"", schemaJson);
        Assert.Contains("\"DE\"", schemaJson);
        Assert.Contains("\"JP\"", schemaJson);
        Assert.Contains("\"AU\"", schemaJson);
        Assert.Contains("\"BR\"", schemaJson);
        Assert.Contains("\"ZW\"", schemaJson);
    }

    [Fact]
    public async Task PhoneFieldSchemaDefinition_SettingsSchemaContainsCountryCodeEnum()
    {
        // Arrange
        var contentFieldsAssembly = typeof(CrestApps.OrchardCore.ContentFields.Fields.PhoneField).Assembly;
        var phoneFieldSchemaType = contentFieldsAssembly.GetType(
            "CrestApps.OrchardCore.ContentFields.Schemas.PhoneFieldSchemaDefinition",
            throwOnError: true);
        var instance = (IContentSchemaDefinition)Activator.CreateInstance(phoneFieldSchemaType);

        // Act
        var schema = await instance.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var json = schema.Build().Root.Source.GetRawText();

        // Assert — SpecificCountryCode should also be constrained to country codes.
        Assert.Contains("\"SpecificCountryCode\"", json);
        Assert.Contains("\"US\"", json);
        Assert.Contains("\"InitialCountryMode\"", json);
        Assert.Contains("\"Globe\"", json);
        Assert.Contains("\"CurrentCulture\"", json);
        Assert.Contains("\"Specific\"", json);
    }

    [Fact]
    public async Task PhoneFieldSchemaDefinition_FieldSchemaContainsPhoneNumberProperties()
    {
        // Arrange
        var contentFieldsAssembly = typeof(CrestApps.OrchardCore.ContentFields.Fields.PhoneField).Assembly;
        var phoneFieldSchemaType = contentFieldsAssembly.GetType(
            "CrestApps.OrchardCore.ContentFields.Schemas.PhoneFieldSchemaDefinition",
            throwOnError: true);
        var instance = Activator.CreateInstance(phoneFieldSchemaType);

        // Act
        var schemaJson = await GetFieldSchemaJsonAsync(instance);

        // Assert
        Assert.Contains("\"PhoneNumber\"", schemaJson);
        Assert.Contains("\"CountryCode\"", schemaJson);
        Assert.Contains("\"NationalNumber\"", schemaJson);
    }

    private static ContentFieldSchemaContext CreateFieldContext(string fieldDefinitionName, string fieldName = null)
    {
        var contentTypeDefinition = new ContentTypeDefinition("TestType", "TestType", [], new JsonObject());
        var contentTypePartDefinition = new ContentTypePartDefinition(
            "TestPart",
            new ContentPartDefinition("TestPart", [], new JsonObject()),
            new JsonObject())
        {
            ContentTypeDefinition = contentTypeDefinition,
        };
        var contentPartFieldDefinition = new ContentPartFieldDefinition(
            new ContentFieldDefinition(fieldDefinitionName),
            fieldName ?? fieldDefinitionName,
            new JsonObject());

        return new ContentFieldSchemaContext
        {
            ContentPartFieldDefinition = contentPartFieldDefinition,
            ContentTypePartDefinition = contentTypePartDefinition,
        };
    }
}

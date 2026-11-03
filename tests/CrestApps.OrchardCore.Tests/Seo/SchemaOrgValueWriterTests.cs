using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo;
using CrestApps.OrchardCore.Seo.SchemaOrg;

namespace CrestApps.OrchardCore.Tests.Seo;

public sealed class SchemaOrgValueWriterTests
{
    private static readonly StructuredDataContext _context = new(null, StructuredDataPurpose.Page, "https://example.com");

    [Fact]
    public void Convert_WrapsTextInTheObjectTypeOfTheProperty()
    {
        // Arrange
        var author = SchemaOrgVocabulary.GetProperty("BlogPosting", "author");

        // Act
        var value = SchemaOrgValueWriter.Convert(JsonValue.Create("Ada"), author, _context);

        // Assert
        Assert.Equal("""{"@type":"Person","name":"Ada"}""", value.ToJsonString());
    }

    [Fact]
    public void Convert_KeepsAReferenceToAnotherNodeAsItIs()
    {
        // Arrange
        var publisher = SchemaOrgVocabulary.GetProperty("BlogPosting", "publisher");

        // Act
        var value = SchemaOrgValueWriter.Convert(JsonLd.Reference("https://example.com/#publisher"), publisher, _context);

        // Assert
        Assert.Equal("""{"@id":"https://example.com/#publisher"}""", value.ToJsonString());
    }

    [Theory]
    [InlineData("2026-05-01T19:00:00Z", "DateTime", "2026-05-01T19:00:00Z")]
    [InlineData("2026-05-01T19:00", "DateTime", "2026-05-01T19:00:00")]
    [InlineData("2026-05-01T19:00:00Z", "Date", "2026-05-01")]
    public void Convert_WritesDatesInIso8601(string text, string kind, string expected)
    {
        // Arrange
        var property = new SchemaOrgProperty { Name = "startDate", Kind = Enum.Parse<SchemaOrgValueKind>(kind) };

        // Act
        var value = SchemaOrgValueWriter.Convert(JsonValue.Create(text), property, _context);

        // Assert
        Assert.Equal(expected, value.GetValue<string>());
    }

    [Fact]
    public void Convert_WritesNumbersAsNumbers()
    {
        // Arrange
        var price = SchemaOrgVocabulary.GetProperty("Product", "offers.price");

        // Act
        var value = SchemaOrgValueWriter.Convert(JsonValue.Create("19.99"), price, _context);

        // Assert
        Assert.Equal("19.99", value.ToJsonString());
    }

    [Theory]
    [InlineData("InStock")]
    [InlineData("In stock")]
    [InlineData("https://schema.org/InStock")]
    public void Convert_FindsTheEnumerationValueATextMeans(string text)
    {
        // Arrange
        var availability = SchemaOrgVocabulary.GetProperty("Product", "offers.availability");

        // Act
        var value = SchemaOrgValueWriter.Convert(JsonValue.Create(text), availability, _context);

        // Assert
        Assert.Equal("https://schema.org/InStock", value.GetValue<string>());
    }

    [Fact]
    public void Convert_TurnsLinesIntoAListAndRelativeImagesIntoAbsoluteUrls()
    {
        // Arrange
        var ingredients = SchemaOrgVocabulary.GetProperty("Recipe", "recipeIngredient");
        var image = SchemaOrgVocabulary.GetProperty("Recipe", "image");

        // Act
        var list = SchemaOrgValueWriter.Convert(JsonValue.Create("2 eggs\n\n1 cup flour\n"), ingredients, _context);
        var images = SchemaOrgValueWriter.Convert(new JsonArray("/media/a.jpg", "/media/b.jpg"), image, _context);

        // Assert
        Assert.Equal("""["2 eggs","1 cup flour"]""", list.ToJsonString());
        Assert.Equal("""["https://example.com/media/a.jpg","https://example.com/media/b.jpg"]""", images.ToJsonString());
    }

    [Fact]
    public void Convert_ParsesTextThatIsJson()
    {
        // Arrange
        var mainEntity = SchemaOrgVocabulary.GetProperty("FAQPage", "mainEntity");

        // Act
        var value = SchemaOrgValueWriter.Convert(JsonValue.Create("""[{"@type":"Question","name":"Why?"}]"""), mainEntity, _context);

        // Assert
        Assert.IsType<JsonArray>(value);
    }

    [Fact]
    public void Convert_WhenTheValueIsEmpty_GivesNothing()
    {
        // Arrange
        var headline = SchemaOrgVocabulary.GetProperty("Article", "headline");

        // Act & Assert
        Assert.Null(SchemaOrgValueWriter.Convert(JsonValue.Create("  "), headline, _context));
        Assert.Null(SchemaOrgValueWriter.Convert(null, headline, _context));
    }

    [Fact]
    public void Set_CreatesNestedObjectsWithTheirTypes()
    {
        // Arrange
        var node = new JsonObject();

        // Act
        SchemaOrgValueWriter.Set(node, "offers.price", JsonValue.Create(10), path => SchemaOrgVocabulary.GetProperty("Product", path)?.ObjectType);
        SchemaOrgValueWriter.Set(node, "offers.priceCurrency", JsonValue.Create("USD"), path => SchemaOrgVocabulary.GetProperty("Product", path)?.ObjectType);

        // Assert
        Assert.Equal("""{"offers":{"@type":"Offer","price":10,"priceCurrency":"USD"}}""", node.ToJsonString());
    }

    [Theory]
    [InlineData("headline", true)]
    [InlineData("offers.price", true)]
    [InlineData("baseSalary.value.unitText", true)]
    [InlineData("offers.", false)]
    [InlineData("1price", false)]
    [InlineData("price tag", false)]
    [InlineData("@type", false)]
    public void IsValidPropertyName_AcceptsSchemaOrgNames(string name, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, SchemaOrgValueWriter.IsValidPropertyName(name));
    }
}

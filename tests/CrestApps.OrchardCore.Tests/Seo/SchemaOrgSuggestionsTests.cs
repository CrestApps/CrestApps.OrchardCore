using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.SchemaOrg;

namespace CrestApps.OrchardCore.Tests.Seo;

public sealed class SchemaOrgSuggestionsTests
{
    private static readonly SchemaOrgContentSource[] _blogPostSources =
    [
        new("Item:DisplayText", "Title", "Content item", SchemaOrgValueKind.Text),
        new("Item:Summary", "Summary", "Content item", SchemaOrgValueKind.LongText),
        new("Item:Author", "Author", "Content item", SchemaOrgValueKind.Object),
        new("Item:PublishedUtc", "Published date", "Content item", SchemaOrgValueKind.DateTime),
        new("Site:Publisher", "The site's publisher", "Site", SchemaOrgValueKind.Object),
        new("Field:BlogPost.Image", "Banner Image", "Fields of Blog Post", SchemaOrgValueKind.Image),
        new("Field:BlogPost.Tags", "Tags", "Fields of Blog Post", SchemaOrgValueKind.TextList),
        new("Field:BlogPost.Subtitle", "Subtitle", "Fields of Blog Post", SchemaOrgValueKind.Text),
    ];

    [Fact]
    public void SuggestMappings_MapsABlogPostLikeTheEditor()
    {
        // Act
        var mappings = SchemaOrgSuggestions.SuggestMappings("BlogPosting", _blogPostSources);

        // Assert
        Assert.Contains(mappings, mapping => mapping.Name == "headline" && mapping.Value == "Item:DisplayText");
        Assert.Contains(mappings, mapping => mapping.Name == "author" && mapping.Value == "Item:Author");
        Assert.Contains(mappings, mapping => mapping.Name == "image" && mapping.Value == "Field:BlogPost.Image");
        Assert.Contains(mappings, mapping => mapping.Name == "keywords" && mapping.Value == "Field:BlogPost.Tags");
        Assert.DoesNotContain(mappings, mapping => mapping.Name == "thumbnailUrl");
        Assert.DoesNotContain(mappings, mapping => mapping.Name is "name" or "description" or "datePublished");
    }

    [Fact]
    public void SuggestMappings_LeavesToEditorsWhatTheContentTypeCannotGive()
    {
        // Act
        var mappings = SchemaOrgSuggestions.SuggestMappings("Event", _blogPostSources);

        // Assert
        Assert.Contains(mappings, mapping => mapping.Name == "startDate" && mapping.Source == SchemaOrgValueSource.Editor);
        Assert.Contains(mappings, mapping => mapping.Name == "location.address" && mapping.Source == SchemaOrgValueSource.Editor);
        Assert.Contains(mappings, mapping => mapping.Name == "organizer" && mapping.Value == "Site:Publisher");
        Assert.DoesNotContain(mappings, mapping => mapping.Name is "location" or "offers");
    }

    [Fact]
    public void SuggestMappings_SkipsWhatIsMapped()
    {
        // Arrange
        var existing = new[] { new SchemaOrgPropertyMapping { Name = "headline", Source = SchemaOrgValueSource.Content, Value = "Field:BlogPost.Subtitle" } };

        // Act
        var mappings = SchemaOrgSuggestions.SuggestMappings("BlogPosting", _blogPostSources, existing);

        // Assert
        Assert.DoesNotContain(mappings, mapping => mapping.Name == "headline");
    }

    [Fact]
    public void Suggest_NeverPicksAFieldOfTheWrongKind()
    {
        // Arrange
        var price = SchemaOrgVocabulary.GetProperty("Product", "offers.price");
        var sources = new[] { new SchemaOrgContentSource("Field:Product.Price", "Price", "Fields", SchemaOrgValueKind.Image) };

        // Act
        var mapping = SchemaOrgSuggestions.Suggest(price, sources, out var score);

        // Assert
        Assert.Equal(0, score);
        Assert.Equal(SchemaOrgValueSource.Editor, mapping.Source);
    }

    [Fact]
    public void MissingRequired_ListsTheRequiredPropertiesNoMappingGives()
    {
        // Act
        var missing = SchemaOrgSuggestions.MissingRequired("Event", [new SchemaOrgPropertyMapping { Name = "location.address", Source = SchemaOrgValueSource.Editor }]);

        // Assert
        Assert.Equal(["startDate"], missing);
    }
}

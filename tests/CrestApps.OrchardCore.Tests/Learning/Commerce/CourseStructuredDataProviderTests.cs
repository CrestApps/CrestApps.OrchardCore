using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.StructuredData;
using CrestApps.OrchardCore.Seo;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Commerce;

/// <summary>
/// A course is described as a schema.org <c>Course</c>: it overrides the <c>Product</c> type a store product gave it,
/// keeps the offer, and is provided by the site's publisher.
/// </summary>
public sealed class CourseStructuredDataProviderTests
{
    [Fact]
    public async Task ACourse_IsACourse_WithItsCodeLevelAndDuration_ProvidedByThePublisher()
    {
        // Arrange
        var item = new ContentItem { ContentItemId = "c1", ContentType = "Course", DisplayText = "Safety" };

        item.Apply(new CoursePart { Code = "SAF-101", Summary = "Work safely.", Level = "Beginner", EstimatedMinutes = 90 });

        var context = new StructuredDataContext(item, StructuredDataPurpose.Page, "https://example.com")
        {
            MainEntity = new JsonObject
            {
                ["@type"] = "Product",
                ["offers"] = new JsonObject { ["@type"] = "Offer", ["price"] = "49.00" },
                ["publisher"] = new JsonObject { ["@id"] = "https://example.com/#publisher" },
            },
        };

        // Act
        await new CourseStructuredDataProvider().BuildAsync(context);

        // Assert
        var entity = context.MainEntity;

        Assert.Equal("Course", entity["@type"]?.GetValue<string>());
        Assert.Equal("SAF-101", entity["courseCode"]?.GetValue<string>());
        Assert.Equal("Work safely.", entity["description"]?.GetValue<string>());
        Assert.Equal("Beginner", entity["educationalLevel"]?.GetValue<string>());
        Assert.Equal("PT90M", entity["timeRequired"]?.GetValue<string>());
        Assert.Equal("https://example.com/#publisher", entity["provider"]?["@id"]?.GetValue<string>());
        Assert.NotNull(entity["offers"]);
    }
}

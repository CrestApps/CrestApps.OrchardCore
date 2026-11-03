using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo.Drivers;
using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.SchemaOrg;

namespace CrestApps.OrchardCore.Tests.Seo;

public sealed class SchemaOrgVocabularyTests
{
    [Fact]
    public void GetProperties_IncludesTheParentsPropertiesOnce_TheTypesOwnWinning()
    {
        // Act
        var properties = SchemaOrgVocabulary.GetProperties("Product");

        // Assert
        Assert.Single(properties, property => property.Name == "name");
        Assert.Equal(SchemaOrgPropertyLevel.Required, properties.Single(property => property.Name == "name").Level);
        Assert.Contains(properties, property => property.Name == "sameAs");
    }

    [Fact]
    public void GetProperties_ListsTheRequiredAndRecommendedPropertiesFirst()
    {
        // Act
        var levels = SchemaOrgVocabulary.GetProperties("Event").Select(property => property.Level).ToList();

        // Assert
        Assert.Equal(levels.OrderByDescending(level => level), levels);
    }

    [Fact]
    public void GetProperties_GivesATypeTheVocabularyDoesNotListTheGenericProperties()
    {
        // Act
        var properties = SchemaOrgVocabulary.GetProperties("MusicEvent");

        // Assert
        Assert.Equal(SchemaOrgVocabulary.GetProperties("Thing").Select(property => property.Name), properties.Select(property => property.Name));
    }

    [Fact]
    public void Types_HaveUniqueNamesParentsThatExistAndValidProperties()
    {
        // Arrange
        var names = SchemaOrgVocabulary.Types.Select(type => type.Name).ToList();

        // Assert
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var type in SchemaOrgVocabulary.Types)
        {
            Assert.True(type.Parent is null || SchemaOrgVocabulary.GetType(type.Parent) is not null, type.Name);
            Assert.All(type.Properties, property => Assert.True(SchemaOrgValueWriter.IsValidPropertyName(property.Name), property.Name));
            Assert.All(type.Properties.Where(property => property.Kind == SchemaOrgValueKind.Object), property => Assert.False(string.IsNullOrEmpty(property.ObjectType), property.Name));
            Assert.All(type.Properties.Where(property => property.Kind == SchemaOrgValueKind.Enumeration), property => Assert.NotEmpty(property.Options));
        }
    }

    [Fact]
    public void ReadProperties_KeepsNamedRowsAndDropsTheValuesEditorsEnter()
    {
        // Arrange
        var json = new JsonArray
        {
            new JsonObject { ["name"] = " headline ", ["source"] = "Content", ["value"] = "Item:DisplayText" },
            new JsonObject { ["name"] = "eventStatus", ["source"] = "Editor", ["value"] = "left over" },
            new JsonObject { ["name"] = "", ["source"] = "Fixed", ["value"] = "x" },
        }.ToJsonString();

        // Act
        var properties = SchemaOrgPartSettingsDisplayDriver.ReadProperties(json);

        // Assert
        Assert.Collection(
            properties,
            property =>
            {
                Assert.Equal("headline", property.Name);
                Assert.Equal(SchemaOrgValueSource.Content, property.Source);
                Assert.Equal("Item:DisplayText", property.Value);
            },
            property =>
            {
                Assert.Equal("eventStatus", property.Name);
                Assert.Equal(SchemaOrgValueSource.Editor, property.Source);
                Assert.Null(property.Value);
            });
    }

    [Fact]
    public void ReadProperties_WhenTheJsonIsBroken_GivesNull()
    {
        // Act & Assert
        Assert.Null(SchemaOrgPartSettingsDisplayDriver.ReadProperties("[{"));
    }

    [Fact]
    public void BuildFields_ListsThePropertiesLeftToEditorsWithTheirInputs()
    {
        // Arrange
        var settings = new SchemaOrgPartSettings
        {
            SchemaType = "Event",
            Properties =
            [
                new() { Name = "headline", Source = SchemaOrgValueSource.Content, Value = "Item:DisplayText" },
                new() { Name = "startDate", Source = SchemaOrgValueSource.Editor },
                new() { Name = "eventStatus", Source = SchemaOrgValueSource.Editor },
            ],
        };

        // Act
        var fields = SchemaOrgPartDisplayDriver.BuildFields(settings);

        // Assert
        Assert.Collection(
            fields,
            field =>
            {
                Assert.Equal("Start date", field.Label);
                Assert.Equal(SchemaOrgValueKind.DateTime, field.Kind);
                Assert.Equal(SchemaOrgPropertyLevel.Required, field.Level);
            },
            field =>
            {
                Assert.Equal(SchemaOrgValueKind.Enumeration, field.Kind);
                Assert.NotEmpty(field.Options);
            });
    }

    [Theory]
    [InlineData("startDate", "Start date")]
    [InlineData("offers.priceCurrency", "Offers › Price currency")]
    [InlineData("sku", "Sku")]
    public void Humanize_TurnsPropertyNamesIntoLabels(string name, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, SchemaOrgPartDisplayDriver.Humanize(name));
    }
}

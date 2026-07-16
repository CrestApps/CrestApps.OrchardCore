using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterDataGovernanceCatalogTests
{
    [Fact]
    public void Categories_HaveUniqueKeys()
    {
        // Arrange
        var categories = ContactCenterDataGovernanceCatalog.Categories;

        // Act
        var distinctKeys = categories.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count();

        // Assert
        Assert.Equal(categories.Count, distinctKeys);
    }

    [Fact]
    public void Categories_AllHaveRequiredText()
    {
        // Arrange
        var categories = ContactCenterDataGovernanceCatalog.Categories;

        // Assert
        Assert.All(categories, category =>
        {
            Assert.False(string.IsNullOrWhiteSpace(category.Key));
            Assert.False(string.IsNullOrWhiteSpace(category.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(category.RetentionBasis));
            Assert.False(string.IsNullOrWhiteSpace(category.Description));
        });
    }

    [Fact]
    public void ContainsPersonalData_MatchesSensitivity()
    {
        // Assert
        Assert.All(ContactCenterDataGovernanceCatalog.Categories, category =>
        {
            var expected = category.Sensitivity != ContactCenterDataSensitivity.NonPersonal;

            Assert.Equal(expected, category.ContainsPersonalData);
        });
    }

    [Fact]
    public void PersonalCategories_DefineAConcreteErasureStrategy()
    {
        // Arrange
        var personal = ContactCenterDataGovernanceCatalog.Categories
            .Where(c => c.ContainsPersonalData);

        // Assert
        Assert.All(personal, category =>
            Assert.NotEqual(ContactCenterErasureStrategy.NotApplicable, category.ErasureStrategy));
    }

    [Fact]
    public void NonPersonalCategories_DoNotAnonymize()
    {
        // Arrange
        var nonPersonal = ContactCenterDataGovernanceCatalog.Categories
            .Where(c => !c.ContainsPersonalData);

        // Assert
        Assert.All(nonPersonal, category =>
            Assert.NotEqual(ContactCenterErasureStrategy.Anonymize, category.ErasureStrategy));
    }

    [Fact]
    public void RecordingReferenceCategories_AreAlwaysPersonal()
    {
        // Arrange
        var recordingBearing = ContactCenterDataGovernanceCatalog.Categories
            .Where(c => c.ContainsRecordingReference);

        // Assert
        Assert.NotEmpty(recordingBearing);
        Assert.All(recordingBearing, category => Assert.True(category.ContainsPersonalData));
    }

    [Theory]
    [InlineData("interaction-event")]
    [InlineData("interaction")]
    [InlineData("call-session")]
    public void TryGet_ReturnsKnownCategory(string key)
    {
        // Act
        var found = ContactCenterDataGovernanceCatalog.TryGet(key, out var category);

        // Assert
        Assert.True(found);
        Assert.Equal(key, category.Key);
    }

    [Fact]
    public void TryGet_ReturnsFalseForUnknownKey()
    {
        // Act
        var found = ContactCenterDataGovernanceCatalog.TryGet("does-not-exist", out var category);

        // Assert
        Assert.False(found);
        Assert.Null(category);
    }
}

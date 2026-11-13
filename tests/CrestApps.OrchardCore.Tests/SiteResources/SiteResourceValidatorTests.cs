using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Services;
using CrestApps.OrchardCore.Tests.AgenticCommerce;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Tests.SiteResources;

public sealed class SiteResourceValidatorTests
{
    [Fact]
    public void Validate_AWellFormedResource_HasNoError()
    {
        // Act
        var errors = CreateValidator().Validate(Resource("brand"), []);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("-starts-with-dash")]
    [InlineData("slash/name")]
    public void Validate_AnInvalidName_IsRejected(string name)
    {
        // Act
        var errors = CreateValidator().Validate(Resource(name), []);

        // Assert
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(SiteResource.Name)));
    }

    [Fact]
    public void Validate_ANameAnotherResourceUses_IsRejectedWhateverItsCase()
    {
        // Act
        var errors = CreateValidator().Validate(Resource("brand", "1"), [("2", "BRAND")]);

        // Assert
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(SiteResource.Name)));
    }

    [Fact]
    public void Validate_TheResourcesOwnName_IsAccepted()
    {
        // Act
        var errors = CreateValidator().Validate(Resource("brand", "1"), [("1", "brand")]);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ANameAModuleDefines_IsRejected()
    {
        // Arrange
        var manifest = new ResourceManifest();
        manifest.DefineStyle("bootstrap").SetUrl("/bootstrap.css");

        // Act
        var errors = CreateValidator(manifest).Validate(Resource("Bootstrap"), []);

        // Assert
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(SiteResource.Name)));
    }

    [Fact]
    public void Validate_NoContentOrADependencyOnItself_IsRejected()
    {
        // Arrange
        var resource = Resource("brand");
        resource.Content = " ";
        resource.Dependencies = ["Brand"];

        // Act
        var errors = CreateValidator().Validate(resource, []);

        // Assert
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(SiteResource.Content)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(SiteResource.Dependencies)));
    }

    private static SiteResource Resource(string name, string itemId = "1")
        => new()
        {
            ItemId = itemId,
            Name = name,
            Content = "body { margin: 0; }",
        };

    private static SiteResourceValidator CreateValidator(params ResourceManifest[] manifests)
    {
        var options = new ResourceManagementOptions();

        foreach (var manifest in manifests)
        {
            options.ResourceManifests.Add(manifest);
        }

        return new SiteResourceValidator(
            new InMemoryDocumentManager<DictionaryDocument<SiteResource>>(),
            Options.Create(options),
            new PassThroughStringLocalizer<SiteResourceValidator>());
    }

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

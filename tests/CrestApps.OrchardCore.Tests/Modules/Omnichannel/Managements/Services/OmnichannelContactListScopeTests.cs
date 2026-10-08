using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelContactListScopeTests
{
    [Theory]
    [InlineData("Contact", true)]
    [InlineData("Lead", true)]
    [InlineData("Contact,Lead", true)]
    [InlineData("Contact, Lead", true)]
    [InlineData("Contact,BlogPost", false)]
    [InlineData("BlogPost", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public async Task IsContactOnlyListAsync_GatesOnRoutedContentTypes(string contentTypeId, bool expected)
    {
        // Arrange - the content admin list carries the type filter as a route value, not a query string.
        var provider = await CreateWarmedProviderAsync();
        var httpContext = new DefaultHttpContext();

        if (contentTypeId is not null)
        {
            httpContext.Request.RouteValues["contentTypeId"] = contentTypeId;
        }

        // Act
        var result = await OmnichannelContactListScope.IsContactOnlyListAsync(httpContext, provider);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task IsContactOnlyListAsync_WhenHttpContextIsNull_ReturnsFalse()
    {
        var provider = await CreateWarmedProviderAsync();

        var result = await OmnichannelContactListScope.IsContactOnlyListAsync(null, provider);

        Assert.False(result);
    }

    private static async Task<OmnichannelContentTypeProvider> CreateWarmedProviderAsync()
    {
        var definitions = new[]
        {
            new ContentTypeDefinitionBuilder().WithName("Contact").WithPart("OmnichannelContactPart").Build(),
            new ContentTypeDefinitionBuilder().WithName("Lead").WithPart("OmnichannelContactPart").Build(),
            new ContentTypeDefinitionBuilder().WithName("BlogPost").WithPart("BlogPostPart").Build(),
        };

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(manager => manager.ListTypeDefinitionsAsync())
            .ReturnsAsync(definitions);

        var provider = new OmnichannelContentTypeProvider(Mock.Of<IServiceScopeFactory>());
        await provider.EnsureInitializedAsync(contentDefinitionManager.Object);

        return provider;
    }
}

using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Events;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelContentTypeProviderTests
{
    private const string ContactPartName = "OmnichannelContactPart";

    [Fact]
    public async Task EnsureInitializedAsync_WhenPartAttachedDuringWarm_DoesNotLoseTheUpdate()
    {
        // Arrange - reproduce the warm race: the contact part is attached in the window between the warm
        // reading the definitions and committing its snapshot. The first read returns the pre-attach
        // definitions and, as a side effect, raises the attach event that lands mid-warm; the second read
        // (the retry) returns the post-attach definitions.
        var provider = new OmnichannelContentTypeProvider(Mock.Of<IServiceScopeFactory>());

        var beforeAttach = new[]
        {
            new ContentTypeDefinitionBuilder().WithName("BlogPost").WithPart("BlogPostPart").Build(),
        };

        var afterAttach = new[]
        {
            new ContentTypeDefinitionBuilder().WithName("BlogPost").WithPart("BlogPostPart").Build(),
            new ContentTypeDefinitionBuilder().WithName("Contact").WithPart(ContactPartName).Build(),
        };

        var readCount = 0;
        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(manager => manager.ListTypeDefinitionsAsync())
            .ReturnsAsync(() =>
            {
                readCount++;

                if (readCount == 1)
                {
                    // The attach lands after this (stale) snapshot is taken but before the warm commits.
                    provider.ContentPartAttached(new ContentPartAttachedContext
                    {
                        ContentTypeName = "Contact",
                        ContentPartName = ContactPartName,
                    });

                    return beforeAttach;
                }

                return afterAttach;
            });

        // Act
        await provider.EnsureInitializedAsync(contentDefinitionManager.Object);

        // Assert - the racing attach must not be swallowed; the warm re-reads and captures it.
        Assert.Contains("Contact", provider.GetContactContentTypes());
        Assert.True(readCount >= 2, "The warm should have re-read the definitions after the racing change.");
    }

    [Fact]
    public async Task EnsureInitializedAsync_AppliesAttachRaisedAfterWarmIncrementally()
    {
        // Arrange - a tenant that starts with no contact content type.
        var provider = new OmnichannelContentTypeProvider(Mock.Of<IServiceScopeFactory>());

        var definitions = new[]
        {
            new ContentTypeDefinitionBuilder().WithName("BlogPost").WithPart("BlogPostPart").Build(),
        };

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(manager => manager.ListTypeDefinitionsAsync())
            .ReturnsAsync(definitions);

        await provider.EnsureInitializedAsync(contentDefinitionManager.Object);
        Assert.Empty(provider.GetContactContentTypes());

        // Act - the contact part is attached later, after the cache is warm.
        provider.ContentPartAttached(new ContentPartAttachedContext
        {
            ContentTypeName = "Contact",
            ContentPartName = ContactPartName,
        });

        // Assert - the incremental update keeps the warm cache current without a re-read.
        Assert.Contains("Contact", provider.GetContactContentTypes());
    }
}

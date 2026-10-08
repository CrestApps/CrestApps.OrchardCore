using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentTypes.Events;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Crm;

public sealed class OmnichannelContentTypeProviderCrmTests
{
    [Fact]
    public async Task CrmEnabled_SplitsLeadTypesFromContactTypes()
    {
        // Arrange
        var provider = new OmnichannelContentTypeProvider(Mock.Of<IServiceScopeFactory>(), CrmOptions(enabled: true));

        // Act
        await provider.EnsureInitializedAsync(DefinitionManager());

        // Assert
        Assert.Equal(["Customer", "Lead"], provider.GetContactContentTypes().Order());
        Assert.Equal(["Customer"], provider.GetContactKindContentTypes());
        Assert.Equal(["Lead"], provider.GetLeadContentTypes());
        Assert.Equal(["Account"], provider.GetAccountContentTypes());
        Assert.Equal(["SalesOpportunity"], provider.GetOpportunityContentTypes());
        Assert.True(provider.IsLeadContentType("Lead"));
        Assert.False(provider.IsLeadContentType("Customer"));
    }

    [Fact]
    public async Task CrmDisabled_TreatsALeadMarkedTypeAsAPlainContact()
    {
        // Arrange - without the CRM feature there are no leads, so contacts behave exactly as before it existed.
        var provider = new OmnichannelContentTypeProvider(Mock.Of<IServiceScopeFactory>(), CrmOptions(enabled: false));

        // Act
        await provider.EnsureInitializedAsync(DefinitionManager());

        // Assert
        Assert.Empty(provider.GetLeadContentTypes());
        Assert.Equal(["Customer", "Lead"], provider.GetContactKindContentTypes().Order());
    }

    [Fact]
    public async Task AttachingTheLeadPart_MovesTheTypeFromContactsToLeads()
    {
        // Arrange
        var provider = new OmnichannelContentTypeProvider(Mock.Of<IServiceScopeFactory>(), CrmOptions(enabled: true));
        await provider.EnsureInitializedAsync(DefinitionManager());

        // Act
        provider.ContentPartAttached(new ContentPartAttachedContext
        {
            ContentTypeName = "Customer",
            ContentPartName = "LeadPart",
        });

        // Assert
        Assert.Contains("Customer", provider.GetLeadContentTypes());
        Assert.DoesNotContain("Customer", provider.GetContactKindContentTypes());
        Assert.Contains("Customer", provider.GetContactContentTypes());
    }

    private static IContentDefinitionManager DefinitionManager()
    {
        var definitions = new[]
        {
            new ContentTypeDefinitionBuilder().WithName("Customer").WithPart("OmnichannelContactPart").Build(),
            new ContentTypeDefinitionBuilder().WithName("Lead").WithPart("OmnichannelContactPart").WithPart("LeadPart").Build(),
            new ContentTypeDefinitionBuilder().WithName("Account").WithPart("AccountPart").WithPart("ListPart").Build(),
            new ContentTypeDefinitionBuilder().WithName("SalesOpportunity").WithPart("OpportunityPart").Build(),
            new ContentTypeDefinitionBuilder().WithName("BlogPost").WithPart("BlogPostPart").Build(),
        };

        var manager = new Mock<IContentDefinitionManager>();
        manager.Setup(m => m.ListTypeDefinitionsAsync()).ReturnsAsync(definitions);

        return manager.Object;
    }

    private static IOptions<OmnichannelCrmOptions> CrmOptions(bool enabled)
        => Options.Create(new OmnichannelCrmOptions { Enabled = enabled });
}

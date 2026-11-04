using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.ContentManagement.Metadata.Builders;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Crm;

public sealed class CrmAccountChildTypeRuleTests
{
    private readonly AccountContentTypeProvider _provider = new(new Accounts.InMemoryContentDefinitionManager(), [new CrmAccountChildTypeRule()]);

    [Fact]
    public void ContactType_BelongsToAnAccount()
    {
        var contact = new ContentTypeDefinitionBuilder().WithName("Contact").WithPart("OmnichannelContactPart").Build();

        Assert.True(_provider.IsAccountChildType(contact));
        Assert.False(_provider.IsExcludedFromAccounts(contact));
    }

    [Fact]
    public void OpportunityType_BelongsToAnAccount()
    {
        var opportunity = new ContentTypeDefinitionBuilder().WithName("SalesOpportunity").WithPart("OpportunityPart").Build();

        Assert.True(_provider.IsAccountChildType(opportunity));
    }

    [Fact]
    public void LeadType_IsExcludedFromAccounts()
    {
        // A lead carries the contact part too, so only the exclusion keeps it out of the account lists.
        var lead = new ContentTypeDefinitionBuilder().WithName("Lead").WithPart("OmnichannelContactPart").WithPart("LeadPart").Build();

        Assert.False(_provider.IsAccountChildType(lead));
        Assert.True(_provider.IsExcludedFromAccounts(lead));
    }

    [Fact]
    public void UnrelatedType_IsNotAChild()
    {
        var article = new ContentTypeDefinitionBuilder().WithName("Article").WithPart("TitlePart").Build();

        Assert.False(_provider.IsAccountChildType(article));
        Assert.False(_provider.IsExcludedFromAccounts(article));
    }
}

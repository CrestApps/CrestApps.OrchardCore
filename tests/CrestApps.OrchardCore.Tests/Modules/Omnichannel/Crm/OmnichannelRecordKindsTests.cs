using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement.Metadata.Builders;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Crm;

public sealed class OmnichannelRecordKindsTests
{
    [Fact]
    public void Classify_ContactType_IsReachableContactAndAccountChild()
    {
        // Arrange
        var contact = new ContentTypeDefinitionBuilder().WithName("Customer").WithPart("OmnichannelContactPart").Build();

        // Act & Assert
        Assert.True(OmnichannelRecordKinds.IsReachable(contact));
        Assert.True(OmnichannelRecordKinds.IsContact(contact));
        Assert.False(OmnichannelRecordKinds.IsLead(contact));
        Assert.True(OmnichannelRecordKinds.IsAccountChild(contact));
    }

    [Fact]
    public void Classify_LeadType_IsReachableLeadButNeverAccountChild()
    {
        // Arrange
        var lead = new ContentTypeDefinitionBuilder().WithName("Lead").WithPart("OmnichannelContactPart").WithPart("LeadPart").Build();

        // Act & Assert
        Assert.True(OmnichannelRecordKinds.IsReachable(lead));
        Assert.True(OmnichannelRecordKinds.IsLead(lead));
        Assert.False(OmnichannelRecordKinds.IsContact(lead));
        Assert.False(OmnichannelRecordKinds.IsAccountChild(lead));
    }

    [Fact]
    public void Classify_LeadPartWithoutContactPart_IsNotALead()
    {
        // Arrange - a lead is a reachable record, so the marker alone does not make one.
        var type = new ContentTypeDefinitionBuilder().WithName("Odd").WithPart("LeadPart").Build();

        // Act & Assert
        Assert.False(OmnichannelRecordKinds.IsLead(type));
        Assert.False(OmnichannelRecordKinds.IsAccountChild(type));
    }

    [Fact]
    public void Classify_OpportunityType_IsAccountChild()
    {
        // Arrange
        var opportunity = new ContentTypeDefinitionBuilder().WithName("ResellOpportunity").WithPart("OpportunityPart").Build();

        // Act & Assert
        Assert.True(OmnichannelRecordKinds.IsOpportunity(opportunity));
        Assert.True(OmnichannelRecordKinds.IsAccountChild(opportunity));
        Assert.False(OmnichannelRecordKinds.IsReachable(opportunity));
    }

    [Fact]
    public void Classify_AccountType_IsNeverItsOwnChild()
    {
        // Arrange - an account that also carries the contact part must not be offered inside accounts.
        var account = new ContentTypeDefinitionBuilder().WithName("Household").WithPart("AccountPart").WithPart("OmnichannelContactPart").Build();

        // Act & Assert
        Assert.True(OmnichannelRecordKinds.IsAccount(account));
        Assert.False(OmnichannelRecordKinds.IsAccountChild(account));
    }

    [Fact]
    public void Classify_Null_IsNothing()
    {
        Assert.False(OmnichannelRecordKinds.IsReachable(null));
        Assert.False(OmnichannelRecordKinds.IsLead(null));
        Assert.False(OmnichannelRecordKinds.IsAccountChild(null));
    }
}

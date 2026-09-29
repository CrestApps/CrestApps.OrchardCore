using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Crm;

public sealed class OmnichannelContactMatchesTests
{
    private static readonly string[] _leadTypes = ["Lead"];

    [Fact]
    public void Order_PutsContactsBeforeLeads()
    {
        // Arrange
        var matches = new[]
        {
            Row("lead-1", "Lead"),
            Row("contact-1", "Customer"),
        };

        // Act
        var ordered = OmnichannelContactMatches.Order(matches, _leadTypes);

        // Assert
        Assert.Equal(["contact-1", "lead-1"], ordered);
    }

    [Fact]
    public void Order_LeavesOutConvertedLeads()
    {
        // Arrange - a converted lead is never matched; the contact it became is.
        var matches = new[]
        {
            Row("lead-converted", "Lead", isConverted: true),
            Row("contact-1", "Customer"),
        };

        // Act
        var ordered = OmnichannelContactMatches.Order(matches, _leadTypes);

        // Assert
        Assert.Equal(["contact-1"], ordered);
    }

    [Fact]
    public void Order_TreatsRowsWithoutContentTypeAsContacts()
    {
        // Arrange - rows indexed before the content type column existed predate leads.
        var matches = new[]
        {
            Row("lead-1", "Lead"),
            Row("old-contact", null),
        };

        // Act
        var ordered = OmnichannelContactMatches.Order(matches, _leadTypes);

        // Assert
        Assert.Equal(["old-contact", "lead-1"], ordered);
    }

    [Fact]
    public void BestTier_ContactAndLeadAtOneNumber_ResolvesToTheContact()
    {
        // Arrange
        var matches = new[]
        {
            Row("contact-1", "Customer"),
            Row("lead-1", "Lead"),
        };

        // Act
        var best = OmnichannelContactMatches.BestTier(matches, _leadTypes);

        // Assert
        Assert.Equal(["contact-1"], best);
    }

    [Fact]
    public void BestTier_TwoContacts_StaysAmbiguous()
    {
        // Arrange
        var matches = new[]
        {
            Row("contact-1", "Customer"),
            Row("contact-2", "Customer"),
            Row("lead-1", "Lead"),
        };

        // Act
        var best = OmnichannelContactMatches.BestTier(matches, _leadTypes);

        // Assert
        Assert.Equal(["contact-1", "contact-2"], best);
    }

    [Fact]
    public void BestTier_OnlyLeads_ReturnsTheOpenLeads()
    {
        // Arrange
        var matches = new[]
        {
            Row("lead-1", "Lead"),
            Row("lead-2", "Lead", isConverted: true),
        };

        // Act
        var best = OmnichannelContactMatches.BestTier(matches, _leadTypes);

        // Assert
        Assert.Equal(["lead-1"], best);
    }

    [Fact]
    public void Order_WithoutLeadTypes_KeepsTheOriginalOrder()
    {
        // Arrange - a tenant without the CRM feature has no lead types, so nothing changes for it.
        var matches = new[]
        {
            Row("contact-2", "Customer"),
            Row("contact-1", "Lead"),
            Row("contact-2", "Customer"),
        };

        // Act
        var ordered = OmnichannelContactMatches.Order(matches, []);

        // Assert
        Assert.Equal(["contact-2", "contact-1"], ordered);
    }

    private static OmnichannelContactIndex Row(string contentItemId, string contentType, bool isConverted = false)
        => new()
        {
            ContentItemId = contentItemId,
            ContentType = contentType,
            IsConverted = isConverted,
            Published = true,
            Latest = true,
        };
}

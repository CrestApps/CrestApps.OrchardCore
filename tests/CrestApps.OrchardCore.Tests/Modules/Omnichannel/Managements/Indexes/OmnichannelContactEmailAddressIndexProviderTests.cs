using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Tests.PersonalData;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Indexes;

public sealed class OmnichannelContactEmailAddressIndexProviderTests
{
    private readonly OmnichannelContactEmailAddressIndexProvider _provider = new(TestContactMethods.Accessor);

    [Fact]
    public void CreateIndexes_WritesARowForEveryValidAddressInItsCanonicalFormOnce()
    {
        // Arrange
        // The contact index keeps only the first address; mail from the second, or an unsubscribe of it, has to find
        // the contact too.
        var contact = CrmPersonalDataTestContacts.Create(
            "Ann Lee",
            ["Ann@Example.com", "not an email", "\"Ann Lee\" <ann@example.com>", "mailto:Ann.Work@Contoso.com"],
            []);

        // Act
        var rows = _provider.CreateIndexes(contact).ToList();

        // Assert
        Assert.Equal(["ann@example.com", "ann.work@contoso.com"], rows.Select(row => row.NormalizedEmailAddress));
        Assert.All(rows, row =>
        {
            Assert.Equal(contact.ContentItemId, row.ContentItemId);
            Assert.Equal(ContactPhoneTestStore.ContactContentType, row.ContentType);
            Assert.True(row.Published);
            Assert.True(row.Latest);
            Assert.False(row.IsConverted);
        });
    }

    [Fact]
    public void CreateIndexes_ForAConvertedLead_MarksItsRows()
    {
        // Arrange
        var lead = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@example.com"], []);
        lead.ContentType = ContactPhoneTestStore.LeadContentType;
        lead.Alter<LeadPart>(part => part.IsConverted = true);

        // Act
        var row = Assert.Single(_provider.CreateIndexes(lead));

        // Assert
        Assert.Equal(ContactPhoneTestStore.LeadContentType, row.ContentType);
        Assert.True(row.IsConverted);
    }

    [Fact]
    public void CreateIndexes_ForAnOldVersion_WritesNothing()
    {
        // Arrange
        var contact = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@example.com"], []);
        contact.Published = false;
        contact.Latest = false;

        // Act & Assert
        Assert.Empty(_provider.CreateIndexes(contact));
    }
}

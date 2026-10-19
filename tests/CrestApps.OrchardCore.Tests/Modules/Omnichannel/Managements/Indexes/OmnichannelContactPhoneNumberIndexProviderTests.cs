using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Indexes;

public sealed class OmnichannelContactPhoneNumberIndexProviderTests
{
    private readonly OmnichannelContactPhoneNumberIndexProvider _provider = new(new DefaultPhoneNumberService(), TestContactMethods.Accessor);

    [Fact]
    public void CreateIndexes_WritesARowForEveryNumberWhateverItsType()
    {
        // Arrange
        // The contact index keeps only the first cell and home numbers; a company reached at its work number, or a
        // person's second cell number, has to be found too.
        var contact = ContactPhoneTestStore.CreateRecord(
            ContactPhoneTestStore.ContactContentType,
            ("Cell", "+17025550101"),
            ("Cell", "+17025550102"),
            ("Home", "+17025550103"),
            ("Work", "+17025550104"),
            ("Office", "+17025550105"),
            ("Other", "+17025550106"),
            ("Fax", "+17025550107"));

        // Act
        var rows = _provider.CreateIndexes(contact).ToList();

        // Assert
        Assert.Equal(
            [
                ("Cell", "+17025550101"),
                ("Cell", "+17025550102"),
                ("Home", "+17025550103"),
                ("Work", "+17025550104"),
                ("Office", "+17025550105"),
                ("Other", "+17025550106"),
                ("Fax", "+17025550107"),
            ],
            rows.Select(row => (row.PhoneType, row.NormalizedPhoneNumber)));
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
    public void CreateIndexes_WhenANumberIsSavedTwice_WritesItOnceUnderItsFirstType()
    {
        // Arrange
        var contact = ContactPhoneTestStore.CreateRecord(
            ContactPhoneTestStore.ContactContentType,
            ("Work", "+1 (702) 555-0104"),
            ("Cell", "+17025550104"));

        // Act
        var row = Assert.Single(_provider.CreateIndexes(contact));

        // Assert
        Assert.Equal("Work", row.PhoneType);
        Assert.Equal("+17025550104", row.NormalizedPhoneNumber);
    }

    [Fact]
    public void CreateIndexes_WhenANumberCannotBeReadAsE164_LeavesItOut()
    {
        // Arrange
        var contact = ContactPhoneTestStore.CreateRecord(
            ContactPhoneTestStore.ContactContentType,
            ("Cell", "not a number"),
            ("Work", "+17025550104"));

        // Act
        var row = Assert.Single(_provider.CreateIndexes(contact));

        // Assert
        Assert.Equal("+17025550104", row.NormalizedPhoneNumber);
    }

    [Fact]
    public void CreateIndexes_WhenTheLeadWasConverted_MarksEveryRow()
    {
        // Arrange
        var lead = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.LeadContentType, ("Work", "+17025550104"));
        lead.Alter<LeadPart>(part => part.IsConverted = true);

        // Act
        var row = Assert.Single(_provider.CreateIndexes(lead));

        // Assert
        Assert.True(row.IsConverted);
        Assert.Equal(ContactPhoneTestStore.LeadContentType, row.ContentType);
    }

    [Fact]
    public void CreateIndexes_WhenTheVersionIsNeitherPublishedNorLatest_WritesNothing()
    {
        // Arrange
        var contact = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Work", "+17025550104"));
        contact.Published = false;
        contact.Latest = false;

        // Act & Assert
        Assert.Empty(_provider.CreateIndexes(contact));
    }
}

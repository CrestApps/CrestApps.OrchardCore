using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class ContactOptOutResolverTests
{
    [Theory]
    [InlineData("Work", "Cell")]
    [InlineData("Office", "Other")]
    [InlineData("Cell", "Work")]
    public async Task HasOptedOutAsync_WhenAnotherRecordAtTheSameNumberAskedNotToBeCalled_IsTrueWhateverTypeEachSavedItAs(string optedOutType, string otherType)
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("opt-out-shared-number");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            // A request to stop recorded on one record stops every record reachable at one of its numbers. Comparing
            // only the first cell and home numbers let a record that saved the number as Work be dialed.
            var optedOut = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, (optedOutType, "+17025550104"));
            optedOut.Alter<OmnichannelContactPart>(part => part.DoNotCall = true);

            var other = ContactPhoneTestStore.CreateRecord(
                ContactPhoneTestStore.LeadContentType,
                ("Cell", "+17025550199"),
                (otherType, "+17025550104"));

            await ContactPhoneTestStore.SaveAsync(store, optedOut, other);

            await using var session = store.CreateSession();
            var resolver = new ContactOptOutResolver(session, ContactPhoneTestStore.CreateLookup(session));

            // Act
            var byPhone = await resolver.HasOptedOutAsync(other, OmnichannelConstants.Channels.Phone, TestContext.Current.CancellationToken);
            var bySms = await resolver.HasOptedOutAsync(other, OmnichannelConstants.Channels.Sms, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(byPhone);
            Assert.False(bySms);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task HasOptedOutAsync_WhenNoRecordSharesANumber_IsDecidedByTheContactAlone()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("opt-out-alone");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            var optedOut = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Work", "+17025550104"));
            optedOut.Alter<OmnichannelContactPart>(part => part.DoNotCall = true);

            var other = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Work", "+17025550105"));

            await ContactPhoneTestStore.SaveAsync(store, optedOut, other);

            await using var session = store.CreateSession();
            var resolver = new ContactOptOutResolver(session, ContactPhoneTestStore.CreateLookup(session));

            // Act & Assert
            Assert.True(await resolver.HasOptedOutAsync(optedOut, OmnichannelConstants.Channels.Phone, TestContext.Current.CancellationToken));
            Assert.False(await resolver.HasOptedOutAsync(other, OmnichannelConstants.Channels.Phone, TestContext.Current.CancellationToken));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }
}

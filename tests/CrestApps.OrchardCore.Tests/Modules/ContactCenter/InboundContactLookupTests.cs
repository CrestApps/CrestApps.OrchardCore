using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class InboundContactLookupTests
{
    [Fact]
    public async Task FindCallerItemIdsAsync_WhenTheCallerIsAContactsWorkNumber_MatchesTheContact()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("inbound-work-number");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            // The dialer reaches a company at its work number; when it calls back from it, it is not an unknown caller.
            var company = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Work", "+17025550104"));
            await ContactPhoneTestStore.SaveAsync(store, company);

            await using var session = store.CreateSession();
            var lookup = CreateLookup(session);

            // Act
            var callers = await lookup.FindCallerItemIdsAsync("+1 (702) 555-0104", TestContext.Current.CancellationToken);
            var contacts = await lookup.FindContactItemIdsAsync("+17025550104", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal([company.ContentItemId], callers);
            Assert.Equal([company.ContentItemId], contacts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task FindCallerItemIdsAsync_WhenAContactAndALeadShareTheNumber_MatchesTheContact()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("inbound-contact-before-lead");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            var lead = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.LeadContentType, ("Cell", "+17025550101"));
            var contact = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Office", "+17025550101"));
            await ContactPhoneTestStore.SaveAsync(store, lead, contact);

            await using var session = store.CreateSession();
            var lookup = CreateLookup(session);

            // Act
            var callers = await lookup.FindCallerItemIdsAsync("+17025550101", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal([contact.ContentItemId], callers);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task FindContactItemIdsAsync_WhenTheContactIsNotPublished_DoesNotMatchIt()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("inbound-draft");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            var draft = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Work", "+17025550104"));
            draft.Published = false;
            await ContactPhoneTestStore.SaveAsync(store, draft);

            await using var session = store.CreateSession();

            // Act
            var contacts = await CreateLookup(session).FindContactItemIdsAsync("+17025550104", TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(contacts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    private static InboundContactLookup CreateLookup(global::YesSql.ISession session)
    {
        var contactTypes = new Mock<IOmnichannelContactTypeProvider>();
        contactTypes
            .Setup(provider => provider.GetLeadContentTypesAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<string>>([ContactPhoneTestStore.LeadContentType]));

        return new InboundContactLookup(ContactPhoneTestStore.CreateLookup(session), new DefaultPhoneNumberService(), contactTypes.Object);
    }
}

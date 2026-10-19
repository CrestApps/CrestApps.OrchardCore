using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public sealed class SmsMessagingChannelContactMatchTests
{
    [Fact]
    public async Task FindContactIdsAsync_WhenTheSenderIsAContactsWorkNumber_MatchesTheContactBeforeALead()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("sms-sender-work-number");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            // A text sent to a contact's work number is answered from it; matching only the first cell and home
            // numbers left the reply attached to nobody.
            var lead = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.LeadContentType, ("Cell", "+17025550104"));
            var contact = ContactPhoneTestStore.CreateRecord(
                ContactPhoneTestStore.ContactContentType,
                ("Cell", "+17025550101"),
                ("Work", "+17025550104"));
            await ContactPhoneTestStore.SaveAsync(store, lead, contact);

            await using var session = store.CreateSession();

            var contactTypes = new Mock<IOmnichannelContactTypeProvider>();
            contactTypes
                .Setup(provider => provider.GetLeadContentTypesAsync(It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult<IReadOnlyCollection<string>>([ContactPhoneTestStore.LeadContentType]));

            var channel = new SmsMessagingChannel(
                new Lazy<ISmsDispatcher>(() => throw new InvalidOperationException("No text is sent.")),
                session,
                Mock.Of<IStringLocalizer<SmsMessagingChannel>>(),
                contactTypes.Object,
                new DefaultPhoneNumberService(),
                Modules.Omnichannel.TestContactMethods.Accessor,
                ContactPhoneTestStore.CreateLookup(session));

            // Act
            var matches = await channel.FindContactIdsAsync("+17025550104", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal([contact.ContentItemId, lead.ContentItemId], matches);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }
}

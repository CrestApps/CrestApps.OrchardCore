using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;
using CrestApps.OrchardCore.Tests.PersonalData;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

/// <summary>
/// The email channel finds a contact by any of its addresses, against the rows and schema a tenant has.
/// </summary>
public sealed class EmailMessagingChannelContactMatchTests : IAsyncLifetime
{
    private string _path;
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("email-contact-match");

        _path = path;
        _store = await ContactPhoneTestStore.CreateAsync(connectionString);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task FindContactIdsAsync_FindsAContactByItsSecondAddress()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var ann = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@example.com", "Ann.Work@Contoso.com"], []);
        var bob = CrmPersonalDataTestContacts.Create("Bob Ray", ["bob@example.com"], []);
        await ContactPhoneTestStore.SaveAsync(_store, ann, bob);

        await using var session = _store.CreateSession();
        var channel = CreateChannel(session);

        // Act
        var byFirst = await channel.FindContactIdsAsync("ann@example.com", cancellationToken);
        var bySecond = await channel.FindContactIdsAsync("\"Ann Lee\" <ann.work@contoso.com>", cancellationToken);
        var byNobody = await channel.FindContactIdsAsync("nobody@example.com", cancellationToken);

        // Assert
        Assert.Equal([ann.ContentItemId], byFirst);
        Assert.Equal([ann.ContentItemId], bySecond);
        Assert.Empty(byNobody);
    }

    [Fact]
    public async Task FindContactIdsAsync_PutsAContactBeforeALeadAtTheSameAddress()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var lead = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@example.com"], []);
        lead.ContentType = ContactPhoneTestStore.LeadContentType;
        var contact = CrmPersonalDataTestContacts.Create("Ann Lee", ["other@example.com", "ann@example.com"], []);
        await ContactPhoneTestStore.SaveAsync(_store, lead, contact);

        await using var session = _store.CreateSession();
        var channel = CreateChannel(session);

        // Act
        var ids = await channel.FindContactIdsAsync("ann@example.com", cancellationToken);

        // Assert
        Assert.Equal([contact.ContentItemId, lead.ContentItemId], ids);
    }

    [Fact]
    public async Task SearchContactIdsByAddressAsync_SearchesEveryAddressAndReturnsEachRecordOnce()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var ann = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@contoso.com", "ann.work@contoso.com"], []);
        var bob = CrmPersonalDataTestContacts.Create("Bob Ray", ["bob@example.com"], []);
        await ContactPhoneTestStore.SaveAsync(_store, ann, bob);

        await using var session = _store.CreateSession();
        var channel = CreateChannel(session);

        // Act
        var byDomain = await channel.SearchContactIdsByAddressAsync("CONTOSO", [ContactPhoneTestStore.ContactContentType], 10, cancellationToken);
        var bySecond = await channel.SearchContactIdsByAddressAsync("work@", [ContactPhoneTestStore.ContactContentType], 10, cancellationToken);
        var otherType = await channel.SearchContactIdsByAddressAsync("contoso", [ContactPhoneTestStore.LeadContentType], 10, cancellationToken);

        // Assert
        Assert.Equal([ann.ContentItemId], byDomain);
        Assert.Equal([ann.ContentItemId], bySecond);
        Assert.Empty(otherType);
    }

    [Fact]
    public async Task FindContactIdsAsync_FindsARecordTheBackfillHasNotReachedByItsPrimaryEmailAsSaved()
    {
        // Arrange: records saved before the email address index existed have no address rows and no canonical primary
        // email until the backfill saves them again.
        var cancellationToken = TestContext.Current.CancellationToken;
        var ann = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@example.com"], []);
        var bob = CrmPersonalDataTestContacts.Create("Bob Ray", ["Bob.Ray@Example.com"], []);
        await ContactPhoneTestStore.SaveAsync(_store, ann, bob);
        await ForgetEmailIndexingAsync();

        await using var session = _store.CreateSession();
        var channel = CreateChannel(session);

        // Act
        var byCanonical = await channel.FindContactIdsAsync("\"Ann Lee\" <Ann@Example.com>", cancellationToken);
        var bySavedSpelling = await channel.FindContactIdsAsync("Bob.Ray@Example.com", cancellationToken);

        // Assert
        Assert.Equal([ann.ContentItemId], byCanonical);
        Assert.Equal([bob.ContentItemId], bySavedSpelling);
    }

    [Fact]
    public async Task SearchContactIdsByAddressAsync_TakesUnderscoresLiterally()
    {
        // Arrange: in LIKE, '_' matches any one character, so "j_smith" would also find "jasmith".
        var cancellationToken = TestContext.Current.CancellationToken;
        var underscore = CrmPersonalDataTestContacts.Create("John Smith", ["j_smith@contoso.com"], []);
        var other = CrmPersonalDataTestContacts.Create("Jane Smith", ["jasmith@contoso.com"], []);
        await ContactPhoneTestStore.SaveAsync(_store, underscore, other);

        await using var session = _store.CreateSession();
        var channel = CreateChannel(session);

        // Act
        var ids = await channel.SearchContactIdsByAddressAsync("j_smith", [ContactPhoneTestStore.ContactContentType], 10, cancellationToken);
        var everything = await channel.SearchContactIdsByAddressAsync("%%%", [ContactPhoneTestStore.ContactContentType], 10, cancellationToken);

        // Assert
        Assert.Equal([underscore.ContentItemId], ids);
        Assert.Empty(everything);
    }

    // Puts the store back in the state of a tenant whose records were saved before the email address index existed.
    private async Task ForgetEmailIndexingAsync()
    {
        var configuration = _store.Configuration;
        var dialect = configuration.SqlDialect;

        string Table(Type type)
            => dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(type), configuration.Schema);

        await using var connection = configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"DELETE FROM {Table(typeof(OmnichannelContactEmailAddressIndex))}; " +
            $"UPDATE {Table(typeof(OmnichannelContactIndex))} SET {dialect.QuoteForColumnName(nameof(OmnichannelContactIndex.NormalizedPrimaryEmailAddress))} = NULL;";

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static EmailMessagingChannel CreateChannel(global::YesSql.ISession session)
    {
        var contactTypes = new Mock<IOmnichannelContactTypeProvider>();
        contactTypes
            .Setup(provider => provider.GetLeadContentTypesAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<string>>([ContactPhoneTestStore.LeadContentType]));

        return new(
            new Lazy<IEmailDispatcher>(() => Mock.Of<IEmailDispatcher>()),
            contactTypes.Object,
            session,
            new PassThroughStringLocalizer<EmailMessagingChannel>(),
            TestContactMethods.Accessor,
            ConsentDoubles.Over(new SharedNumberOptOutResolver()));
    }
}

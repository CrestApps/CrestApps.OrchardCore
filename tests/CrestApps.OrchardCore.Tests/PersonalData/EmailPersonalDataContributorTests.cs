using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.PersonalData;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.Entities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// A privacy request reaches what the email channel keeps about a person: the emails exchanged with them, the delivery
/// log and the suppression list, found by any spelling of their address; and the CRM finds their email activities
/// whatever case the address was given in.
/// </summary>
public sealed class EmailPersonalDataContributorTests : IAsyncLifetime
{
    private const string DeliverabilityCollection = EmailChannelConstants.DeliverabilityCollectionName;

    private static readonly DateTime _now = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private string _path;
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _path = Path.Combine(Path.GetTempPath(), $"crestapps-pd-email-{Guid.NewGuid():N}.db");
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new OmnichannelMessageIndexProvider()], OmnichannelConstants.CollectionName);
        _store.RegisterIndexes([new EmailDeliveryLogIndexProvider(), new EmailSuppressionIndexProvider()], DeliverabilityCollection);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, cancellationToken);
        await _store.InitializeCollectionAsync(DeliverabilityCollection, cancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelMessageIndex>(table => table
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("CustomerAddress", column => column.WithLength(255))
            .Column<string>("ServiceAddress", column => column.WithLength(255))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<bool>("IsInbound", column => column.NotNull().WithDefault(false))
            .Column<string>("ConversationId", column => column.WithLength(26))
            .Column<string>("ProviderMessageId", column => column.WithLength(128)),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<EmailDeliveryLogIndex>(table => table
            .Column<string>("AddressId", column => column.WithLength(EmailDeliveryLogIndex.AddressIdLength))
            .Column<int>("Kind")
            .Column<string>("Recipient", column => column.WithLength(EmailDeliveryLogIndex.RecipientLength))
            .Column<string>("RecipientDomain", column => column.WithLength(EmailDeliveryLogIndex.RecipientLength))
            .Column<string>("MessageId", column => column.WithLength(EmailDeliveryLogIndex.MessageIdLength))
            .Column<string>("EventId", column => column.WithLength(EmailDeliveryLogIndex.EventIdLength))
            .Column<bool>("IsBulk")
            .Column<bool>("IsUnconfirmed", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("OccurredUtc", column => column.NotNull()),
            collection: DeliverabilityCollection);

        await schemaBuilder.CreateMapIndexTableAsync<EmailSuppressionIndex>(table => table
            .Column<string>("Address", column => column.WithLength(EmailDeliveryLogIndex.RecipientLength))
            .Column<int>("Reason")
            .Column<DateTime>("CreatedUtc", column => column.NotNull()),
            collection: DeliverabilityCollection);

        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Export_FindsTheEmailsTheLogAndTheSuppressionOfThePersonsAddress_HoweverItWasTyped()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync();

        await using var session = _store.CreateSession();
        var contributor = new EmailPersonalDataContributor(session, Mock.Of<IMessagingAttachmentStore>());
        var export = new PersonalDataExport();

        // Act
        await contributor.ExportAsync(new PersonalDataExportContext(new PersonalDataSubject(email: "Ann@Example.com"), export), cancellationToken);

        // Assert
        var messages = export.Records.Where(record => record.Kind == EmailPersonalDataContributor.MessageKind).ToList();
        Assert.Equal(["m-ann-in", "m-ann-out"], messages.Select(record => record.Id));

        var inbound = messages[0];
        Assert.Equal("Hi, it's Ann", inbound.Fields[nameof(OmnichannelMessage.Content)]);
        Assert.Equal("Order 1001", inbound.Fields[nameof(MessagingMessageDetails.Subject)]);
        Assert.Equal("Ann Lee", inbound.Fields[nameof(EmailMessageMetadata.FromName)]);
        Assert.Equal("receipt.pdf", inbound.Fields[nameof(MessagingMessageAttachments)]);

        var entry = Assert.Single(export.Records, record => record.Kind == EmailPersonalDataContributor.DeliveryLogKind);
        Assert.Equal("ann@example.com", entry.Fields[nameof(EmailDeliveryLogEntry.Recipient)]);

        var suppression = Assert.Single(export.Records, record => record.Kind == EmailPersonalDataContributor.SuppressionKind);
        Assert.Equal("ann@example.com", suppression.Fields[nameof(EmailSuppression.Address)]);
    }

    [Fact]
    public async Task Anonymize_EmptiesTheEmailsAndTheLog_DeletesTheFiles_AndKeepsTheAddressSuppressed()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync();

        var attachments = new Mock<IMessagingAttachmentStore>();
        attachments
            .Setup(store => store.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var result = new PersonalDataAnonymizeResult();

        // Act
        await using (var session = _store.CreateSession())
        {
            var contributor = new EmailPersonalDataContributor(session, attachments.Object);

            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(new PersonalDataSubject(email: "ann@example.com"), null, result), cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        // Assert
        Assert.Equal(2, result.Records[EmailPersonalDataContributor.MessageKind]);
        Assert.Equal(1, result.Records[EmailPersonalDataContributor.DeliveryLogKind]);
        Assert.Equal(1, result.Records[EmailPersonalDataContributor.SuppressionKind]);
        attachments.Verify(store => store.DeleteAsync("file-1", It.IsAny<CancellationToken>()), Times.Once);

        await using var check = _store.CreateSession();
        var messages = (await check.Query<OmnichannelMessage, OmnichannelMessageIndex>(collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken))
            .ToDictionary(message => message.Id, StringComparer.Ordinal);

        var inbound = messages["m-ann-in"];
        Assert.Equal(string.Empty, inbound.Content);
        Assert.Null(inbound.CustomerAddress);
        Assert.Null(inbound.GetSubject());
        Assert.Null(inbound.GetQuotedText());
        Assert.Empty(inbound.GetAttachments());
        Assert.Null(inbound.GetEmailMetadata().FromName);
        Assert.Empty(inbound.GetEmailMetadata().Cc);

        // The record and the identifiers it is threaded by stay.
        Assert.Equal("msg-1", inbound.GetEmailMetadata().MessageId);
        Assert.Equal("support@contoso.com", inbound.ServiceAddress);

        // Somebody else's email is left alone.
        Assert.Equal("Hello from Bob", messages["m-bob"].Content);

        var entries = (await check.Query<EmailDeliveryLogEntry, EmailDeliveryLogIndex>(collection: DeliverabilityCollection)
            .ListAsync(cancellationToken))
            .ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        Assert.Null(entries["log-ann"].Recipient);
        Assert.Null(entries["log-ann"].Detail);
        Assert.Equal("example.com", entries["log-ann"].RecipientDomain);
        Assert.Equal("bob@example.com", entries["log-bob"].Recipient);

        // The address stays suppressed, so the next broadcast does not mail the person again.
        var suppressions = (await check.Query<EmailSuppression, EmailSuppressionIndex>(collection: DeliverabilityCollection)
            .ListAsync(cancellationToken))
            .ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        Assert.Equal("ann@example.com", suppressions["sup-ann"].Address);
        Assert.Null(suppressions["sup-ann"].Detail);
        Assert.Equal("Reported as spam", suppressions["sup-bob"].Detail);
    }

    [Fact]
    public void CrmDestinations_KeepTheCanonicalSpellingOfAnEmailBesideTheOneGiven()
    {
        // Act
        var destinations = CrmActivityPersonalDataContributor.GetDestinations(["Ann@Example.com ", "ann@example.com"], ["+17025550100"]);

        // Assert
        // An email activity's destination is stored in lower case, and PostgreSQL and SQLite compare by case.
        Assert.Equal(["Ann@Example.com", "ann@example.com", "+17025550100"], destinations);
    }

    private async Task SeedAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var session = _store.CreateSession();

        var inbound = Message("m-ann-in", "ann@example.com", "Hi, it's Ann", isInbound: true, _now);
        inbound.SetSubject("Order 1001");
        inbound.SetQuotedText("> Your order has shipped");
        inbound.SetAttachments([new MessagingAttachment { Id = "file-1", FileName = "receipt.pdf", ContentType = "application/pdf", Length = 10 }]);
        inbound.Put(new EmailMessageMetadata
        {
            MessageId = "msg-1",
            FromName = "Ann Lee",
            Cc = ["ann.partner@example.com"],
        });

        var outbound = Message("m-ann-out", "ann@example.com", "Thanks Ann", isInbound: false, _now.AddMinutes(5));
        var other = Message("m-bob", "bob@example.com", "Hello from Bob", isInbound: true, _now.AddMinutes(10));

        foreach (var message in new[] { inbound, outbound, other })
        {
            await session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
        }

        await session.SaveAsync(new EmailDeliveryLogEntry
        {
            ItemId = "log-ann",
            AddressId = "address-1",
            Kind = EmailDeliveryEventKind.HardBounce,
            Recipient = "ann@example.com",
            RecipientDomain = "example.com",
            MessageId = "msg-2",
            OccurredUtc = _now,
            Detail = "550 5.1.1 <ann@example.com>: mailbox unavailable",
        }, collection: DeliverabilityCollection, cancellationToken: cancellationToken);

        await session.SaveAsync(new EmailDeliveryLogEntry
        {
            ItemId = "log-bob",
            AddressId = "address-1",
            Kind = EmailDeliveryEventKind.Sent,
            Recipient = "bob@example.com",
            RecipientDomain = "example.com",
            MessageId = "msg-3",
            OccurredUtc = _now,
        }, collection: DeliverabilityCollection, cancellationToken: cancellationToken);

        await session.SaveAsync(new EmailSuppression
        {
            ItemId = "sup-ann",
            Address = "ann@example.com",
            Reason = EmailSuppressionReason.HardBounce,
            Detail = "550 5.1.1 <ann@example.com>: mailbox unavailable",
            CreatedUtc = _now,
        }, collection: DeliverabilityCollection, cancellationToken: cancellationToken);

        await session.SaveAsync(new EmailSuppression
        {
            ItemId = "sup-bob",
            Address = "bob@example.com",
            Reason = EmailSuppressionReason.Complaint,
            Detail = "Reported as spam",
            CreatedUtc = _now,
        }, collection: DeliverabilityCollection, cancellationToken: cancellationToken);

        await session.SaveChangesAsync(cancellationToken);
    }

    private static OmnichannelMessage Message(string id, string customerAddress, string content, bool isInbound, DateTime createdUtc)
        => new()
        {
            Id = id,
            Channel = EmailChannelConstants.ChannelName,
            CustomerAddress = customerAddress,
            ServiceAddress = "support@contoso.com",
            Content = content,
            IsInbound = isInbound,
            CreatedUtc = createdUtc,
        };
}

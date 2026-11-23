using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The requester e-mails go through the templated e-mail outbox, carrying their ticket and the reply they belong to: an
/// e-mail that isn't delivered at once leaves a notice on the reply, a later delivery clears it, a given-up e-mail says
/// it wasn't delivered, and an e-mail the site can't send says so at once. The e-mails an earlier version kept in the
/// service desk's own outbox are handed to the templated e-mail outbox. An import creates tickets without telling
/// anyone.
/// </summary>
public sealed class TicketEmailOutboxTests
{
    [Fact]
    public async Task AnEmail_CarriesItsTicketAndReply_ToTheTemplatedEmailOutbox()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var (ticketId, entryId) = await ReplyAsync(kit);

        // Act
        var sent = await SendAsync(kit, Item(ticketId, entryId));

        // Assert
        Assert.True(sent);
        var email = Assert.Single(kit.Sender.Sent);
        Assert.Equal("dana@example.com", email.Email.To);
        Assert.Equal(TicketsConstants.EmailKinds.Reply, email.Email.Kind);
        Assert.Equal(new RecordRef(TicketsConstants.RecordKind, ticketId), email.Email.Source);
        Assert.Equal(entryId, email.Email.Properties[TicketEmailOutbox.EntryIdProperty]);
        Assert.Equal("<p>Hello</p>", ((TicketEmailReply)email.Email.Models["Reply"]).Html);
        Assert.Equal("[T-1] Printer jams", email.Content.Subject);
        Assert.Null(await NoticeAsync(kit, ticketId, entryId));
    }

    [Fact]
    public async Task AnEmailThatIsntDelivered_LeavesANoticeOnTheReply_UntilItIs()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var (ticketId, entryId) = await ReplyAsync(kit);
        kit.Sender.Failure = new InvalidOperationException("Mailbox unavailable");

        // Act
        var sent = await SendAsync(kit, Item(ticketId, entryId));

        // Assert
        Assert.False(sent);
        var notice = await NoticeAsync(kit, ticketId, entryId);
        Assert.StartsWith("Not delivered yet; trying again.", notice);
        Assert.Contains("Mailbox unavailable", notice);

        // Act: the outbox's retry delivers it.
        await ObserveAsync(kit, (observer, token) => observer.DeliveredAsync(Delivery(ticketId, entryId, TemplatedEmailOutboxStatus.Sent, null), token));

        // Assert
        Assert.Null(await NoticeAsync(kit, ticketId, entryId));
    }

    [Fact]
    public async Task AnEmailGivenUp_LeavesANotDeliveredNoticeOnTheReply()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var (ticketId, entryId) = await ReplyAsync(kit);

        // Act
        await ObserveAsync(kit, (observer, token) => observer.AbandonedAsync(Delivery(ticketId, entryId, TemplatedEmailOutboxStatus.Abandoned, "Mailbox unavailable"), token));

        // Assert
        var notice = await NoticeAsync(kit, ticketId, entryId);
        Assert.StartsWith("Not delivered to the requester", notice);
        Assert.Contains("Mailbox unavailable", notice);
    }

    [Fact]
    public async Task TheEmailOfAnotherRecord_LeavesTheTicketsEntriesAlone()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var (ticketId, entryId) = await ReplyAsync(kit);
        var delivery = new TemplatedEmailDelivery
        {
            ItemId = "other",
            Source = new RecordRef("MailingListMember", "m1"),
            Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TicketEmailOutbox.EntryIdProperty] = entryId },
            Status = TemplatedEmailOutboxStatus.Abandoned,
            LastError = "Mailbox unavailable",
        };

        // Act
        await ObserveAsync(kit, (observer, token) => observer.AbandonedAsync(delivery, token));

        // Assert
        Assert.Null(await NoticeAsync(kit, ticketId, entryId));
    }

    [Fact]
    public async Task AnEmailTheSiteCantSend_SaysAtOnceThatItWasntDelivered()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync(configureServices: services => services.AddScoped<ITemplatedEmailOutbox, SkippingOutbox>());
        var (ticketId, entryId) = await ReplyAsync(kit);

        // Act
        var sent = await SendAsync(kit, Item(ticketId, entryId));

        // Assert
        Assert.False(sent);
        Assert.StartsWith("Not delivered to the requester", await NoticeAsync(kit, ticketId, entryId));
    }

    [Fact]
    public async Task TheEmailsAnEarlierVersionKept_AreHandedToTheTemplatedEmailOutbox_AndRemoved()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var (ticketId, entryId) = await ReplyAsync(kit);
        await kit.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var waiting = Item(ticketId, entryId);
            waiting.Status = TicketEmailOutboxStatus.Pending;
            waiting.Attempts = 2;
            var sent = Item(ticketId, null);
            sent.Status = TicketEmailOutboxStatus.Sent;
            sent.Kind = TicketsConstants.EmailKinds.Received;
            await session.SaveAsync(waiting, false, TicketsConstants.CollectionName, TestContext.Current.CancellationToken);
            await session.SaveAsync(sent, false, TicketsConstants.CollectionName, TestContext.Current.CancellationToken);

            return true;
        });

        // Act
        var first = await kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().HandOverAsync(TestContext.Current.CancellationToken));
        var second = await kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().HandOverAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(2, first);
        Assert.Equal(0, second);
        var email = Assert.Single(kit.Sender.Sent);
        Assert.Equal(TicketsConstants.EmailKinds.Reply, email.Email.Kind);
        Assert.Equal(entryId, email.Email.Properties[TicketEmailOutbox.EntryIdProperty]);
        Assert.Null(await kit.ReadAsync(services => services.GetRequiredService<ISession>().Query<TicketEmailOutboxItem>(TicketsConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task AnImportedTicket_TellsNoObserver_SoNoRequesterIsEmailed()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();

        // Act
        var result = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().CreateAsync(new TicketCreateRequest
        {
            Subject = "Old ticket",
            Body = "From the previous help desk.",
            DeskId = "support",
            Channel = TicketsConstants.Channels.Agent,
            IsImport = true,
        }, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(kit.Observers.Events);
        Assert.NotNull(await kit.GetAsync(result.Ticket.ContentItemId));
    }

    private static async Task<(string TicketId, string EntryId)> ReplyAsync(TicketKit kit)
    {
        var created = await kit.CreateAsync();
        var ticketId = created.Ticket.ContentItemId;
        await kit.RunAsync(services => services.GetRequiredService<ITicketService>().ReplyAsync(ticketId, new TicketReply { Body = "Hello" }, TestContext.Current.CancellationToken));
        var entry = (await kit.EntriesAsync(ticketId)).Single(value => value.Body == "Hello");

        return (ticketId, entry.ItemId);
    }

    private static Task<bool> SendAsync(TicketKit kit, TicketEmailOutboxItem item)
        => kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().SendAsync(item, TestContext.Current.CancellationToken));

    // Tells the Tickets observer of the templated e-mail outbox what became of an e-mail, as the outbox's retries do.
    private static Task<bool> ObserveAsync(TicketKit kit, Func<ITemplatedEmailOutboxObserver, CancellationToken, Task> callback)
        => kit.RunAsync(async services =>
        {
            var observer = services.GetServices<ITemplatedEmailOutboxObserver>().OfType<TicketEmailDeliveryObserver>().Single();
            await callback(observer, TestContext.Current.CancellationToken);

            return true;
        });

    private static TemplatedEmailDelivery Delivery(string ticketId, string entryId, TemplatedEmailOutboxStatus status, string error)
        => new()
        {
            ItemId = "email-1",
            Kind = TicketsConstants.EmailKinds.Reply,
            To = "dana@example.com",
            Source = new RecordRef(TicketsConstants.RecordKind, ticketId),
            Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TicketEmailOutbox.EntryIdProperty] = entryId },
            Status = status,
            LastError = error,
        };

    private static TicketEmailOutboxItem Item(string ticketId, string entryId)
        => new()
        {
            TicketId = ticketId,
            EntryId = entryId,
            Kind = TicketsConstants.EmailKinds.Reply,
            To = "dana@example.com",
            Reference = "T-1",
            Subject = "[T-1] Printer jams",
            Html = "<p>Hello</p>",
            TicketNumber = "T-1",
            TicketSubject = "Printer jams",
            ReplyBody = "Hello",
            ReplyHtml = "<p>Hello</p>",
            ReplyAuthorName = "Ana",
        };

    private static async Task<string> NoticeAsync(TicketKit kit, string ticketId, string entryId)
    {
        var entry = (await kit.EntriesAsync(ticketId)).Single(value => value.ItemId == entryId);

        return entry.Properties is not null && entry.Properties.TryGetValue(TimelineConstants.Properties.Notice, out var notice) ? notice?.ToString() : null;
    }

    // An outbox on a site without an e-mail service.
    private sealed class SkippingOutbox : ITemplatedEmailOutbox
    {
        public Task<TemplatedEmailOutboxResult> SendAsync(TemplatedEmail email, Func<CancellationToken, Task<TemplatedEmailContent>> buildAsync, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplatedEmailOutboxResult { Status = TemplatedEmailOutboxStatus.Skipped });
    }
}

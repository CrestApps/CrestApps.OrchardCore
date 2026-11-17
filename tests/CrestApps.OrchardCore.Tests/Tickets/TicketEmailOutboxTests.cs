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
/// The requester e-mails go through an outbox on a real store: a send that throws leaves the e-mail to try again and a
/// notice on the reply that it wasn't delivered; a later send that works clears the notice; after the last try the
/// e-mail is given up and the reply says so. An import creates tickets without telling anyone.
/// </summary>
public sealed class TicketEmailOutboxTests
{
    [Fact]
    public async Task ASendThatThrows_IsTriedAgain_AndTheReplyShowsItWasntDelivered_UntilItIs()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var (ticketId, entryId) = await ReplyAsync(kit);
        kit.Sender.Failure = new InvalidOperationException("Mailbox unavailable");

        // Act
        var sent = await kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().SendAsync(Item(ticketId, entryId), TestContext.Current.CancellationToken));

        // Assert
        Assert.False(sent);
        var pending = await OutboxAsync(kit);
        Assert.Equal(TicketEmailOutboxStatus.Pending, pending.Status);
        Assert.Equal(1, pending.Attempts);
        Assert.Equal(Calendars.CalendarTestHarness.Now.AddMinutes(5), pending.NextAttemptUtc);
        Assert.Equal("Mailbox unavailable", pending.LastError);
        Assert.Contains("Mailbox unavailable", await NoticeAsync(kit, ticketId, entryId));

        // Act: the retry is due and the mailbox is back.
        await DueNowAsync(kit);
        kit.Sender.Failure = null;
        var retried = await kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().SendDueAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, retried);
        Assert.Equal(TicketEmailOutboxStatus.Sent, (await OutboxAsync(kit)).Status);
        Assert.Null(await NoticeAsync(kit, ticketId, entryId));
        var email = Assert.Single(kit.Sender.Sent);
        Assert.Equal("dana@example.com", email.Email.To);
        Assert.Equal("<p>Hello</p>", ((TicketEmailReply)email.Email.Models["Reply"]).Html);
    }

    [Fact]
    public async Task AnEmailThatKeepsFailing_IsGivenUpAfterSixTries_AndTheReplySaysItWasntDelivered()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var (ticketId, entryId) = await ReplyAsync(kit);
        kit.Sender.Failure = new InvalidOperationException("Mailbox unavailable");
        await kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().SendAsync(Item(ticketId, entryId), TestContext.Current.CancellationToken));

        // Act
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await DueNowAsync(kit);
            await kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().SendDueAsync(TestContext.Current.CancellationToken));
        }

        // Assert
        var abandoned = await OutboxAsync(kit);
        Assert.Equal(TicketEmailOutboxStatus.Abandoned, abandoned.Status);
        Assert.Equal(6, abandoned.Attempts);
        Assert.Null(abandoned.NextAttemptUtc);
        Assert.StartsWith("Not delivered to the requester", await NoticeAsync(kit, ticketId, entryId));
        Assert.Equal(0, await kit.RunAsync(services => services.GetRequiredService<TicketEmailOutbox>().SendDueAsync(TestContext.Current.CancellationToken)));
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

    private static Task<TicketEmailOutboxItem> OutboxAsync(TicketKit kit)
        => kit.ReadAsync(services => services.GetRequiredService<ISession>().Query<TicketEmailOutboxItem>(TicketsConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken));

    // Moves the next try to now, as if its wait had passed.
    private static Task<bool> DueNowAsync(TicketKit kit)
        => kit.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var item = await session.Query<TicketEmailOutboxItem>(TicketsConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            item.NextAttemptUtc = Calendars.CalendarTestHarness.Now;
            await session.SaveAsync(item, false, TicketsConstants.CollectionName, TestContext.Current.CancellationToken);

            return true;
        });

    private static async Task<string> NoticeAsync(TicketKit kit, string ticketId, string entryId)
    {
        var entry = (await kit.EntriesAsync(ticketId)).Single(value => value.ItemId == entryId);

        return entry.Properties is not null && entry.Properties.TryGetValue(TimelineConstants.Properties.Notice, out var notice) ? notice?.ToString() : null;
    }
}

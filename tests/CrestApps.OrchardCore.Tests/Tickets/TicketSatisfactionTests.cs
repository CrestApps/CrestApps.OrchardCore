using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Satisfaction on a real store: a request is rated once it is solved, from 1 to 5, and the rating is on the ticket, its
/// index row and its timeline, and observers hear of it; the requester of a recently solved ticket is asked once, after
/// the desk's delay, and a ticket solved long before isn't asked at all.
/// </summary>
public sealed class TicketSatisfactionTests
{
    [Fact]
    public async Task ARequestIsRatedOnceSolved_FromOneToFive()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var early = await RateAsync(kit, ticketId, 5, null);
        await kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, new TicketUpdate { StatusId = TicketKit.Solved.ItemId }, cancellationToken: TestContext.Current.CancellationToken));
        var outOfRange = await RateAsync(kit, ticketId, 9, null);
        var rated = await RateAsync(kit, ticketId, 4, "  Quick and clear.  ");

        // Assert
        Assert.False(early.Succeeded);
        Assert.False(outOfRange.Succeeded);
        Assert.True(rated.Succeeded);
        var part = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        Assert.Equal(4, part.Satisfaction.Score);
        Assert.Equal("Quick and clear.", part.Satisfaction.Comment);
        var row = await kit.ReadAsync(services => services.GetRequiredService<ISession>().QueryIndex<TicketIndex>(index => index.ContentItemId == ticketId).FirstOrDefaultAsync(TestContext.Current.CancellationToken));
        Assert.Equal(4, row.SatisfactionScore);
        Assert.Contains(await kit.EntriesAsync(ticketId), entry => entry.Body?.StartsWith("Rated 4 out of 5.", StringComparison.Ordinal) == true);
        Assert.Contains("Rated:4", kit.Observers.Events);
    }

    [Fact]
    public async Task ARecentlySolvedTicketsRequester_IsAskedOnce_AndAnOldOneIsntAsked()
    {
        // Arrange
        var links = new Mock<ITicketRequesterLinks>();
        links.Setup(value => value.GetRatingLinkAsync(It.IsAny<ContentItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentItem ticket, CancellationToken _) => "https://help.example.com/requests/" + ticket.ContentItemId + "/rate?token=x");
        await using var kit = await TicketKit.CreateAsync(
            configureDesk: desk => desk.RatingRequestDelayHours = 24,
            configureServices: services =>
            {
                services.AddSingleton(links.Object);
                services.AddScoped<TicketRatingRequests>();
            });
        var recent = await SolvedAsync(kit, "dana@example.com", CalendarTestHarness.Now.AddDays(-2));
        var old = await SolvedAsync(kit, "lee@example.com", CalendarTestHarness.Now.AddDays(-30));
        var tooSoon = await SolvedAsync(kit, "cy@example.com", CalendarTestHarness.Now.AddHours(-3));

        // Act
        var first = await kit.RunAsync(services => services.GetRequiredService<TicketRatingRequests>().SendDueAsync(TestContext.Current.CancellationToken));
        var second = await kit.RunAsync(services => services.GetRequiredService<TicketRatingRequests>().SendDueAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        var email = Assert.Single(kit.Sender.Sent);
        Assert.Equal(TicketsConstants.EmailKinds.Rating, email.Email.Kind);
        Assert.Equal("dana@example.com", email.Email.To);
        Assert.Contains("/requests/" + recent + "/rate?token=x", email.Content.Html);
        Assert.NotNull((await kit.GetAsync(recent)).GetOrCreate<TicketPart>().RatingRequestedUtc);
        Assert.Null((await kit.GetAsync(old)).GetOrCreate<TicketPart>().RatingRequestedUtc);
        Assert.Null((await kit.GetAsync(tooSoon)).GetOrCreate<TicketPart>().RatingRequestedUtc);
    }

    private static Task<TicketResult> RateAsync(TicketKit kit, string ticketId, int score, string comment)
        => kit.RunAsync(services => services.GetRequiredService<ITicketService>().RateAsync(ticketId, score, comment, TestContext.Current.CancellationToken));

    // A guest's ticket solved at a given time.
    private static async Task<string> SolvedAsync(TicketKit kit, string email, DateTime solvedUtc)
    {
        var created = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().CreateAsync(new TicketCreateRequest
        {
            Subject = "Printer jams",
            Body = "It jams.",
            DeskId = "support",
            Channel = TicketsConstants.Channels.Agent,
            RequesterContact = new ContactInfo { Name = "Guest", Email = email },
        }, TestContext.Current.CancellationToken));
        var ticketId = created.Ticket.ContentItemId;
        await kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, new TicketUpdate { StatusId = TicketKit.Solved.ItemId }, cancellationToken: TestContext.Current.CancellationToken));
        kit.Sender.Sent.Clear();

        await kit.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var ticket = await session.Query<ContentItem, TicketIndex>(index => index.ContentItemId == ticketId).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            ticket.Alter<TicketPart>(part => part.SolvedUtc = solvedUtc);
            await session.SaveAsync(ticket, cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });

        return ticketId;
    }
}

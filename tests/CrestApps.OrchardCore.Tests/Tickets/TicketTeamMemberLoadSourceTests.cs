using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A team member's ticket load, read from the ticket index on SQLite: the tickets assigned to them that are still being
/// worked on count (solved and closed ones don't), and the latest assignment says whose turn it is.
/// </summary>
public sealed class TicketTeamMemberLoadSourceTests
{
    [Fact]
    public async Task OpenTicketsCount_AndTheLatestAssignmentSaysWhoseTurnItIs()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync(
            services => services.AddScoped<TicketTeamMemberLoadSource>(),
            TicketIndexSchema.CreateAsync,
            [new TicketIndexProvider()]);
        var now = CalendarTestHarness.Now;

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Ticket("t1", "ana", TicketStatusCategory.Open, now.AddHours(-5)));
            await session.SaveAsync(Ticket("t2", "ana", TicketStatusCategory.Pending, now.AddHours(-1)));
            await session.SaveAsync(Ticket("t3", "ana", TicketStatusCategory.Solved, now.AddHours(-9)));
            await session.SaveAsync(Ticket("t4", "ben", TicketStatusCategory.Closed, now.AddHours(-2)));
        });

        // Act
        var loads = await harness.RunAsync(services => services.GetRequiredService<TicketTeamMemberLoadSource>().GetLoadAsync(["ana", "ben", "cy"]), commit: false);

        // Assert
        Assert.Equal(2, loads["ana"].Open);
        Assert.Equal(now.AddHours(-1), loads["ana"].LastAssignedUtc);
        Assert.Equal(0, loads["ben"].Open);
        Assert.Equal(now.AddHours(-2), loads["ben"].LastAssignedUtc);
        Assert.Equal(0, loads["cy"].Open);
        Assert.Null(loads["cy"].LastAssignedUtc);
    }

    private static ContentItem Ticket(string id, string assignee, TicketStatusCategory category, DateTime assignedUtc)
    {
        var item = new ContentItem { ContentItemId = id, ContentType = TicketsConstants.DefaultContentType, Latest = true, Published = true, DisplayText = id };
        item.Apply(new TicketPart { Number = id, DeskId = "support", StatusCategory = category, AssigneeId = assignee, AssignedUtc = assignedUtc });

        return item;
    }
}

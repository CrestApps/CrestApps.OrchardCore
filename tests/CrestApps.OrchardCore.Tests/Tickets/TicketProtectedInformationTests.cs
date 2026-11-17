using System.Text.Json;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Api;
using CrestApps.OrchardCore.Tickets.Core.Handlers;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The protected information of tickets: stored encrypted in documents of its own, never on the ticket, so the ticket,
/// its API model (which the AI tools, the API and the webhooks send) and its timeline never carry a value; every change
/// and reveal is an internal event with the labels only; it follows a merge, goes with a deleted ticket, and is deleted
/// the desk's number of days after the ticket closed.
/// </summary>
public sealed class TicketProtectedInformationTests
{
    private const string Secret = "vpn-user / S3cret!pass";

    [Fact]
    public async Task AValue_IsStoredEncrypted_AndNothingThatReadsOrSendsTheTicketCarriesIt()
    {
        // Arrange
        await using var kit = await CreateKitAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var added = await kit.RunAsync(services => services.GetRequiredService<ITicketProtectedInformation>().AddAsync(ticketId, "VPN login", Secret, TicketActor.User("ana", "Ana"), fromRequester: true, TestContext.Current.CancellationToken));

        // Assert: the stored document holds a cipher, which reads back to the value.
        Assert.NotNull(added);
        var stored = await kit.ReadAsync(services => services.GetRequiredService<ISession>().Query<TicketProtectedItemDocument>().FirstOrDefaultAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain("S3cret", stored.ProtectedValue, StringComparison.Ordinal);
        var values = await kit.ReadAsync(services => services.GetRequiredService<ITicketProtectedInformation>().ReadValuesAsync(ticketId, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(("VPN login", Secret), (Assert.Single(values).Label, values[0].Value));

        // The ticket, its API model and its timeline: the label at most, never the value.
        var ticket = await kit.GetAsync(ticketId);
        Assert.DoesNotContain("S3cret", JsonSerializer.Serialize(ticket.Content), StringComparison.Ordinal);
        Assert.DoesNotContain("S3cret", JsonSerializer.Serialize(TicketApiModel.From(ticket)), StringComparison.Ordinal);
        var entries = await kit.EntriesAsync(ticketId);
        Assert.All(entries, entry => Assert.DoesNotContain("S3cret", entry.Body ?? string.Empty, StringComparison.Ordinal));
        Assert.All(entries, entry => Assert.DoesNotContain("S3cret", JsonSerializer.Serialize(TicketApiEntry.From(entry, [], _ => null)), StringComparison.Ordinal));
        var note = Assert.Single(entries, entry => entry.Body?.Contains("VPN login", StringComparison.Ordinal) == true);
        Assert.Equal((TicketsConstants.EntryKinds.Event, global::CrestApps.OrchardCore.Timeline.Models.TimelineVisibility.Internal), (note.Kind, note.Visibility));
    }

    [Fact]
    public async Task TheLists_KnowWhichTicketsHoldProtectedInformation_AndRemovingAndShowingAreNoted()
    {
        // Arrange
        await using var kit = await CreateKitAsync();
        var withItems = (await kit.CreateAsync()).Ticket.ContentItemId;
        var without = (await kit.CreateAsync("Second")).Ticket.ContentItemId;
        var item = await kit.RunAsync(services => services.GetRequiredService<ITicketProtectedInformation>().AddAsync(withItems, "VPN login", Secret, TicketActor.User("ben", "Ben"), cancellationToken: TestContext.Current.CancellationToken));

        // Act
        var found = await kit.ReadAsync(services => services.GetRequiredService<ITicketProtectedInformation>().FindTicketsWithItemsAsync([withItems, without], cancellationToken: TestContext.Current.CancellationToken));
        await kit.RunAsync(async services =>
        {
            var information = services.GetRequiredService<ITicketProtectedInformation>();
            await information.RecordRevealAsync(withItems, TicketActor.User("ben", "Ben"), succeeded: false, cancellationToken: TestContext.Current.CancellationToken);
            await information.RecordRevealAsync(withItems, TicketActor.User("ben", "Ben"), succeeded: true, cancellationToken: TestContext.Current.CancellationToken);

            return await information.RemoveAsync(withItems, item.ItemId, TicketActor.User("ben", "Ben"), cancellationToken: TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Equal([withItems], found);
        var bodies = (await kit.EntriesAsync(withItems)).Select(entry => entry.Body).ToList();
        Assert.Contains(bodies, body => body?.Contains("wrong password", StringComparison.Ordinal) == true);
        Assert.Contains(bodies, body => body?.Contains("shown: VPN login", StringComparison.Ordinal) == true);
        Assert.Contains(bodies, body => body?.Contains("removed: VPN login", StringComparison.Ordinal) == true);
        Assert.Empty(await kit.ReadAsync(services => services.GetRequiredService<ITicketProtectedInformation>().ListAsync(withItems, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task TheRequester_ReachesOnlyWhatTheyShared_NeverWhatAgentsKeepOnTheirTicket()
    {
        // Arrange: the requester shared their VPN login; an agent keeps the server's admin password on the same ticket, and
        // another ticket holds only an agent's item.
        await using var kit = await CreateKitAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        var agentsOnly = (await kit.CreateAsync("Second")).Ticket.ContentItemId;
        var (shared, kept) = await kit.RunAsync(async services =>
        {
            var information = services.GetRequiredService<ITicketProtectedInformation>();
            var fromRequester = await information.AddAsync(ticketId, "VPN login", Secret, TicketActor.User("ana", "Ana"), fromRequester: true, TestContext.Current.CancellationToken);
            var fromAgent = await information.AddAsync(ticketId, "Server admin", "root / Adm1n!", TicketActor.User("ben", "Ben"), cancellationToken: TestContext.Current.CancellationToken);
            await information.AddAsync(agentsOnly, "Router admin", "admin / R0uter!", TicketActor.User("ben", "Ben"), cancellationToken: TestContext.Current.CancellationToken);

            return (fromRequester, fromAgent);
        });

        // Act
        var (values, found, removedKept, removedShared) = await kit.RunAsync(async services =>
        {
            var information = services.GetRequiredService<ITicketProtectedInformation>();
            var read = await information.ReadValuesAsync(ticketId, requesterSharedOnly: true, TestContext.Current.CancellationToken);
            var withShared = await information.FindTicketsWithItemsAsync([ticketId, agentsOnly], requesterSharedOnly: true, TestContext.Current.CancellationToken);
            await information.RecordRevealAsync(ticketId, TicketActor.User("ana", "Ana"), succeeded: true, requesterSharedOnly: true, TestContext.Current.CancellationToken);
            var keptRemoved = await information.RemoveAsync(ticketId, kept.ItemId, TicketActor.User("ana", "Ana"), requesterSharedOnly: true, TestContext.Current.CancellationToken);
            var sharedRemoved = await information.RemoveAsync(ticketId, shared.ItemId, TicketActor.User("ana", "Ana"), requesterSharedOnly: true, TestContext.Current.CancellationToken);

            return (read, withShared, keptRemoved, sharedRemoved);
        });

        // Assert
        Assert.Equal(("VPN login", Secret), (Assert.Single(values).Label, values[0].Value));
        Assert.Equal([ticketId], found);
        Assert.False(removedKept);
        Assert.True(removedShared);
        Assert.Equal("Server admin", Assert.Single(await kit.ReadAsync(services => services.GetRequiredService<ITicketProtectedInformation>().ListAsync(ticketId, TestContext.Current.CancellationToken))).Label);
        var shown = Assert.Single(await kit.EntriesAsync(ticketId), entry => entry.Body?.Contains("was shown", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("Server admin", shown.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void APageShowingTheValues_IsKeptOutOfEveryCache()
    {
        // Arrange
        var response = new Microsoft.AspNetCore.Http.DefaultHttpContext().Response;

        // Act
        TicketProtectedReveals.PreventCaching(response);

        // Assert
        Assert.Contains("no-store", response.Headers.CacheControl.ToString(), StringComparison.Ordinal);
        Assert.Equal("no-cache", response.Headers.Pragma.ToString());
    }

    [Fact]
    public async Task AnEmptyOrTooLongValue_IsRefused()
    {
        // Arrange
        await using var kit = await CreateKitAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var empty = await kit.RunAsync(services => services.GetRequiredService<ITicketProtectedInformation>().AddAsync(ticketId, "VPN login", " ", TicketActor.Site, cancellationToken: TestContext.Current.CancellationToken));
        var tooLong = await kit.RunAsync(services => services.GetRequiredService<ITicketProtectedInformation>().AddAsync(ticketId, "VPN login", new string('x', ITicketProtectedInformation.MaxValueLength + 1), TicketActor.Site, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(empty);
        Assert.Null(tooLong);
    }

    [Fact]
    public async Task AMergedTicketsProtectedInformation_MovesToTheTicketItIsMergedInto()
    {
        // Arrange
        await using var kit = await CreateKitAsync();
        var source = (await kit.CreateAsync()).Ticket.ContentItemId;
        var target = (await kit.CreateAsync("Target")).Ticket.ContentItemId;
        await kit.RunAsync(services => services.GetRequiredService<ITicketProtectedInformation>().AddAsync(source, "VPN login", Secret, TicketActor.Site, cancellationToken: TestContext.Current.CancellationToken));

        // Act
        var result = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().MergeAsync(source, target, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        Assert.Empty(await kit.ReadAsync(services => services.GetRequiredService<ITicketProtectedInformation>().ListAsync(source, TestContext.Current.CancellationToken)));
        Assert.Equal("VPN login", Assert.Single(await kit.ReadAsync(services => services.GetRequiredService<ITicketProtectedInformation>().ListAsync(target, TestContext.Current.CancellationToken))).Label);
    }

    [Fact]
    public async Task ADeletedTicketsProtectedInformation_GoesWithIt()
    {
        // Arrange
        await using var kit = await CreateKitAsync();
        var ticket = (await kit.CreateAsync()).Ticket;
        await kit.RunAsync(services => services.GetRequiredService<ITicketProtectedInformation>().AddAsync(ticket.ContentItemId, "VPN login", Secret, TicketActor.Site, cancellationToken: TestContext.Current.CancellationToken));

        // Act: the removal's handler, as Orchard Core runs it when the ticket is deleted.
        await kit.RunAsync(async services =>
        {
            var cleanup = new TicketContentCleanup(services.GetRequiredService<ResumableSteps>(), services.GetRequiredService<ISession>());
            await cleanup.RemovedAsync(new RemoveContentContext(ticket, true, RemoveContentReason.Deletion));

            return true;
        });

        // Assert
        Assert.Empty(await kit.ReadAsync(services => services.GetRequiredService<ITicketProtectedInformation>().ListAsync(ticket.ContentItemId, TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData(3, 4, true)]
    [InlineData(3, 1, false)]
    [InlineData(0, 400, false)]
    public async Task ProtectedInformation_IsDeletedTheDesksNumberOfDays_AfterTheTicketClosed(int keepDays, int closedDaysAgo, bool deleted)
    {
        // Arrange
        await using var kit = await CreateKitAsync(desk => desk.DeleteProtectedInformationDays = keepDays);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        await kit.RunAsync(async services =>
        {
            await services.GetRequiredService<ITicketProtectedInformation>().AddAsync(ticketId, "VPN login", Secret, TicketActor.Site, cancellationToken: TestContext.Current.CancellationToken);

            // Closed some days ago.
            var session = services.GetRequiredService<ISession>();
            var ticket = await session.Query<ContentItem, TicketIndex>(index => index.ContentItemId == ticketId).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            ticket.Alter<TicketPart>(part =>
            {
                part.StatusId = TicketKit.Closed.ItemId;
                part.StatusCategory = TicketStatusCategory.Closed;
                part.ClosedUtc = Calendars.CalendarTestHarness.Now.AddDays(-closedDaysAgo);
            });
            await session.SaveAsync(ticket, cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });

        // Act
        var count = await kit.RunAsync(services => services.GetRequiredService<TicketProtectedInformationCleaner>().DeleteDueAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(deleted ? 1 : 0, count);
        Assert.Equal(deleted, (await kit.ReadAsync(services => services.GetRequiredService<ITicketProtectedInformation>().ListAsync(ticketId, TestContext.Current.CancellationToken))).Count == 0);
    }

    private static Task<TicketKit> CreateKitAsync(Action<TicketDesk> configureDesk = null)
        => TicketKit.CreateAsync(configureDesk: configureDesk, configureServices: services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddScoped<ITicketProtectedInformation, DefaultTicketProtectedInformation>();
            services.AddScoped<TicketProtectedInformationCleaner>();
        });
}

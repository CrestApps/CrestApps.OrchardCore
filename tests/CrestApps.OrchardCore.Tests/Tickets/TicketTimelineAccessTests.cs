using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A ticket's timeline: whoever may view the ticket reads its replies and notes; its requester reads only its public
/// entries; nobody else reads anything, a missing
/// ticket has no timeline, and nothing is added through the generic timeline composer, because replies and notes go
/// through the ticket service, which keeps the counters, the service levels and the life cycle.
/// </summary>
public sealed class TicketTimelineAccessTests
{
    private static readonly ClaimsPrincipal _agent = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "agent-1")], "test"));
    private static readonly ClaimsPrincipal _stranger = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "stranger")], "test"));
    private static readonly ClaimsPrincipal _requester = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "requester-1")], "test"));

    [Fact]
    public async Task TheRequester_ReadsOnlyThePublicEntriesOfTheirOwnTicket()
    {
        // Arrange
        var access = Create();
        var subject = new RecordRef(TicketsConstants.RecordKind, "t1");

        // Act
        var requesterPublic = await access.CanViewAsync(_requester, subject, TimelineVisibility.Public, TestContext.Current.CancellationToken);
        var requesterInternal = await access.CanViewAsync(_requester, subject, TimelineVisibility.Internal, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(requesterPublic);
        Assert.False(requesterInternal);
    }

    [Fact]
    public async Task WhoeverMayViewTheTicket_ReadsItsRepliesAndNotes_AndNobodyElseReadsAnything()
    {
        // Arrange
        var access = Create();
        var subject = new RecordRef(TicketsConstants.RecordKind, "t1");

        // Act
        var agentPublic = await access.CanViewAsync(_agent, subject, TimelineVisibility.Public, TestContext.Current.CancellationToken);
        var agentInternal = await access.CanViewAsync(_agent, subject, TimelineVisibility.Internal, TestContext.Current.CancellationToken);
        var strangerPublic = await access.CanViewAsync(_stranger, subject, TimelineVisibility.Public, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(agentPublic);
        Assert.True(agentInternal);
        Assert.False(strangerPublic);
    }

    [Fact]
    public async Task AMissingTicket_HasNoTimeline_AndNothingIsAddedThroughTheGenericComposer()
    {
        // Arrange
        var access = Create();

        // Act
        var missing = await access.CanViewAsync(_agent, new RecordRef(TicketsConstants.RecordKind, "missing"), TimelineVisibility.Public, TestContext.Current.CancellationToken);
        var note = await access.CanAddAsync(_agent, new RecordRef(TicketsConstants.RecordKind, "t1"), "Note", TimelineVisibility.Internal, TestContext.Current.CancellationToken);
        var comment = await access.CanAddAsync(_agent, new RecordRef(TicketsConstants.RecordKind, "t1"), "Comment", TimelineVisibility.Public, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TicketsConstants.RecordKind, access.SubjectKind);
        Assert.False(missing);
        Assert.False(note);
        Assert.False(comment);
    }

    private static TicketTimelineAccess Create()
    {
        var ticket = new ContentItem { ContentItemId = "t1", ContentType = TicketsConstants.DefaultContentType };
        ticket.Apply(new TicketPart { DeskId = "support", Requester = new CrestApps.OrchardCore.Parties.Models.PartyRef(CrestApps.OrchardCore.Parties.Models.PartyKinds.User, "requester-1") });
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("t1", It.IsAny<VersionOptions>())).ReturnsAsync(ticket);

        var access = new Mock<ITicketAccessService>();
        access.Setup(service => service.CanAsync(_agent, ticket, TicketOperation.ViewAssigned, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return new TicketTimelineAccess(contentManager.Object, access.Object, Mock.Of<global::YesSql.ISession>());
    }
}

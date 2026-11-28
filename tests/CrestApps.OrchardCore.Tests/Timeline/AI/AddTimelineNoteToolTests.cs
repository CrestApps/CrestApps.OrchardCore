using System.Text.Json;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.AI;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Moq;
using TimelineAIToolsStartup = CrestApps.OrchardCore.Timeline.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Timeline.AI;

/// <summary>
/// <c>addTimelineNote</c> adds an internal note as the timeline card's Add note button does: only for a signed-in person
/// the record's access provider lets add a note, under their name, committed at once, and it says whom the note
/// mentions. It never asks for approval.
/// </summary>
public sealed class AddTimelineNoteToolTests
{
    private static readonly RecordRef _workOrder = new("WorkOrder", "wo-1");

    [Fact]
    public async Task ACallerWhoIsNotSignedIn_IsRefused_AndNothingIsAdded()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit { User = TimelineAIToolTestKit.Anonymous() };

        // Act
        var result = await kit.InvokeAsync(new AddTimelineNoteTool(), new() { ["recordKind"] = "WorkOrder", ["recordId"] = "wo-1", ["text"] = "Hi" });

        // Assert
        Assert.Equal(TimelineAIToolBase.SignInRequired, result);
        Assert.Empty(kit.Timeline.Entries);
    }

    [Fact]
    public async Task ACallerTheProviderDoesNotLetAddANote_IsRefused_AndNothingIsAddedOrCommitted()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit();
        kit.Access.Add = (_, _, _) => false;

        // Act
        var result = await kit.InvokeAsync(new AddTimelineNoteTool(), new() { ["recordKind"] = "Ticket", ["recordId"] = "t-1", ["text"] = "Hi" });

        // Assert
        Assert.Equal(AddTimelineNoteTool.NotAllowed, result);
        Assert.Contains("addTicketReply", result, StringComparison.Ordinal);
        Assert.Empty(kit.Timeline.Entries);
        kit.Session.Verify(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ANote_IsAddedAsAnInternalNoteUnderTheCallersName_AndCommitted()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit();
        string askedKind = null;
        TimelineVisibility? askedVisibility = null;
        kit.Access.Add = (_, kind, visibility) =>
        {
            askedKind = kind;
            askedVisibility = visibility;

            return true;
        };

        // Act
        var result = JsonElement.Parse(await kit.InvokeAsync(new AddTimelineNoteTool(), new() { ["recordKind"] = "workorder", ["recordId"] = "wo-1", ["text"] = "  Parts arrive Tuesday, @bob please call them.  " }));

        // Assert
        var entry = Assert.Single(kit.Timeline.Entries);
        Assert.Equal(_workOrder, entry.Subject);
        Assert.Equal(TimelineConstants.Kinds.Note, entry.Kind);
        Assert.Equal(TimelineVisibility.Internal, entry.Visibility);
        Assert.Equal(TimelineAIToolTestKit.Ana, entry.Author);
        Assert.Equal("ana", entry.AuthorName);
        Assert.Equal("Parts arrive Tuesday, @bob please call them.", entry.Body);
        Assert.Equal(TimelineConstants.Kinds.Note, askedKind);
        Assert.Equal(TimelineVisibility.Internal, askedVisibility);
        kit.Session.Verify(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        Assert.True(result.GetProperty("added").GetBoolean());
        Assert.Equal(entry.ItemId, result.GetProperty("entryId").GetString());
        Assert.Equal("internal", result.GetProperty("visibility").GetString());
        Assert.Equal(["bob"], result.GetProperty("mentioned").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal("Acme Corp", result.GetProperty("record").GetProperty("title").GetString());
    }

    [Fact]
    public async Task WhatTheTimelineRefuses_IsSaid_AndNothingIsCommitted()
    {
        // Arrange
        var timeline = new Mock<ITimelineService>();
        timeline.Setup(service => service.AddAsync(It.IsAny<TimelineEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimelineResult { Errors = ["The record is closed."] });
        var kit = new TimelineAIToolTestKit { TimelineService = timeline.Object };

        // Act
        var result = await kit.InvokeAsync(new AddTimelineNoteTool(), new() { ["recordKind"] = "WorkOrder", ["recordId"] = "wo-1", ["text"] = "Hi" });

        // Assert
        Assert.Equal("Nothing was added: The record is closed.", result);
        kit.Session.Verify(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnEmptyOrTooLongNote_IsRefused()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit();

        // Act
        var empty = await kit.InvokeAsync(new AddTimelineNoteTool(), new() { ["recordKind"] = "WorkOrder", ["recordId"] = "wo-1", ["text"] = "   " });
        var tooLong = await kit.InvokeAsync(new AddTimelineNoteTool(), new() { ["recordKind"] = "WorkOrder", ["recordId"] = "wo-1", ["text"] = new string('x', TimelineConstants.MaxBodyLength + 1) });

        // Assert
        Assert.Equal("Nothing was added: write the note's text first.", empty);
        Assert.StartsWith("Nothing was added: a note is at most", tooLong, StringComparison.Ordinal);
        Assert.Empty(kit.Timeline.Entries);
    }

    [Fact]
    public void TheTool_IsRegisteredWithItsTexts_AndAsksNoApproval()
    {
        // Act
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(TimelineAIToolsStartup));

        // Assert
        Assert.Equal(2, tools.Count);
        Assert.True(tools.TryGetValue(AddTimelineNoteTool.TheName, out var tool));
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Timeline", tool.Category);
        Assert.False(tool.RequiresApproval);
    }
}

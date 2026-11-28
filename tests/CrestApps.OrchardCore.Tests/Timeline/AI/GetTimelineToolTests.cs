using System.Text.Json;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.AI;
using CrestApps.OrchardCore.Timeline.Models;
using Moq;
using TimelineAIToolsStartup = CrestApps.OrchardCore.Timeline.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Timeline.AI;

/// <summary>
/// <c>getTimeline</c> reads one record's timeline as the timeline card does: only for a signed-in person who may read
/// it, newest first and a page at a time, with author names, local times, changes and file names, a redacted entry's
/// text and files left out, and what people wrote marked as data. It never asks for approval.
/// </summary>
public sealed class GetTimelineToolTests
{
    private static readonly RecordRef _acme = new("Content", "acme");

    [Fact]
    public async Task ACallerWhoIsNotSignedIn_IsRefused()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit { User = TimelineAIToolTestKit.Anonymous() };

        // Act
        var result = await kit.InvokeAsync(new GetTimelineTool(), new() { ["recordKind"] = "Content", ["recordId"] = "acme" });

        // Assert
        Assert.Equal(TimelineAIToolBase.SignInRequired, result);
    }

    [Fact]
    public async Task ACallerWhoMayReadNeitherKindOfEntry_IsRefused_AsForARecordThatDoesNotExist()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit();
        kit.Access.View = (_, _) => false;
        kit.Timeline.Entries.Add(Entry("n1", TimelineConstants.Kinds.Note, TimelineVisibility.Internal, 9, entry => entry.Body = "Secret"));

        // Act
        var result = await kit.InvokeAsync(new GetTimelineTool(), new() { ["recordKind"] = "Content", ["recordId"] = "acme" });

        // Assert
        Assert.Equal(GetTimelineTool.NotAllowed, result);
    }

    [Fact]
    public async Task AKindWithNoTimeline_IsRefused_WithTheKindsThatHaveOne()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit();

        // Act
        var result = await kit.InvokeAsync(new GetTimelineTool(), new() { ["recordKind"] = "Invoice", ["recordId"] = "1" });

        // Assert
        Assert.StartsWith("Records of the kind 'Invoice' have no timeline.", result, StringComparison.Ordinal);
        Assert.Contains("Content, Order, Ticket, WorkOrder", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTimeline_IsReadNewestFirst_WithNamesLocalTimesChangesAndFiles_AndARedactedEntryHidesItsTextAndFiles()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit();
        kit.Timeline.Entries.Add(Entry("n1", TimelineConstants.Kinds.Note, TimelineVisibility.Internal, 9, entry =>
        {
            entry.Author = TimelineAIToolTestKit.Ana;
            entry.AuthorName = "ana";
            entry.Body = "Called them back.";
        }));
        kit.Timeline.Entries.Add(Entry("c1", TimelineConstants.Kinds.Comment, TimelineVisibility.Public, 10, entry =>
        {
            entry.AuthorName = "Guest";
            entry.Body = new string('x', 2_500);
            entry.AttachmentIds = ["quote"];
            entry.ExternalId = "message-id-1";
            entry.Properties[TimelineConstants.Properties.Notice] = "The e-mail bounced.";
            entry.Properties["DeliveryStatus"] = "secret-status";
        }));
        kit.Timeline.Entries.Add(Entry("e1", TimelineConstants.Kinds.Event, TimelineVisibility.Internal, 11, entry =>
            entry.Changes = [new TimelineFieldChange { Field = "Status", From = "Open", To = "Closed", FromValue = "s1", ToValue = "s2" }]));
        kit.Timeline.Entries.Add(Entry("r1", TimelineConstants.Kinds.Note, TimelineVisibility.Internal, 12, entry =>
        {
            entry.Body = "Card number 4111";
            entry.AttachmentIds = ["card"];
            entry.RedactedUtc = TimelineAIToolTestKit.Now;
            entry.RedactedBy = TimelineAIToolTestKit.Ana;
        }));

        // Act
        var json = await kit.InvokeAsync(new GetTimelineTool(), new() { ["recordKind"] = "content", ["recordId"] = "acme" });
        var result = JsonElement.Parse(json);
        var items = result.GetProperty("items");

        // Assert
        Assert.Equal(["r1", "e1", "c1", "n1"], items.EnumerateArray().Select(item => item.GetProperty("id").GetString()));
        Assert.Equal("Acme Corp", result.GetProperty("record").GetProperty("title").GetString());
        Assert.Equal("Content", result.GetProperty("record").GetProperty("kind").GetString());
        Assert.Equal(4, result.GetProperty("total").GetInt32());
        Assert.False(result.GetProperty("truncated").GetBoolean());
        Assert.Equal(GetTimelineTool.UntrustedNote, result.GetProperty("note").GetString());

        Assert.True(items[0].GetProperty("redacted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("text").ValueKind);
        Assert.Empty(items[0].GetProperty("files").EnumerateArray());

        Assert.Equal("Open", items[1].GetProperty("changes")[0].GetProperty("from").GetString());
        Assert.Equal("Closed", items[1].GetProperty("changes")[0].GetProperty("to").GetString());

        Assert.Equal(GetTimelineTool.MaxTextLength, items[2].GetProperty("text").GetString().Length);
        Assert.True(items[2].GetProperty("textTruncated").GetBoolean());
        Assert.Equal(2_500, items[2].GetProperty("textLength").GetInt32());
        Assert.Equal(["quote.pdf"], items[2].GetProperty("files").EnumerateArray().Select(file => file.GetString()));
        Assert.Equal("The e-mail bounced.", items[2].GetProperty("notice").GetString());
        Assert.Equal("public", items[2].GetProperty("visibility").GetString());
        Assert.Equal("Guest", items[2].GetProperty("author").GetString());

        Assert.Equal("Ana Lopez", items[3].GetProperty("author").GetString());
        Assert.Equal("internal", items[3].GetProperty("visibility").GetString());
        Assert.Equal("2026-11-16T09:00:00+00:00", items[3].GetProperty("when").GetString());

        // Only allow-listed fields: no external id, party id, other properties or redacted text.
        Assert.DoesNotContain("message-id-1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-status", json, StringComparison.Ordinal);
        Assert.DoesNotContain("u-ana", json, StringComparison.Ordinal);
        Assert.DoesNotContain("4111", json, StringComparison.Ordinal);
        Assert.DoesNotContain("card.pdf", json, StringComparison.Ordinal);
        Assert.False(items[1].GetProperty("changes")[0].TryGetProperty("toValue", out _));
    }

    [Fact]
    public async Task APage_IsCappedAtFifty_AndSaysWhenMoreFollow()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit();

        for (var hour = 1; hour <= 3; hour++)
        {
            kit.Timeline.Entries.Add(Entry("n" + hour, TimelineConstants.Kinds.Note, TimelineVisibility.Internal, hour, entry => entry.Body = "Note " + hour));
        }

        // Act
        var capped = JsonElement.Parse(await kit.InvokeAsync(new GetTimelineTool(), new() { ["recordKind"] = "Content", ["recordId"] = "acme", ["pageSize"] = 500 }));
        var second = JsonElement.Parse(await kit.InvokeAsync(new GetTimelineTool(), new() { ["recordKind"] = "Content", ["recordId"] = "acme", ["page"] = 2, ["pageSize"] = 1 }));

        // Assert
        Assert.Equal(TimelineAIToolBase.MaxPageSize, capped.GetProperty("pageSize").GetInt32());
        Assert.Equal("n2", Assert.Single(second.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.True(second.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task ACallerWhoMayReadOnlyInternalEntries_DoesNotSeePublicOnes_AndGetsNoTotal()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit();
        kit.Access.View = (_, visibility) => visibility == TimelineVisibility.Internal;
        kit.Timeline.Entries.Add(Entry("n1", TimelineConstants.Kinds.Note, TimelineVisibility.Internal, 9, entry => entry.Body = "Internal"));
        kit.Timeline.Entries.Add(Entry("c1", TimelineConstants.Kinds.Comment, TimelineVisibility.Public, 10, entry => entry.Body = "Public"));

        // Act
        var result = JsonElement.Parse(await kit.InvokeAsync(new GetTimelineTool(), new() { ["recordKind"] = "Content", ["recordId"] = "acme" }));

        // Assert
        Assert.Equal("n1", Assert.Single(result.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("total").ValueKind);
    }

    [Fact]
    public async Task ALookup_UsesTheOnlyMatch_AndReturnsCandidatesWhenTwoShareATitle()
    {
        // Arrange
        var kit = new TimelineAIToolTestKit();
        kit.Timeline.Entries.Add(Entry("n1", TimelineConstants.Kinds.Note, TimelineVisibility.Internal, 9, entry => entry.Body = "Hello"));
        kit.Directory.Setup(directory => directory.SearchAsync("Content", It.Is<RecordSearch>(search => search.Text == "Acme"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecordSummary> { new() { Id = "acme", Title = "Acme Corp" } });
        kit.Directory.Setup(directory => directory.SearchAsync("Content", It.Is<RecordSearch>(search => search.Text == "Jane Doe"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecordSummary> { new() { Id = "jane-1", Title = "Jane Doe" }, new() { Id = "jane-2", Title = "Jane Doe" } });

        // Act
        var found = JsonElement.Parse(await kit.InvokeAsync(new GetTimelineTool(), new() { ["recordKind"] = "Content", ["lookup"] = "Acme" }));
        var ambiguous = JsonElement.Parse(await kit.InvokeAsync(new GetTimelineTool(), new() { ["recordKind"] = "Content", ["lookup"] = "Jane Doe" }));

        // Assert
        Assert.Equal("n1", Assert.Single(found.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(["jane-1", "jane-2"], ambiguous.GetProperty("candidates").EnumerateArray().Select(candidate => candidate.GetProperty("id").GetString()));
        Assert.False(ambiguous.TryGetProperty("items", out _));
    }

    [Fact]
    public void TheTool_IsRegisteredWithItsTexts_AndAsksNoApproval()
    {
        // Act
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(TimelineAIToolsStartup));

        // Assert
        Assert.True(tools.TryGetValue(GetTimelineTool.TheName, out var tool));
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Timeline", tool.Category);
        Assert.False(tool.RequiresApproval);
    }

    internal static TimelineEntry Entry(string id, string kind, TimelineVisibility visibility, int hour, Action<TimelineEntry> configure = null)
    {
        var entry = new TimelineEntry
        {
            ItemId = id,
            Subject = _acme,
            Kind = kind,
            Visibility = visibility,
            CreatedUtc = new DateTime(2026, 11, 16, hour, 0, 0, DateTimeKind.Utc),
        };

        configure?.Invoke(entry);

        return entry;
    }
}

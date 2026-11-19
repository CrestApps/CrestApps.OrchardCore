using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core;
using CrestApps.OrchardCore.Timeline.Core.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A ticket's files in one place, read from the timeline's file index (real YesSql and SQLite), not from the entries:
/// newest first, a page at a time with the total, the files of internal notes only of the tickets whose notes the
/// reader may read (so a merged ticket's files follow their own entries' visibility), and never the files of a
/// redacted entry.
/// </summary>
public sealed class TicketFilesTests
{
    private static readonly RecordRef _t1 = new(TicketsConstants.RecordKind, "t1");
    private static readonly RecordRef _t2 = new(TicketsConstants.RecordKind, "t2");

    [Fact]
    public async Task ATicketsFiles_FollowTheirEntriesVisibility_AndRedactedEntriesFilesNeverShow()
    {
        // Arrange: t1 has a public reply (a), an internal note (b) and a redacted reply (c); t2, merged into t1, has an
        // internal note (d) and a public reply (e); t9 (another ticket) has a file too.
        await using var harness = await TicketReportDataSourceTests.CreateAsync(services => services.AddTimelineCore());
        await SeedAsync(harness);

        // Act
        var agent = await ListAsync(harness, new TimelineFileQuery { Subjects = [_t1, _t2], InternalSubjects = [_t1] });
        var requester = await ListAsync(harness, new TimelineFileQuery { Subjects = [_t1, _t2] });
        var firstPage = await ListAsync(harness, new TimelineFileQuery { Subjects = [_t1, _t2], InternalSubjects = [_t1, _t2], Take = 2 });

        // Assert
        Assert.Equal(["e", "b", "a"], agent.Files.Select(file => file.AttachmentId));
        Assert.Equal(3, agent.Total);
        Assert.Equal(["e", "a"], requester.Files.Select(file => file.AttachmentId));
        Assert.Equal(["e", "d"], firstPage.Files.Select(file => file.AttachmentId));
        Assert.Equal(4, firstPage.Total);
        var note = Assert.Single(agent.Files, file => file.AttachmentId == "b");
        Assert.Equal(("n1", _t1, TimelineVisibility.Internal, "Ana"), (note.EntryId, note.Subject, note.Visibility, note.AuthorName));
    }

    [Fact]
    public async Task TheFileIndexStep_IndexesTheEntriesMadeBefore_AndFinishes()
    {
        // Arrange
        await using var harness = await TicketReportDataSourceTests.CreateAsync(services => services.AddTimelineCore());
        await SeedAsync(harness);

        // Act
        var done = await harness.RunAsync(services => ActivatorUtilities.CreateInstance<TimelineFileIndexStep>(services).RunAsync(new ResumableStepContext()));
        var files = await ListAsync(harness, new TimelineFileQuery { Subjects = [_t1], InternalSubjects = [_t1] });

        // Assert
        Assert.True(done);
        Assert.Equal(["b", "a"], files.Files.Select(file => file.AttachmentId));
    }

    private static Task<TimelineFilePage> ListAsync(CalendarTestHarness harness, TimelineFileQuery query)
        => harness.RunAsync(services => services.GetRequiredService<ITimelineFileService>().ListAsync(query), commit: false);

    private static Task<bool> SeedAsync(CalendarTestHarness harness)
        => harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var now = CalendarTestHarness.Now;

            await session.SaveAsync(Entry("r1", _t1, TimelineVisibility.Public, now.AddHours(-5), "a"), false, TimelineConstants.CollectionName);
            await session.SaveAsync(Entry("n1", _t1, TimelineVisibility.Internal, now.AddHours(-4), "b"), false, TimelineConstants.CollectionName);
            var redacted = Entry("r2", _t1, TimelineVisibility.Public, now.AddHours(-3), "c");
            redacted.RedactedUtc = now.AddHours(-1);
            await session.SaveAsync(redacted, false, TimelineConstants.CollectionName);
            await session.SaveAsync(Entry("n2", _t2, TimelineVisibility.Internal, now.AddHours(-2), "d"), false, TimelineConstants.CollectionName);
            await session.SaveAsync(Entry("r3", _t2, TimelineVisibility.Public, now.AddHours(-1), "e"), false, TimelineConstants.CollectionName);
            await session.SaveAsync(Entry("r9", new RecordRef(TicketsConstants.RecordKind, "t9"), TimelineVisibility.Public, now, "z"), false, TimelineConstants.CollectionName);

            return true;
        });

    private static TimelineEntry Entry(string id, RecordRef subject, TimelineVisibility visibility, DateTime createdUtc, string attachmentId)
        => new()
        {
            ItemId = id,
            Subject = subject,
            Kind = visibility == TimelineVisibility.Internal ? TicketsConstants.EntryKinds.Note : TicketsConstants.EntryKinds.Reply,
            Visibility = visibility,
            Author = new PartyRef(PartyKinds.User, "ana"),
            AuthorName = "Ana",
            Body = "Text",
            AttachmentIds = [attachmentId],
            CreatedUtc = createdUtc,
        };
}

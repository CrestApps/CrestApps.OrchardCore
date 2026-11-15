using System.Security.Claims;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Reports;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DataSources;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Timeline;

/// <summary>
/// The TimelineEntries data set, read from SQLite, shows a reader only the entries the record's timeline access lets
/// them see: a record they can't see gives no rows, and an internal note is checked apart from the public comments.
/// </summary>
public sealed class TimelineReportDataSetTests
{
    [Fact]
    public async Task OnlyEntriesTheReaderMaySee_AreRead()
    {
        // Arrange
        // The real access service, asking a provider that answers one record at a time (the default batched form).
        ITimelineAccessService access = new global::CrestApps.OrchardCore.Timeline.Core.Services.DefaultTimelineAccessService([new OnlyPublicEntriesOfA()]);

        await using var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(access);
                new DataSourcesStartup().ConfigureServices(services);
            },
            TimelineIndexSchema.CreateAsync,
            [new TimelineEntryIndexProvider()],
            [TimelineConstants.CollectionName]);

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Entry("e1", "a", TimelineVisibility.Public), false, TimelineConstants.CollectionName);
            await session.SaveAsync(Entry("e2", "a", TimelineVisibility.Internal), false, TimelineConstants.CollectionName);
            await session.SaveAsync(Entry("e3", "b", TimelineVisibility.Public), false, TimelineConstants.CollectionName);
        });

        // Act
        await using var scope = harness.CreateScope();
        var rows = await ReportDesignerTestServices.ReadAsync(
            scope.ServiceProvider.GetRequiredService<TimelineReportDataSource>(),
            new DataSourceQuery
            {
                DataSet = TimelineReportDataSource.EntriesDataSet,
                Fields = new HashSet<string>(StringComparer.Ordinal),
                MaxRows = 100,
                Context = new DataSourceContext { User = CalendarTestHarness.User("ana") },
            },
            TestContext.Current.CancellationToken);

        // Assert
        var index = rows.Fields.ToList().FindIndex(field => field.Name == "EntryId");
        Assert.Equal(["e1"], rows.Rows.Select(row => (string)row[index]));
    }

    private static TimelineEntry Entry(string id, string subjectId, TimelineVisibility visibility)
        => new()
        {
            ItemId = id,
            Subject = new RecordRef("Ticket", subjectId),
            Kind = visibility == TimelineVisibility.Internal ? TimelineConstants.Kinds.Note : TimelineConstants.Kinds.Comment,
            Visibility = visibility,
            Author = new PartyRef(PartyKinds.User, "agent"),
            Body = "Text",
            CreatedUtc = CalendarTestHarness.Now,
        };

    private sealed class OnlyPublicEntriesOfA : ITimelineAccessProvider
    {
        public string SubjectKind => "Ticket";

        public Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
            => Task.FromResult(subject.Id == "a" && visibility == TimelineVisibility.Public);

        public Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}

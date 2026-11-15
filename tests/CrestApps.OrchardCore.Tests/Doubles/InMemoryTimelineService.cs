using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A timeline kept in memory, for tests of modules that write notes and history to records' timelines.
/// </summary>
public sealed class InMemoryTimelineService : ITimelineService
{
    /// <summary>
    /// Gets every entry added, in the order added.
    /// </summary>
    public List<TimelineEntry> Entries { get; } = [];

    /// <summary>
    /// Gets the entries of one record, in the order added.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <returns>The entries.</returns>
    public IReadOnlyList<TimelineEntry> Of(RecordRef subject)
        => [.. Entries.Where(entry => entry.Subject == subject)];

    public Task<TimelineResult> AddAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
    {
        entry.ItemId ??= $"entry-{Entries.Count + 1}";
        Entries.Add(entry);

        return Task.FromResult(new TimelineResult { Entry = entry });
    }

    public Task<TimelineResult> ImportAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.ItemId is not null && Entries.FirstOrDefault(stored => stored.ItemId == entry.ItemId) is { } existing)
        {
            return Task.FromResult(new TimelineResult { Entry = existing });
        }

        return AddAsync(entry, cancellationToken);
    }

    public Task<TimelineResult> UpdateAsync(string entryId, Action<TimelineEntryChanges> change, CancellationToken cancellationToken = default)
    {
        var entry = Entries.FirstOrDefault(item => item.ItemId == entryId);

        if (entry is null)
        {
            return Task.FromResult(new TimelineResult { Errors = ["The entry doesn't exist."] });
        }

        var changes = new TimelineEntryChanges(entry);
        change(changes);
        entry.Properties.Clear();
        foreach (var (key, value) in changes.Properties)
        {
            entry.Properties[key] = value;
        }
        entry.Channel = changes.Channel;
        entry.ExternalId = changes.ExternalId;

        return Task.FromResult(new TimelineResult { Entry = entry });
    }

    public Task<TimelineEntry> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.FirstOrDefault(entry => entry.ExternalId == externalId));

    public Task<TimelineEntry> FindAsync(string entryId, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.FirstOrDefault(entry => entry.ItemId == entryId));

    public Task<TimelinePage> ListAsync(TimelineQuery query, CancellationToken cancellationToken = default)
    {
        var entries = Entries
            .Where(entry => entry.Subject == query.Subject && (query.IncludeInternal || entry.Visibility == TimelineVisibility.Public))
            .Where(entry => query.Kinds is not { Count: > 0 } || query.Kinds.Contains(entry.Kind))
            .OrderByDescending(entry => entry.CreatedUtc)
            .ToList();

        return Task.FromResult(new TimelinePage([.. entries.Skip(query.Skip).Take(query.Take)], entries.Count));
    }

    public Task<TimelineResult> RedactAsync(string entryId, PartyRef by, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<int> DeleteAllAsync(RecordRef subject, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.RemoveAll(entry => entry.Subject == subject));

    public Task<int> ScrubAsync(RecordRef subject, Func<string, string> scrub, CancellationToken cancellationToken = default)
    {
        var changed = 0;

        foreach (var entry in Entries.Where(entry => entry.Subject == subject))
        {
            entry.Body = scrub(entry.Body);
            entry.AuthorName = scrub(entry.AuthorName);

            if (global::CrestApps.OrchardCore.Timeline.Core.Services.TimelineScrubbing.ScrubProperties(entry.Properties, scrub) is { } properties)
            {
                entry.Properties = properties;
            }

            changed++;
        }

        return Task.FromResult(changed);
    }
}

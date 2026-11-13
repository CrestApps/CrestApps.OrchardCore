using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Timeline.Core.Services;

/// <summary>
/// The default <see cref="ITimelineService"/>, on <see cref="TimelineEntryStore"/>.
/// </summary>
public sealed class DefaultTimelineService : ITimelineService
{
    private readonly TimelineEntryStore _store;
    private readonly IEnumerable<ITimelineEntryHandler> _handlers;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTimelineService"/> class.
    /// </summary>
    /// <param name="store">The entry store.</param>
    /// <param name="handlers">The entry handlers.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultTimelineService(
        TimelineEntryStore store,
        IEnumerable<ITimelineEntryHandler> handlers,
        IClock clock,
        IStringLocalizer<DefaultTimelineService> stringLocalizer)
    {
        _store = store;
        _handlers = handlers;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<TimelineResult> AddAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Tidy(entry);

        var errors = new List<string>();

        if (entry.Subject.IsEmpty)
        {
            errors.Add(S["Say which record the entry is on."]);
        }

        if (string.IsNullOrEmpty(entry.Kind))
        {
            errors.Add(S["Say what kind of entry it is."]);
        }

        if (string.IsNullOrEmpty(entry.Body) && entry.Changes.Count == 0 && entry.AttachmentIds.Count == 0)
        {
            errors.Add(S["Write something, or attach a file."]);
        }

        if (entry.Body?.Length > TimelineConstants.MaxBodyLength)
        {
            errors.Add(S["The text is too long; keep it under {0} characters.", TimelineConstants.MaxBodyLength]);
        }

        foreach (var handler in _handlers)
        {
            await handler.AddingAsync(entry, errors, cancellationToken);
        }

        if (errors.Count > 0)
        {
            return new TimelineResult { Entry = entry, Errors = errors };
        }

        entry.ItemId ??= CrestApps.Core.UniqueId.GenerateId();

        if (entry.CreatedUtc == default)
        {
            entry.CreatedUtc = _clock.UtcNow;
        }

        await _store.CreateAsync(entry, cancellationToken);

        foreach (var handler in _handlers)
        {
            await handler.AddedAsync(entry, cancellationToken);
        }

        return new TimelineResult { Entry = entry };
    }

    /// <inheritdoc/>
    public async Task<TimelineEntry> FindAsync(string entryId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(entryId) ? null : await _store.FindByIdAsync(entryId, cancellationToken);

    /// <inheritdoc/>
    public async Task<TimelinePage> ListAsync(TimelineQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (entries, total) = await _store.PageAsync(query, cancellationToken);

        return new TimelinePage(entries, total);
    }

    /// <inheritdoc/>
    public async Task<TimelineResult> RedactAsync(string entryId, PartyRef by, CancellationToken cancellationToken = default)
    {
        var entry = await FindAsync(entryId, cancellationToken);

        if (entry is null)
        {
            return new TimelineResult { Errors = [S["The entry doesn't exist."]] };
        }

        if (entry.RedactedUtc is not null)
        {
            return new TimelineResult { Entry = entry };
        }

        entry.Body = null;
        entry.RedactedUtc = _clock.UtcNow;
        entry.RedactedBy = by;

        await _store.UpdateAsync(entry, cancellationToken);

        foreach (var handler in _handlers)
        {
            await handler.RedactedAsync(entry, cancellationToken);
        }

        return new TimelineResult { Entry = entry };
    }

    /// <inheritdoc/>
    public async Task<int> DeleteAllAsync(RecordRef subject, CancellationToken cancellationToken = default)
    {
        var entries = await _store.ListBySubjectAsync(subject, cancellationToken);

        foreach (var entry in entries)
        {
            await _store.DeleteAsync(entry, cancellationToken);
        }

        return entries.Count;
    }

    private static void Tidy(TimelineEntry entry)
    {
        entry.Kind = entry.Kind?.Trim();
        entry.Body = string.IsNullOrWhiteSpace(entry.Body) ? null : entry.Body.Trim();
        entry.AuthorName = string.IsNullOrWhiteSpace(entry.AuthorName) ? null : entry.AuthorName.Trim();
        entry.Channel = string.IsNullOrWhiteSpace(entry.Channel) ? null : entry.Channel.Trim();
        entry.Changes = [.. (entry.Changes ?? []).Where(change => change is not null && !string.IsNullOrWhiteSpace(change.Field) && change.From != change.To)];
        entry.AttachmentIds = [.. (entry.AttachmentIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)];
    }
}

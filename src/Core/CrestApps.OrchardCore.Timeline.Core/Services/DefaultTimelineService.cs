using System.Text.Json;
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

        var errors = Check(entry, limitBody: true);

        foreach (var handler in _handlers)
        {
            await handler.AddingAsync(entry, errors, cancellationToken);
        }

        if (errors.Count > 0)
        {
            return new TimelineResult { Entry = entry, Errors = errors };
        }

        Stamp(entry);

        await _store.CreateAsync(entry, cancellationToken);

        foreach (var handler in _handlers)
        {
            await handler.AddedAsync(entry, cancellationToken);
        }

        return new TimelineResult { Entry = entry };
    }

    /// <inheritdoc/>
    public async Task<TimelineResult> ImportAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!string.IsNullOrEmpty(entry.ItemId) && await _store.FindByIdAsync(entry.ItemId, cancellationToken) is { } stored)
        {
            return new TimelineResult { Entry = stored };
        }

        Tidy(entry);

        var errors = Check(entry, limitBody: false);

        if (errors.Count > 0)
        {
            return new TimelineResult { Entry = entry, Errors = errors };
        }

        Stamp(entry);

        await _store.CreateAsync(entry, cancellationToken);

        return new TimelineResult { Entry = entry };
    }

    /// <inheritdoc/>
    public async Task<TimelineResult> UpdateAsync(string entryId, Action<TimelineEntryChanges> change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (string.IsNullOrEmpty(entryId) || await _store.FindByIdAsync(entryId, cancellationToken) is not { } entry)
        {
            return new TimelineResult { Errors = [S["The entry doesn't exist."]] };
        }

        // The change works on a copy of what may change, so what the entry says, where it is, who wrote it and its kind
        // stay as written, and a refused change leaves the stored entry as it was.
        var changes = new TimelineEntryChanges(entry);
        change(changes);

        var errors = new List<string>();

        if (changes.ExternalId?.Trim() is { Length: > TimelineConstants.MaxExternalIdLength })
        {
            errors.Add(S["The external id is longer than {0} characters.", TimelineConstants.MaxExternalIdLength]);
        }

        if (changes.Channel?.Trim() is { Length: > TimelineConstants.MaxIndexedLength })
        {
            errors.Add(S["The channel is longer than {0} characters.", TimelineConstants.MaxIndexedLength]);
        }

        if (errors.Count > 0)
        {
            return new TimelineResult { Entry = entry, Errors = errors };
        }

        entry.Properties.Clear();
        foreach (var (key, value) in changes.Properties)
        {
            entry.Properties[key] = value;
        }
        entry.Channel = string.IsNullOrWhiteSpace(changes.Channel) ? null : changes.Channel.Trim();
        entry.ExternalId = string.IsNullOrWhiteSpace(changes.ExternalId) ? null : changes.ExternalId.Trim();

        await _store.UpdateAsync(entry, cancellationToken);

        foreach (var handler in _handlers)
        {
            await handler.UpdatedAsync(entry, cancellationToken);
        }

        return new TimelineResult { Entry = entry };
    }

    /// <inheritdoc/>
    public async Task<TimelineEntry> FindAsync(string entryId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(entryId) ? null : Detach(await _store.FindByIdAsync(entryId, cancellationToken));

    /// <inheritdoc/>
    public async Task<TimelineEntry> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(externalId) ? null : Detach(await _store.FindByExternalIdAsync(externalId.Trim(), cancellationToken));

    /// <inheritdoc/>
    public async Task<TimelinePage> ListAsync(TimelineQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (entries, total) = await _store.PageAsync(query, cancellationToken);

        return new TimelinePage([.. entries.Select(Detach)], total);
    }

    /// <inheritdoc/>
    public async Task<TimelineResult> RedactAsync(string entryId, PartyRef by, CancellationToken cancellationToken = default)
    {
        var entry = string.IsNullOrEmpty(entryId) ? null : await _store.FindByIdAsync(entryId, cancellationToken);

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

            foreach (var handler in _handlers)
            {
                await handler.DeletedAsync(entry, cancellationToken);
            }
        }

        return entries.Count;
    }

    // The rules every stored entry meets. Only a new entry written here is held to the body limit; history is never cut.
    private List<string> Check(TimelineEntry entry, bool limitBody)
    {
        var errors = new List<string>();

        if (entry.Subject.IsEmpty)
        {
            errors.Add(S["Say which record the entry is on."]);
        }

        if (string.IsNullOrEmpty(entry.Kind))
        {
            errors.Add(S["Say what kind of entry it is."]);
        }

        if (string.IsNullOrEmpty(entry.Body) && entry.Changes.Count == 0 && entry.AttachmentIds.Count == 0 && entry.RedactedUtc is null)
        {
            errors.Add(S["Write something, or attach a file."]);
        }

        if (limitBody && entry.Body?.Length > TimelineConstants.MaxBodyLength)
        {
            errors.Add(S["The text is too long; keep it under {0} characters.", TimelineConstants.MaxBodyLength]);
        }

        if (new[] { entry.Subject.Kind, entry.Subject.Id, entry.Kind, entry.Channel, entry.Author.Kind, entry.Author.Id }
            .Any(value => value?.Length > TimelineConstants.MaxIndexedLength))
        {
            errors.Add(S["The record, kind, channel and author can be at most {0} characters each.", TimelineConstants.MaxIndexedLength]);
        }

        if (entry.ExternalId?.Length > TimelineConstants.MaxExternalIdLength)
        {
            errors.Add(S["The external id can be at most {0} characters.", TimelineConstants.MaxExternalIdLength]);
        }

        if (!string.IsNullOrEmpty(entry.ItemId) && entry.ItemId.Length > 26)
        {
            errors.Add(S["The entry id can be at most 26 characters."]);
        }

        return errors;
    }

    private void Stamp(TimelineEntry entry)
    {
        entry.ItemId ??= CrestApps.Core.UniqueId.GenerateId();

        if (entry.CreatedUtc == default)
        {
            entry.CreatedUtc = _clock.UtcNow;
        }

        entry.CreatedUtc = DateTime.SpecifyKind(entry.CreatedUtc, DateTimeKind.Utc);
    }

    // What callers read is a copy: changing it never reaches the stored entry, which changes only through this
    // service (UpdateAsync for what may change, RedactAsync for the body).
    private static TimelineEntry Detach(TimelineEntry entry)
        => entry is null ? null : JsonSerializer.Deserialize<TimelineEntry>(JsonSerializer.SerializeToUtf8Bytes(entry));

    private static void Tidy(TimelineEntry entry)
    {
        entry.Kind = entry.Kind?.Trim();
        entry.Body = string.IsNullOrWhiteSpace(entry.Body) ? null : entry.Body.Trim();
        entry.AuthorName = string.IsNullOrWhiteSpace(entry.AuthorName) ? null : entry.AuthorName.Trim();
        entry.Channel = string.IsNullOrWhiteSpace(entry.Channel) ? null : entry.Channel.Trim();
        entry.ExternalId = string.IsNullOrWhiteSpace(entry.ExternalId) ? null : entry.ExternalId.Trim();
        entry.Changes = [.. (entry.Changes ?? []).Where(change => change is not null && !string.IsNullOrWhiteSpace(change.Field) && change.From != change.To)];
        entry.AttachmentIds = [.. (entry.AttachmentIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)];
    }
}

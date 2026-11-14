using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// Keeps the timelines of records: comments, internal notes and events, newest first. It does not check who may read
/// or write a timeline; callers that act for a user ask <see cref="ITimelineAccessService"/> first.
/// </summary>
public interface ITimelineService
{
    /// <summary>
    /// Adds an entry to a record's timeline. The entry is checked (a record, a kind, and a body, a change or an
    /// attachment), its time is stamped, and the <see cref="ITimelineEntryHandler"/>s are told before and after.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TimelineResult> AddAsync(TimelineEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings in an entry that already happened elsewhere, such as a note an older version kept on the record or an
    /// entry a recipe carries. It is checked and stamped like a new entry, but its body may be longer than
    /// <see cref="TimelineConstants.MaxBodyLength"/> (history is never cut) and no <see cref="ITimelineEntryHandler"/>
    /// is told, so nothing is announced. An entry whose id is already stored is left as it is, so importing again adds
    /// nothing.
    /// </summary>
    /// <param name="entry">The entry; give it an id derived from where it came from so a second import finds it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result, with the stored entry.</returns>
    Task<TimelineResult> ImportAsync(TimelineEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a change to an entry that doesn't rewrite what it says, such as a delivery status a channel keeps in its
    /// properties or the external id it was sent under. The body of an entry can't change this way; it can only be
    /// redacted. The <see cref="ITimelineEntryHandler"/>s are told after.
    /// </summary>
    /// <param name="entry">The entry, as read from <see cref="FindAsync"/> and changed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TimelineResult> UpdateAsync(TimelineEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an entry.
    /// </summary>
    /// <param name="entryId">The entry's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    Task<TimelineEntry> FindAsync(string entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the entry a channel stored under an id it has outside the site, such as an e-mail's message id.
    /// </summary>
    /// <param name="externalId">The external id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    Task<TimelineEntry> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of a record's timeline, newest first.
    /// </summary>
    /// <param name="query">The record and the entries to include.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    Task<TimelinePage> ListAsync(TimelineQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redacts the body of an entry, such as when it holds personal data that must go. The entry stays, with who
    /// redacted it and when; its changes and attachments stay too.
    /// </summary>
    /// <param name="entryId">The entry's id.</param>
    /// <param name="by">Who redacted it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TimelineResult> RedactAsync(string entryId, PartyRef by, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every entry on a record's timeline, when the record itself is deleted. The
    /// <see cref="ITimelineEntryHandler"/>s are told of each, so the files attached to it go too.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many entries were deleted.</returns>
    Task<int> DeleteAllAsync(RecordRef subject, CancellationToken cancellationToken = default);
}

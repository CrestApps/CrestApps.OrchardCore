using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// Says who may read and write the timeline of one kind of record. The module that owns the record kind registers it
/// with <see cref="TimelineServiceCollectionExtensions.AddTimelineAccessProvider{TProvider}"/>; a record kind without
/// a provider has a timeline nobody may read or write through the shared pages.
/// </summary>
public interface ITimelineAccessProvider
{
    /// <summary>
    /// Gets the record kind the provider answers for, a <see cref="RecordRef.Kind"/>.
    /// </summary>
    string SubjectKind { get; }

    /// <summary>
    /// Gets whether a user may read a record's entries of a visibility.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="subject">The record.</param>
    /// <param name="visibility">The visibility.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the user may read them.</returns>
    Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a user may add an entry of a kind and visibility to a record's timeline.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="subject">The record.</param>
    /// <param name="kind">The kind of entry.</param>
    /// <param name="visibility">The visibility.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the user may add it.</returns>
    Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default);
}

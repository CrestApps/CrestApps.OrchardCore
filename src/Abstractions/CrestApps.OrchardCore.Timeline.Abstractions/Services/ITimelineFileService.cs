using CrestApps.OrchardCore.Timeline.Models;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// Lists the files attached to the entries of records' timelines, from the timeline's file index, without reading the
/// entries, so a record can show all its files in one place. It does not check who may read them: callers say whether
/// internal entries' files are listed, as <see cref="ITimelineAccessService"/> allows.
/// </summary>
public interface ITimelineFileService
{
    /// <summary>
    /// Lists files, newest first.
    /// </summary>
    /// <param name="query">The records, whether internal entries' files are listed, and the page.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    Task<TimelineFilePage> ListAsync(TimelineFileQuery query, CancellationToken cancellationToken = default);
}

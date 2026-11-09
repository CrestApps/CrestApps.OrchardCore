using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Outreach;

/// <summary>
/// Pages the recipients of an outreach run with a cursor, so a run of any size is sent one batch at a time and resumes
/// where it stopped.
/// </summary>
public interface IOutreachAudienceSource
{
    /// <summary>
    /// Gets the name runs refer to the source by, in <see cref="OutreachRun.AudienceSource"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Reads one page of the run's recipients.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="cursor">Where the page starts: <see langword="null"/> for the first page, otherwise the
    /// <see cref="OutreachAudiencePage.NextCursor"/> of the page before.</param>
    /// <param name="pageSize">The most recipients to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The page. Reading the same cursor again returns the same recipients, as far as the audience has not changed.</returns>
    Task<OutreachAudiencePage> GetPageAsync(OutreachRun run, string cursor, int pageSize, CancellationToken cancellationToken = default);
}

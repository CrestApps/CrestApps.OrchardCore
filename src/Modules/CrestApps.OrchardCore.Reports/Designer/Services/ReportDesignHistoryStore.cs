using CrestApps.OrchardCore.Reports.Designer.Indexes;
using CrestApps.OrchardCore.Reports.Designer.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Reads and writes the drafts and versions of designed reports. Each is its own document, so autosaving one report
/// never rewrites another, and listing versions reads only their index.
/// </summary>
public sealed class ReportDesignHistoryStore
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignHistoryStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ReportDesignHistoryStore(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Finds the draft of a report.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <returns>The draft, or <see langword="null"/> when the report never had one.</returns>
    public Task<ReportDesignDraft> FindDraftAsync(string designId)
    {
        ArgumentException.ThrowIfNullOrEmpty(designId);

        return _session.Query<ReportDesignDraft, ReportDesignDraftIndex>(index => index.DesignId == designId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Saves a draft.
    /// </summary>
    /// <param name="draft">The draft.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SaveDraftAsync(ReportDesignDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return _session.SaveAsync(draft);
    }

    /// <summary>
    /// Lists the versions of a report from the newest, without loading them.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <returns>The versions.</returns>
    public async Task<IReadOnlyList<ReportDesignVersionIndex>> ListVersionsAsync(string designId)
    {
        ArgumentException.ThrowIfNullOrEmpty(designId);

        return (await _session.QueryIndex<ReportDesignVersionIndex>(index => index.DesignId == designId)
            .OrderByDescending(index => index.Number)
            .ListAsync())
            .ToArray();
    }

    /// <summary>
    /// Finds a version of a report.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <param name="number">The version number.</param>
    /// <returns>The version, or <see langword="null"/>.</returns>
    public Task<ReportDesignVersion> FindVersionAsync(string designId, int number)
    {
        ArgumentException.ThrowIfNullOrEmpty(designId);

        return _session.Query<ReportDesignVersion, ReportDesignVersionIndex>(index => index.DesignId == designId && index.Number == number).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Finds the newest version of a report.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <returns>The version, or <see langword="null"/> when the report has none.</returns>
    public Task<ReportDesignVersion> FindLatestVersionAsync(string designId)
    {
        ArgumentException.ThrowIfNullOrEmpty(designId);

        return _session.Query<ReportDesignVersion, ReportDesignVersionIndex>(index => index.DesignId == designId)
            .OrderByDescending(index => index.Number)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Adds a version.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AddVersionAsync(ReportDesignVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return _session.SaveAsync(version);
    }

    /// <summary>
    /// Deletes the oldest versions of a report beyond the newest <paramref name="keep"/>.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <param name="keep">How many versions to keep; at least one is always kept.</param>
    /// <returns>The number of versions deleted.</returns>
    public async Task<int> TrimVersionsAsync(string designId, int keep)
    {
        ArgumentException.ThrowIfNullOrEmpty(designId);

        var versions = await _session.Query<ReportDesignVersion, ReportDesignVersionIndex>(index => index.DesignId == designId)
            .OrderByDescending(index => index.Number)
            .Skip(Math.Max(1, keep))
            .ListAsync();
        var deleted = 0;

        foreach (var version in versions)
        {
            _session.Delete(version);
            deleted++;
        }

        return deleted;
    }

    /// <summary>
    /// Deletes the draft and every version of a report.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteAllAsync(string designId)
    {
        ArgumentException.ThrowIfNullOrEmpty(designId);

        foreach (var draft in await _session.Query<ReportDesignDraft, ReportDesignDraftIndex>(index => index.DesignId == designId).ListAsync())
        {
            _session.Delete(draft);
        }

        foreach (var version in await _session.Query<ReportDesignVersion, ReportDesignVersionIndex>(index => index.DesignId == designId).ListAsync())
        {
            _session.Delete(version);
        }
    }

    /// <summary>
    /// Commits the pending changes, so a lock held around them covers them.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task CommitAsync()
    {
        return _session.SaveChangesAsync();
    }
}

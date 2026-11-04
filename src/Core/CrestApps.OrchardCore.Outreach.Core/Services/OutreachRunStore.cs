using CrestApps.OrchardCore.Outreach.Core.Indexes;
using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Stores outreach runs in YesSql, with the concurrency check, because a batch and a cancel can change a run at once.
/// </summary>
public sealed class OutreachRunStore : DocumentCatalog<OutreachRun, OutreachRunIndex>, IOutreachRunStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachRunStore"/> class.
    /// </summary>
    /// <param name="session">The tenant session.</param>
    public OutreachRunStore(ISession session)
        : base(session)
    {
        CollectionName = OutreachConstants.CollectionName;
    }

    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;
}

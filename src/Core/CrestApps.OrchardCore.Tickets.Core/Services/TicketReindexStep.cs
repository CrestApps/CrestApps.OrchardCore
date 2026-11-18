using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Saves every ticket again, a batch at a time, so index columns added after tickets were indexed are filled from each
/// ticket's own values. It picks up where it stopped on its next run.
/// </summary>
public sealed class TicketReindexStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Tickets.Reindex";

    private const int TicketsPerRun = 200;
    private const string CursorKey = "after";

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketReindexStep"/> class.
    /// </summary>
    public TicketReindexStep(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var afterId = context.Data.TryGetValue(CursorKey, out var cursor) && long.TryParse(cursor, out var parsed) ? parsed : 0;
        var rows = (await _session.QueryIndex<TicketIndex>(index => index.Id > afterId)
            .OrderBy(index => index.Id)
            .Take(TicketsPerRun)
            .ListAsync(cancellationToken)).ToList();

        foreach (var row in rows)
        {
            if (await _session.Query<ContentItem, TicketIndex>(index => index.Id == row.Id).FirstOrDefaultAsync(cancellationToken) is { } ticket)
            {
                await _session.SaveAsync(ticket, cancellationToken: cancellationToken);
            }
        }

        if (rows.Count < TicketsPerRun)
        {
            return true;
        }

        context.Data[CursorKey] = rows[^1].Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return false;
    }
}

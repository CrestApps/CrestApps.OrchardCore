using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Brings each categorized ticket's category path up to date with its taxonomy, a page at a time, after a category moved
/// under another parent or its key changed: the list's "with sub-categories" filter, the reports and the condition
/// source read the path. A ticket whose category is gone keeps its path. It picks up where it stopped on its next run,
/// and starts over when asked again.
/// </summary>
public sealed class TicketCategoryPathsStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Tickets.CategoryPaths";

    private const int TicketsPerRun = 200;
    private const string CursorKey = "after";

    private readonly ISession _session;
    private readonly ITicketCategoryService _categories;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketCategoryPathsStep"/> class.
    /// </summary>
    public TicketCategoryPathsStep(ISession session, ITicketCategoryService categories)
    {
        _session = session;
        _categories = categories;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <summary>
    /// Asks for the step, from the first ticket again even while a run is under way, since a later move can change the
    /// paths of the tickets already passed.
    /// </summary>
    /// <param name="steps">The resumable steps.</param>
    /// <returns>A task.</returns>
    public static Task RequestAsync(ResumableSteps steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        return steps.RequestAsync(StepName, new Dictionary<string, string> { [CursorKey] = "0" });
    }

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var afterId = context.Data.TryGetValue(CursorKey, out var cursor) && long.TryParse(cursor, out var parsed) ? parsed : 0;
        var rows = (await _session.QueryIndex<TicketIndex>(index => index.Id > afterId && index.CategoryId != null)
            .OrderBy(index => index.Id)
            .Take(TicketsPerRun)
            .ListAsync(cancellationToken)).ToList();

        if (rows.Count > 0)
        {
            var rowIds = rows.Select(row => row.Id).ToArray();

            foreach (var ticket in await _session.Query<ContentItem, TicketIndex>(index => index.Id.IsIn(rowIds)).ListAsync(cancellationToken))
            {
                var part = ticket.GetOrCreate<TicketPart>();

                if (string.IsNullOrEmpty(part.CategoryId)
                    || await _categories.FindAsync(ticket.ContentType, part.CategoryId, cancellationToken) is not { Path.Length: > 0 } category
                    || string.Equals(category.Path, part.CategoryPath, StringComparison.Ordinal))
                {
                    continue;
                }

                part.CategoryPath = category.Path;
                ticket.Apply(part);
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

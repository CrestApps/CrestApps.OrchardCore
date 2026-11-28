using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// The period and filter values a tool runs or exports a report with, as the tool received them.
/// </summary>
internal sealed class ReportRunArguments
{
    /// <summary>
    /// The JSON schema of the period and filter arguments that <c>runReport</c> and <c>exportReport</c> share.
    /// </summary>
    public const string SchemaProperties =
    """
        "from": {
          "type": "string",
          "description": "The first day of the period, as yyyy-MM-dd in the user's time zone. Leave both from and to out for the report's default period."
        },
        "to": {
          "type": "string",
          "description": "The last day of the period, included, as yyyy-MM-dd in the user's time zone."
        },
        "allTime": {
          "type": "boolean",
          "description": "Run on every date instead of a period. A built-in report accepts it only when getReport says acceptsAllTime."
        },
        "filters": {
          "type": "object",
          "description": "Designed reports only: the values of the report's filters by filter id from getReport, each a list of texts; a range takes [from, to] with dates as yyyy-MM-dd; an empty list clears the filter. Filters left out keep their default values.",
          "additionalProperties": {
            "type": "array",
            "items": {
              "type": "string"
            }
          }
        }
    """;

    /// <summary>
    /// Gets the first day of the period, if given.
    /// </summary>
    public DateOnly? From { get; init; }

    /// <summary>
    /// Gets the last day of the period, if given.
    /// </summary>
    public DateOnly? To { get; init; }

    /// <summary>
    /// Gets a value indicating whether every date is wanted.
    /// </summary>
    public bool AllTime { get; init; }

    /// <summary>
    /// Gets the filter values by filter id, or <see langword="null"/> when none were given.
    /// </summary>
    public Dictionary<string, IList<string>> Filters { get; init; }

    /// <summary>
    /// Gets a value indicating whether a period or every date was asked for.
    /// </summary>
    public bool HasPeriod => From.HasValue || To.HasValue || AllTime;

    /// <summary>
    /// Checks the arguments for a built-in report, which takes the period only.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <returns>The problem to tell the model, or <see langword="null"/>.</returns>
    public string CheckFor(ReportDescriptor report)
    {
        if (Filters is { Count: > 0 })
        {
            return "A built-in report takes only the period (from, to or allTime) here; its other filters are set on its page. Leave filters out.";
        }

        if (AllTime && report.DefaultDateRange is null)
        {
            return "This report does not run on every date. Give from and to.";
        }

        return CheckPeriod();
    }

    /// <summary>
    /// Checks the arguments for a designed report: every filter must be one its viewers can change.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <returns>The problem to tell the model, or <see langword="null"/>.</returns>
    public string CheckFor(ReportDesign design)
    {
        var exposed = ReportToolCatalog.GetExposedFilters(design).Select(filter => filter.Id).ToHashSet(StringComparer.Ordinal);
        var unknown = (Filters?.Keys ?? Enumerable.Empty<string>()).Where(id => !exposed.Contains(id)).ToArray();

        if (unknown.Length > 0)
        {
            return exposed.Count == 0
                ? "This report has no filters its viewers can change. Leave filters out."
                : $"This report has no filter {string.Join(", ", unknown.Select(id => $"'{id}'"))} its viewers can change. Its filters are: {string.Join(", ", exposed)}.";
        }

        return CheckPeriod();
    }

    /// <summary>
    /// Gets the period of a designed report run, set on its first date filter its viewers can change.
    /// </summary>
    /// <returns>The period, or <see langword="null"/> to keep the filters' values.</returns>
    public ReportRunPeriod GetPeriod()
        => AllTime ? new ReportRunPeriod(null, null) : HasPeriod ? new ReportRunPeriod(From, To) : null;

    private string CheckPeriod()
    {
        if (AllTime && (From.HasValue || To.HasValue))
        {
            return "Give either allTime or a period (from and to), not both.";
        }

        if (From.HasValue && To.HasValue && From > To)
        {
            return "The first day (from) is after the last day (to).";
        }

        return null;
    }
}

using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The fixed report layouts' capability filtering: a column, metric or cell produced by a feature the tenant has not
/// enabled is left out.
/// </summary>
internal sealed partial class EnterpriseInteractionReportProvider
{
    private bool IsAvailable(string requiredFeatureId)
        => requiredFeatureId is null || !_absentFeatureIds.Contains(requiredFeatureId);

    /// <summary>
    /// Keeps the entries of a fixed layout whose producing capability is enabled. The same requirement list drives
    /// the columns and every row, so a column and its cells cannot drift apart.
    /// </summary>
    private T[] SelectAvailable<T>(IReadOnlyList<T> entries, IReadOnlyList<string> requirements)
    {
        if (entries.Count != requirements.Count)
        {
            throw new InvalidOperationException(
                $"A report layout declares {entries.Count} entries but {requirements.Count} capability requirements. " +
                "The requirement list must have one entry per column so a column and its cells cannot drift apart.");
        }

        if (_absentFeatureIds.Count == 0)
        {
            return [.. entries];
        }

        return [.. entries.Where((_, position) => IsAvailable(requirements[position]))];
    }

    private ReportRow SelectAvailableRow(
        IReadOnlyList<string> cells,
        IReadOnlyList<string> requirements,
        ReportRowKind kind = ReportRowKind.Detail)
        => new(SelectAvailable(cells, requirements), kind);
}

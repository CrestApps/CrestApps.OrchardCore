using System.Data;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Carries, for the length of one import, what the do-not-call screening decided about each row: the rows to import
/// marked Do not call rather than skip, and the rows whose numbers were checked against a registry. The row filter
/// and the import handlers run in the same scope, so the filter records the row and a handler reads it while it maps
/// that row.
/// </summary>
public sealed class ImportRowDoNotCallFlags
{
    private readonly HashSet<DataRow> _rows = [];
    private readonly HashSet<DataRow> _screenedRows = [];

    /// <summary>
    /// Records that the row is to be imported marked Do not call.
    /// </summary>
    /// <param name="row">The row.</param>
    public void Mark(DataRow row)
    {
        if (row is not null)
        {
            _rows.Add(row);
        }
    }

    /// <summary>
    /// Determines whether the row is to be imported marked Do not call.
    /// </summary>
    /// <param name="row">The row.</param>
    public bool IsMarked(DataRow row)
        => row is not null && _rows.Contains(row);

    /// <summary>
    /// Records that the row's numbers were checked against at least one do-not-call registry.
    /// </summary>
    /// <param name="row">The row.</param>
    public void MarkScreened(DataRow row)
    {
        if (row is not null)
        {
            _screenedRows.Add(row);
        }
    }

    /// <summary>
    /// Determines whether the row's numbers were checked against a do-not-call registry.
    /// </summary>
    /// <param name="row">The row.</param>
    public bool IsScreened(DataRow row)
        => row is not null && _screenedRows.Contains(row);
}

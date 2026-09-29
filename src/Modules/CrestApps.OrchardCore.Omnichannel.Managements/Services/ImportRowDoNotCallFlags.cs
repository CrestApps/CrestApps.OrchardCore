using System.Data;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Carries, for the length of one import, the rows the do-not-call screening decided to import marked Do not call
/// rather than skip. The row filter and the import handler run in the same scope, so the filter records the row and
/// the handler reads it while it maps that row.
/// </summary>
public sealed class ImportRowDoNotCallFlags
{
    private readonly HashSet<DataRow> _rows = [];

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
}

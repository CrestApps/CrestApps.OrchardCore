using System.Diagnostics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.DncRegistry.Telemetry;

/// <summary>
/// Counts what became of the rows of one import run of a local do-not-call list, and adds them to
/// <see cref="DncRegistryTelemetry.ImportRows"/> only once the batch that holds them is saved. Rows read after the last
/// saved batch are read again when the import resumes, so counting them as they are read would count them twice.
/// </summary>
internal sealed class DncImportRowTally
{
    private readonly Dictionary<string, long> _pending = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the number of rows of this run that were saved to the list.
    /// </summary>
    public long ImportedRows { get; private set; }

    /// <summary>
    /// Gets the number of rows of this run that were saved as rejected.
    /// </summary>
    public long RejectedRows { get; private set; }

    /// <summary>
    /// Adds a row that is not saved yet.
    /// </summary>
    /// <param name="result">What became of the row, from <see cref="DncRegistryTelemetry"/>'s row results.</param>
    public void Add(string result)
    {
        _pending[result] = _pending.TryGetValue(result, out var count)
            ? count + 1
            : 1;
    }

    /// <summary>
    /// Records the rows added since the last commit, now that they are saved.
    /// </summary>
    public void Commit()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var enabled = DncRegistryTelemetry.ImportRows.Enabled;

        foreach (var (result, count) in _pending)
        {
            if (result == DncRegistryTelemetry.RowImported)
            {
                ImportedRows += count;
            }
            else if (result != DncRegistryTelemetry.RowHeader)
            {
                RejectedRows += count;
            }

            if (!enabled)
            {
                continue;
            }

            var tags = new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ResultTag, result },
            };

            CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
            DncRegistryTelemetry.ImportRows.Add(count, tags);
        }

        _pending.Clear();
    }
}

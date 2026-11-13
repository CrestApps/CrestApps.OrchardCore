using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.SourceAccess;

namespace CrestApps.OrchardCore.Disbursements.Core.Services;

/// <summary>
/// Applies a <see cref="SourceAccessFilter"/> to disbursements and batches.
/// </summary>
public static class DisbursementAccess
{
    /// <summary>
    /// Returns whether the filter allows a disbursement.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="disbursement">The disbursement.</param>
    /// <returns><see langword="true"/> when it is allowed.</returns>
    public static bool Allows(this SourceAccessFilter filter, Disbursement disbursement)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(disbursement);

        return filter.Allows(disbursement.SourceKind, disbursement.AccessScope);
    }

    /// <summary>
    /// Returns whether the filter allows every disbursement of a batch.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="batch">The batch.</param>
    /// <returns><see langword="true"/> when they are all allowed.</returns>
    public static bool Allows(this SourceAccessFilter filter, DisbursementBatch batch)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(batch);

        foreach (var source in batch.Sources ?? [])
        {
            var separator = source.IndexOf(SourceAccessFilter.KeySeparator, StringComparison.Ordinal);
            var kind = separator < 0 ? source : source[..separator];
            var scope = separator < 0 ? null : source[(separator + 1)..];

            if (!filter.Allows(kind, string.IsNullOrEmpty(scope) ? null : scope))
            {
                return false;
            }
        }

        return true;
    }
}

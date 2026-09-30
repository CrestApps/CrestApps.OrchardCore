using CrestApps.OrchardCore.DncRegistry.Models;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Decides whether the scheduled local DNC task should pick up a list. Imports and deletions
/// save <see cref="LocalDncList.ProcessSaveUtc"/> as they make progress, so a list whose
/// heartbeat is recent is still being worked and a list whose heartbeat went quiet was
/// abandoned (a restart, a lost after-request job, or a delete refused while an import held the lock).
/// </summary>
internal static class LocalDncListRecoveryPolicy
{
    /// <summary>
    /// How long a processing or deleting list can go without saving progress before it is treated as abandoned.
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long to wait after each failed import attempt before retrying it automatically.
    /// The wait grows with every consecutive failure.
    /// </summary>
    public static readonly TimeSpan RetryBackoff = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The number of consecutive failed import attempts after which the list is left for an administrator.
    /// </summary>
    public const int MaxAutomaticImportAttempts = 5;

    /// <summary>
    /// Determines what the scheduled task should do with the given list right now.
    /// </summary>
    /// <param name="list">The list to evaluate.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns>The action to take.</returns>
    public static LocalDncListRecoveryAction Evaluate(LocalDncList list, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(list);

        return list.Status switch
        {
            LocalDncListStatus.Pending => LocalDncListRecoveryAction.Import,
            LocalDncListStatus.Processing => IsStale(list, utcNow)
                ? LocalDncListRecoveryAction.Import
                : LocalDncListRecoveryAction.None,
            LocalDncListStatus.Deleting => IsStale(list, utcNow)
                ? LocalDncListRecoveryAction.Delete
                : LocalDncListRecoveryAction.None,
            LocalDncListStatus.Failed => CanRetryFailedImport(list, utcNow)
                ? LocalDncListRecoveryAction.Import
                : LocalDncListRecoveryAction.None,
            _ => LocalDncListRecoveryAction.None,
        };
    }

    private static bool IsStale(LocalDncList list, DateTime utcNow)
        => !list.ProcessSaveUtc.HasValue
        || utcNow - list.ProcessSaveUtc.Value >= StaleAfter;

    private static bool CanRetryFailedImport(LocalDncList list, DateTime utcNow)
    {
        if (list.FailedAttempts >= MaxAutomaticImportAttempts)
        {
            return false;
        }

        if (!list.ProcessSaveUtc.HasValue)
        {
            return true;
        }

        // Lists that failed before attempts were counted read as zero, and wait like a first failure.
        var wait = RetryBackoff * Math.Max(1, list.FailedAttempts);

        return utcNow - list.ProcessSaveUtc.Value >= wait;
    }
}

/// <summary>
/// The action the scheduled local DNC task takes for a list.
/// </summary>
internal enum LocalDncListRecoveryAction
{
    None,
    Import,
    Delete,
}

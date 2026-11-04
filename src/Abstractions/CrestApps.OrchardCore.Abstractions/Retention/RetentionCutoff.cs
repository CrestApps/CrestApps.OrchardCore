namespace CrestApps.OrchardCore.Retention;

/// <summary>
/// Computes retention cutoffs from a window and a floor, both in days.
/// </summary>
public static class RetentionCutoff
{
    /// <summary>
    /// Computes the time before which records may be removed.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="retentionDays">The retention window; zero or less keeps records forever.</param>
    /// <param name="minimumRetentionDays">A floor the window can never go below, such as a legal hold.</param>
    /// <param name="cutoffUtc">When the method returns <see langword="true"/>, the exclusive UTC cutoff.</param>
    /// <returns><see langword="true"/> when records are removed; <see langword="false"/> when they are kept forever.</returns>
    public static bool TryCompute(DateTime nowUtc, double retentionDays, double minimumRetentionDays, out DateTime cutoffUtc)
    {
        cutoffUtc = default;

        if (retentionDays <= 0)
        {
            return false;
        }

        cutoffUtc = nowUtc.AddDays(-Math.Max(retentionDays, Math.Max(0, minimumRetentionDays)));

        return true;
    }
}

using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Computes the effective interaction-event purge cutoff from the retention window and its governance floors.
/// The calculation is a pure function of the current time and options so it can be unit tested, and the floors
/// can only push the cutoff further into the past (keep data longer), never purge earlier than configured.
/// </summary>
public static class RetentionCutoffCalculator
{
    /// <summary>
    /// Computes the effective interaction-event purge cutoff, honoring the projection replay horizon and
    /// legal-hold floors.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="options">The configured retention options.</param>
    /// <param name="cutoffUtc">
    /// When the method returns <see langword="true"/>, the UTC time before which interaction events may be purged.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when purging is enabled and a cutoff was computed; otherwise <see langword="false"/>
    /// because purging is disabled and events are kept indefinitely.
    /// </returns>
    public static bool TryComputeCutoff(DateTime nowUtc, ContactCenterRetentionOptions options, out DateTime cutoffUtc)
    {
        ArgumentNullException.ThrowIfNull(options);

        return TryComputeCutoff(
            nowUtc,
            options.InteractionEventRetentionDays,
            Math.Max(options.ProjectionReplayHorizonDays, options.LegalHoldMinimumDays),
            out cutoffUtc);
    }

    /// <summary>
    /// Computes the effective purge cutoff for a single entity from its own retention window and the floor
    /// that applies to it. The floor can only push the cutoff further into the past, never nearer the present,
    /// so a floor can lengthen retention but can never shorten it.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="retentionDays">The configured retention window in days. Zero or less disables purging.</param>
    /// <param name="minimumRetentionDays">
    /// The governance floor in days. Values of zero or less apply no floor.
    /// </param>
    /// <param name="cutoffUtc">
    /// When the method returns <see langword="true"/>, the UTC time before which records may be purged.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when purging is enabled and a cutoff was computed; otherwise <see langword="false"/>
    /// because purging is disabled and records are kept indefinitely.
    /// </returns>
    public static bool TryComputeCutoff(DateTime nowUtc, double retentionDays, double minimumRetentionDays, out DateTime cutoffUtc)
    {
        cutoffUtc = default;

        if (retentionDays <= 0)
        {
            return false;
        }

        var effectiveDays = Math.Max(retentionDays, Math.Max(0, minimumRetentionDays));

        cutoffUtc = nowUtc.AddDays(-effectiveDays);

        return true;
    }
}

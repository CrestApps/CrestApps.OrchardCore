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
    /// Computes the effective purge cutoff, honoring the projection replay horizon and legal-hold floors.
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

        cutoffUtc = default;

        if (options.InteractionEventRetentionDays <= 0)
        {
            return false;
        }

        var effectiveDays = Math.Max(
            options.InteractionEventRetentionDays,
            Math.Max(
                Math.Max(0, options.ProjectionReplayHorizonDays),
                Math.Max(0, options.LegalHoldMinimumDays)));

        cutoffUtc = nowUtc.AddDays(-effectiveDays);

        return true;
    }
}

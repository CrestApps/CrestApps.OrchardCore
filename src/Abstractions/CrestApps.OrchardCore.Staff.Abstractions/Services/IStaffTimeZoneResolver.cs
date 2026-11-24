using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Works out a staff member's time zone: their own, else their main team's (walking up parent teams), else the site's.
/// Every module that needs a person's local day uses it, so the same person never gets two zones.
/// </summary>
public interface IStaffTimeZoneResolver
{
    /// <summary>
    /// Gets a staff member's IANA time zone.
    /// </summary>
    /// <param name="staffMember">The staff member, or <see langword="null"/> for the site's zone.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The time zone id.</returns>
    Task<string> GetTimeZoneIdAsync(StaffMember staffMember, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a staff member's own time zone or their main team's (walking up parent teams), without falling back to the
    /// site's, for modules that have a fallback of their own.
    /// </summary>
    /// <param name="staffMember">The staff member.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The time zone id, or <see langword="null"/> when neither the person nor their teams have one.</returns>
    Task<string> FindTimeZoneIdAsync(StaffMember staffMember, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the employment history Staff records.
/// </summary>
public interface IStaffHistoryService
{
    /// <summary>
    /// Lists a staff member's history, newest first.
    /// </summary>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="take">The most entries to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entries.</returns>
    Task<IReadOnlyList<StaffHistoryEntry>> ListAsync(string staffMemberId, int take = 100, CancellationToken cancellationToken = default);
}

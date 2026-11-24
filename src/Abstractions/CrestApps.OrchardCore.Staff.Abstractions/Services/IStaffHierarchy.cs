using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// The reporting line: who a person reports to and who reports to them.
/// </summary>
public interface IStaffHierarchy
{
    /// <summary>
    /// Gets a staff member's manager, the manager's manager and so on, nearest first. A loop in the data stops the walk.
    /// </summary>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The managers.</returns>
    Task<IReadOnlyList<StaffMember>> GetManagerChainAsync(string staffMemberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the people who report to a staff member.
    /// </summary>
    /// <param name="staffMemberId">The manager.</param>
    /// <param name="includeIndirect">Whether the reports of reports count.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reports.</returns>
    Task<IReadOnlyList<StaffMember>> GetReportsAsync(string staffMemberId, bool includeIndirect = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether one staff member manages another, directly or through others.
    /// </summary>
    /// <param name="managerId">The manager.</param>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the first manages the second.</returns>
    Task<bool> ManagesAsync(string managerId, string staffMemberId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The staff a signed-in person may manage.
/// </summary>
public sealed class StaffScope
{
    /// <summary>
    /// A scope that covers nobody.
    /// </summary>
    public static readonly StaffScope None = new(false, new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// A scope that covers everybody.
    /// </summary>
    public static readonly StaffScope Everyone = new(true, new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffScope"/> class.
    /// </summary>
    /// <param name="all">Whether everybody is covered.</param>
    /// <param name="teamIds">The teams managed, with their sub-teams.</param>
    /// <param name="staffMemberIds">The people managed through the reporting line.</param>
    public StaffScope(bool all, IReadOnlySet<string> teamIds, IReadOnlySet<string> staffMemberIds)
    {
        All = all;
        TeamIds = teamIds ?? new HashSet<string>(StringComparer.Ordinal);
        StaffMemberIds = staffMemberIds ?? new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets whether everybody is covered.
    /// </summary>
    public bool All { get; }

    /// <summary>
    /// Gets the teams managed, with their sub-teams.
    /// </summary>
    public IReadOnlySet<string> TeamIds { get; }

    /// <summary>
    /// Gets the people managed through the reporting line.
    /// </summary>
    public IReadOnlySet<string> StaffMemberIds { get; }

    /// <summary>
    /// Gets whether the scope covers nobody.
    /// </summary>
    public bool IsEmpty => !All && TeamIds.Count == 0 && StaffMemberIds.Count == 0;

    /// <summary>
    /// Gets whether a team is managed.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <returns><see langword="true"/> when covered.</returns>
    public bool CoversTeam(string teamId)
        => All || (!string.IsNullOrEmpty(teamId) && TeamIds.Contains(teamId));

    /// <summary>
    /// Gets whether a staff member is managed, through the reporting line or a team on a day.
    /// </summary>
    /// <param name="staffMember">The staff member.</param>
    /// <param name="date">The day team memberships must count on.</param>
    /// <returns><see langword="true"/> when covered.</returns>
    public bool Covers(StaffMember staffMember, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(staffMember);

        return All
            || StaffMemberIds.Contains(staffMember.ItemId)
            || staffMember.MembershipsOn(date).Any(membership => TeamIds.Contains(membership.TeamId));
    }
}

/// <summary>
/// Works out which staff a signed-in person may manage: everybody with <see cref="StaffPermissions.ManageStaff"/>, else
/// their reports and the members of the teams they manage when they have <see cref="StaffPermissions.ManageOwnStaff"/>.
/// </summary>
public interface IStaffScopeService
{
    /// <summary>
    /// Gets the scope of a signed-in person.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scope.</returns>
    Task<StaffScope> GetScopeAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a signed-in person may change a staff member.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="staffMember">The staff member.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    Task<bool> CanManageAsync(ClaimsPrincipal user, StaffMember staffMember, CancellationToken cancellationToken = default);
}

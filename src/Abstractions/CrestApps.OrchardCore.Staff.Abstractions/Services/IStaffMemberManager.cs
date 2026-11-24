using CrestApps.Core.Services;
using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Manages staff members. Writes run the catalog handlers, which validate the record, record its history and tell the
/// <see cref="IStaffLifecycleObserver"/>s what changed.
/// </summary>
public interface IStaffMemberManager : ICatalogManager<StaffMember>
{
    /// <summary>
    /// Finds the staff member linked to a user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff member, or <see langword="null"/>.</returns>
    Task<StaffMember> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a staff member by employee (badge) number, ignoring case and spaces.
    /// </summary>
    /// <param name="employeeNumber">The employee number.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff member, or <see langword="null"/>.</returns>
    Task<StaffMember> FindByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the staff members linked to users.
    /// </summary>
    /// <param name="userIds">The users.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff members found.</returns>
    Task<IReadOnlyList<StaffMember>> GetByUserIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches staff members, ordered by name.
    /// </summary>
    /// <param name="query">The filters.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The total that match and the page.</returns>
    Task<(int Count, IReadOnlyList<StaffMember> Items)> SearchAsync(StaffQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the members of a team on a day (members of its sub-teams are not included).
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff members, ordered by name.</returns>
    Task<IReadOnlyList<StaffMember>> ListByTeamAsync(string teamId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the members of a team at any time in a range of days.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff members, ordered by name.</returns>
    Task<IReadOnlyList<StaffMember>> ListByTeamInRangeAsync(string teamId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the staff members who report straight to a staff member.
    /// </summary>
    /// <param name="managerId">The manager.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The direct reports, ordered by name.</returns>
    Task<IReadOnlyList<StaffMember>> ListDirectReportsAsync(string managerId, CancellationToken cancellationToken = default);
}

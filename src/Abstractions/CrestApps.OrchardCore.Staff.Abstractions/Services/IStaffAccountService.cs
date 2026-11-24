using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Makes sure the person doing a module's work is a staff member, such as an agent who signs in to a queue or a
/// cashier who opens a register.
/// </summary>
public interface IStaffAccountService
{
    /// <summary>
    /// Makes sure a user has a staff record, creating one when they have none, and adds capabilities to it. Nothing else
    /// on an existing record changes.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="displayName">The name to use until the record takes it from the user.</param>
    /// <param name="capabilities">The capabilities to add.</param>
    /// <param name="defaults">Sets values on a new record, such as the job title or team.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff member, or <see langword="null"/> when it couldn't be saved.</returns>
    Task<StaffMember> EnsureAsync(string userId, string displayName, IReadOnlyCollection<string> capabilities, Action<StaffMember> defaults = null, CancellationToken cancellationToken = default);
}

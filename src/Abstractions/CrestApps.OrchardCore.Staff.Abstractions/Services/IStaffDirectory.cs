using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// The read side of Staff that other modules use: who is staff, their names and a search for pickers. Names come from
/// the staff record, which Staff keeps in step with the linked user, so modules never copy them.
/// </summary>
public interface IStaffDirectory
{
    /// <summary>
    /// Gets whether a user is an active staff member (active or on leave, and not past the termination date).
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the user is staff.</returns>
    Task<bool> IsStaffAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the staff member of a user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff member, or <see langword="null"/>.</returns>
    Task<StaffMember> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets staff members by id.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff members found, by id.</returns>
    Task<IReadOnlyDictionary<string, StaffMember>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the names of staff members by id.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The names found, by id.</returns>
    Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the names of the staff members linked to users, by user id.
    /// </summary>
    /// <param name="userIds">The users.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The names found, by user id.</returns>
    Task<IReadOnlyDictionary<string, string>> GetNamesByUserIdAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches active staff for a picker, ordered by name.
    /// </summary>
    /// <param name="text">Text in the name, e-mail, job title or employee number, or empty for everyone.</param>
    /// <param name="capability">A capability the person must have, or <see langword="null"/>.</param>
    /// <param name="requireUser">Whether only people with a user account are returned.</param>
    /// <param name="take">The most to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff members.</returns>
    Task<IReadOnlyList<StaffMember>> SearchAsync(string text, string capability = null, bool requireUser = false, int take = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the capabilities modules registered, ordered for display.
    /// </summary>
    /// <returns>The capabilities.</returns>
    IReadOnlyList<StaffCapability> GetCapabilities();
}

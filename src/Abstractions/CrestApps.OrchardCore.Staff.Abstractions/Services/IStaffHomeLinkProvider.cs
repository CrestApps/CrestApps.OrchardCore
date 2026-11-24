using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Adds links to a staff member's own page (My profile), such as their schedule, their time or their field work.
/// Register one with <c>AddStaffHomeLinkProvider</c>; Staff never names the modules that add links.
/// </summary>
public interface IStaffHomeLinkProvider
{
    /// <summary>
    /// Gets the links for a staff member.
    /// </summary>
    /// <param name="staffMember">The staff member.</param>
    /// <param name="user">The signed-in person, who is the staff member.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The links.</returns>
    Task<IEnumerable<StaffHomeLink>> GetLinksAsync(StaffMember staffMember, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

/// <summary>
/// A link on a staff member's own page.
/// </summary>
/// <param name="Title">The link text.</param>
/// <param name="Url">Where it goes, such as <c>~/Admin/my-work</c>.</param>
/// <param name="Description">What it is for.</param>
/// <param name="Icon">A Font Awesome icon class, such as <c>fa-solid fa-calendar</c>.</param>
/// <param name="Position">The order among the links.</param>
public sealed record StaffHomeLink(string Title, string Url, string Description = null, string Icon = null, int Position = 0);

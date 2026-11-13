using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Filters for <see cref="IStaffMemberManager.SearchAsync"/>. Every filter that is set must match.
/// </summary>
public sealed class StaffQuery
{
    /// <summary>
    /// Gets or sets text found in the name, e-mail, job title or employee number.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets a team; members of it and of the teams under it match.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets teams; members of any of them match (sub-teams are not added).
    /// </summary>
    public IReadOnlyCollection<string> TeamIds { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public StaffStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets whether only people with (or without) a user account match.
    /// </summary>
    public bool? HasUser { get; set; }

    /// <summary>
    /// Gets or sets a capability the person must have.
    /// </summary>
    public string Capability { get; set; }

    /// <summary>
    /// Gets or sets the manager the person reports straight to.
    /// </summary>
    public string ManagerId { get; set; }

    /// <summary>
    /// Gets or sets staff member ids; only these match.
    /// </summary>
    public IReadOnlyCollection<string> Ids { get; set; }

    /// <summary>
    /// Gets or sets capabilities; a staff member with any of them matches. Used with <see cref="Capability"/>, both apply.
    /// </summary>
    public IReadOnlyCollection<string> Capabilities { get; set; }
}

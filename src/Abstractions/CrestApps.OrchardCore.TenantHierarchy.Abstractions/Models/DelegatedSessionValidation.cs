namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Holds the outcome of validating a delegated access session with the parent.
/// </summary>
public sealed class DelegatedSessionValidation
{
    /// <summary>
    /// Gets the result for a session that is no longer active.
    /// </summary>
    public static DelegatedSessionValidation Inactive { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the session is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the roles granted in the child tenant.
    /// </summary>
    public string[] ChildRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the version of the granted roles.
    /// </summary>
    public string RolesVersion { get; set; }
}

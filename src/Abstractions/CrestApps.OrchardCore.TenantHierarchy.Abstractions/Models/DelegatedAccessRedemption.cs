namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Holds what a child tenant learns when it redeems a one-time code. It carries only the parent user, the granted
/// child roles and the session, never anything about another child tenant.
/// </summary>
public sealed class DelegatedAccessRedemption
{
    /// <summary>
    /// Gets or sets the parent tenant identifier.
    /// </summary>
    public string ParentTenantId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the parent tenant.
    /// </summary>
    public string ParentDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the slug of the parent tenant.
    /// </summary>
    public string ParentSlug { get; set; }

    /// <summary>
    /// Gets or sets the base address of the parent tenant.
    /// </summary>
    public string ParentAddress { get; set; }

    /// <summary>
    /// Gets or sets the singular word the parent uses for a child tenant.
    /// </summary>
    public string ChildLabel { get; set; }

    /// <summary>
    /// Gets or sets how the tenant switcher shows the list of child tenants.
    /// </summary>
    public SwitcherMode SwitcherMode { get; set; }

    /// <summary>
    /// Gets or sets the parent user identifier.
    /// </summary>
    public string ParentUserId { get; set; }

    /// <summary>
    /// Gets or sets the parent user name.
    /// </summary>
    public string ParentUserName { get; set; }

    /// <summary>
    /// Gets or sets the email address of the parent user.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the roles granted in the child tenant.
    /// </summary>
    public string[] ChildRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the version of the granted roles.
    /// </summary>
    public string RolesVersion { get; set; }

    /// <summary>
    /// Gets or sets the delegated access session identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the authentication methods of the parent sign-in.
    /// </summary>
    public string[] AuthenticationMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets the interval at which the child validates the session.
    /// </summary>
    public TimeSpan ValidationInterval { get; set; }
}

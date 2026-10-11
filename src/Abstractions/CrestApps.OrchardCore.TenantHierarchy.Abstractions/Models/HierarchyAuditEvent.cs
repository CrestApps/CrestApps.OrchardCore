namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// The tenant hierarchy data of an Orchard Core audit trail event, recorded in the parent tenant. Child tenants cannot
/// write it.
/// </summary>
public sealed class HierarchyAuditEvent
{
    /// <summary>
    /// Gets or sets the event name, one of <see cref="HierarchyAuditEventNames"/>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the registry entry of the child tenant the event is about.
    /// </summary>
    public string ChildEntryId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the child tenant when the event happened.
    /// </summary>
    public string ChildDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the parent user who caused the event.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the name of the parent user who caused the event.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the request that caused the event.
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// Gets or sets a short reference to the delegated access session, when the event is about one.
    /// </summary>
    public string SessionReference { get; set; }

    /// <summary>
    /// Gets or sets the authentication methods of the parent sign-in, when the event is about entering a child tenant.
    /// </summary>
    public string[] AuthenticationMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets details about the event.
    /// </summary>
    public string Details { get; set; }
}

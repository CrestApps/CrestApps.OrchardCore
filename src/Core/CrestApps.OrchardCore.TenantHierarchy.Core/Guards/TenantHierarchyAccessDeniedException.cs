namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Thrown when code running in a parent, child or ordinary tenant tries to reach a tenant outside what the tenant
/// hierarchy allows.
/// </summary>
public sealed class TenantHierarchyAccessDeniedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantHierarchyAccessDeniedException"/> class.
    /// </summary>
    public TenantHierarchyAccessDeniedException()
        : base("The tenant hierarchy refused access to another tenant.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantHierarchyAccessDeniedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public TenantHierarchyAccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantHierarchyAccessDeniedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TenantHierarchyAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

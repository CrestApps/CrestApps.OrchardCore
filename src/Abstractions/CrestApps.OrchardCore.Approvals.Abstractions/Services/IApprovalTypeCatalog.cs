namespace CrestApps.OrchardCore.Approvals.Services;

/// <summary>
/// The approval types registered on the tenant, and their handlers.
/// </summary>
public interface IApprovalTypeCatalog
{
    /// <summary>
    /// Lists the types, by name.
    /// </summary>
    /// <returns>The types.</returns>
    IReadOnlyList<ApprovalType> List();

    /// <summary>
    /// Finds a type.
    /// </summary>
    /// <param name="name">The type's name.</param>
    /// <returns>The type, or <see langword="null"/> when it isn't registered (its feature is off).</returns>
    ApprovalType Find(string name);

    /// <summary>
    /// Returns the handler of a type: its own, or one whose hooks do nothing for a type without one or not registered.
    /// </summary>
    /// <param name="name">The type's name.</param>
    /// <returns>The handler.</returns>
    IApprovalTypeHandler GetHandler(string name);
}

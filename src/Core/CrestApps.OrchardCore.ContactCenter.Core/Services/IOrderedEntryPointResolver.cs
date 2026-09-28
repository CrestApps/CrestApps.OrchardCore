namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Lets an <see cref="IEntryPointResolver"/> say where it belongs in the chain. A resolver that does not
/// implement this runs at order zero, which is what every existing resolver does today, so adding the chain
/// changes nothing for them.
/// </summary>
public interface IOrderedEntryPointResolver
{
    /// <summary>
    /// Gets the order this resolver is consulted in, lowest first.
    /// </summary>
    int Order { get; }
}

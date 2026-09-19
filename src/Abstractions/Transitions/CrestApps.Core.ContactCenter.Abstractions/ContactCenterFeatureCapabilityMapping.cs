namespace CrestApps.Core.ContactCenter;

/// <summary>
/// Declares that a host deployment unit owns a Contact Center capability, so disabling that unit
/// quiesces and drains the work the capability covers.
/// </summary>
/// <remarks>
/// This is contributed to dependency injection by whichever startup owns the participant, rather
/// than held in one table. A single table would have to name the provider feature ids, and the
/// Contact Center module is forbidden by an architecture test from referencing them; the providers
/// are also separate packages that the framework cannot enumerate. Contributing the edge from the
/// owning side keeps each module naming only its own identifiers.
/// <para>
/// A deployment unit may own more than one capability, so more than one mapping may name the same
/// unit.
/// </para>
/// </remarks>
public sealed class ContactCenterFeatureCapabilityMapping
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterFeatureCapabilityMapping"/> class.
    /// </summary>
    /// <param name="featureId">The host deployment unit, which in Orchard Core is a feature id.</param>
    /// <param name="capability">The capability that unit owns.</param>
    public ContactCenterFeatureCapabilityMapping(string featureId, string capability)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureId);
        ArgumentException.ThrowIfNullOrEmpty(capability);

        FeatureId = featureId;
        Capability = capability;
    }

    /// <summary>
    /// Gets the host deployment unit that owns the capability.
    /// </summary>
    public string FeatureId { get; }

    /// <summary>
    /// Gets the capability the deployment unit owns.
    /// </summary>
    public string Capability { get; }
}

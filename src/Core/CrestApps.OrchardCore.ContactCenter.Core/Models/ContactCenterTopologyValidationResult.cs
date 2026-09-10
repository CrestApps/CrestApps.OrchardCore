namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The verdict on whether this deployment satisfies the topology it declared.
/// </summary>
public sealed class ContactCenterTopologyValidationResult
{
    /// <summary>
    /// Gets the topology profile identifier the operator declared, if any.
    /// </summary>
    public string DeclaredProfileId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the declared topology is a production topology.
    /// </summary>
    public bool IsProductionTopology { get; init; }

    /// <summary>
    /// Gets the reasons the deployment does not satisfy the topology it declared.
    /// </summary>
    /// <remarks>
    /// Every missing component is reported, not only the first. An operator fixing one at a time across
    /// successive deployments is the slowest possible way to reach a supported configuration.
    /// </remarks>
    public IReadOnlyList<string> Failures { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the deployment satisfies the topology it declared.
    /// </summary>
    public bool IsSatisfied => Failures.Count == 0;
}

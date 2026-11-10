namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Adds the capabilities and transports a feature offers to the store's UCP profile.
/// </summary>
public interface IUcpProfileContributor
{
    /// <summary>
    /// Adds what the feature offers for the profile's version.
    /// </summary>
    /// <param name="context">The profile.</param>
    Task ContributeAsync(UcpProfileContext context);
}

using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.Builders;

/// <summary>
/// Builder returned by <c>AddContactCenterSuite</c> that provides access to the
/// <see cref="IServiceCollection"/> for registering Contact Center Suite services.
/// </summary>
/// <remarks>
/// The suite is a set of pillars a host turns on one at a time. Nothing is registered by asking for the
/// suite itself beyond the host seams every pillar needs; a pillar arrives only when the host names it.
/// </remarks>
public sealed class CrestAppsContactCenterSuiteBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrestAppsContactCenterSuiteBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public CrestAppsContactCenterSuiteBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>
    /// Gets the <see cref="IServiceCollection"/> used to register Contact Center Suite services.
    /// </summary>
    public IServiceCollection Services { get; }
}

using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.Builders;

/// <summary>
/// Builder returned by <c>AddOmnichannel</c> that provides access to the
/// <see cref="IServiceCollection"/> for registering Omnichannel services.
/// </summary>
/// <remarks>
/// Each method on this builder turns on one feature of the omnichannel. A host calls the ones it wants and
/// gets nothing it did not ask for, and every one of them is sugar over an <c>AddCore*</c> method on
/// <see cref="IServiceCollection"/> that a host may call directly instead.
/// </remarks>
public sealed class CrestAppsOmnichannelBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrestAppsOmnichannelBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public CrestAppsOmnichannelBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>
    /// Gets the <see cref="IServiceCollection"/> used to register Omnichannel services.
    /// </summary>
    public IServiceCollection Services { get; }
}

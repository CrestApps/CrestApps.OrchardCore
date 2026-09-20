using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.Builders;

/// <summary>
/// Builder returned by <c>AddContactCenter</c> that provides access to the
/// <see cref="IServiceCollection"/> for registering Contact Center services.
/// </summary>
/// <remarks>
/// Each method on this builder turns on one feature of the contact centre. A host calls the ones it wants and
/// gets nothing it did not ask for, and every one of them is sugar over an <c>AddCore*</c> method on
/// <see cref="IServiceCollection"/> that a host may call directly instead. Where a feature stores its records
/// is a separate decision, made by calling one of the persistence packages' methods on this same builder.
/// </remarks>
public sealed class CrestAppsContactCenterBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrestAppsContactCenterBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public CrestAppsContactCenterBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>
    /// Gets the <see cref="IServiceCollection"/> used to register Contact Center services.
    /// </summary>
    public IServiceCollection Services { get; }
}

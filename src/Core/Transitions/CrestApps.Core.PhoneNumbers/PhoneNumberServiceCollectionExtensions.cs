using CrestApps.Core.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Core.PhoneNumbers;

/// <summary>
/// Registers phone-number parsing and formatting, independently of the host it runs in.
/// </summary>
public static class PhoneNumberServiceCollectionExtensions
{
    /// <summary>
    /// Adds phone-number parsing and formatting to the suite.
    /// </summary>
    /// <remarks>
    /// Every pillar that matches a caller to a record reads through this, so a host that turns on any of them
    /// calls this as well.
    /// </remarks>
    /// <param name="builder">The suite builder.</param>
    /// <returns>The same suite builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterSuiteBuilder AddPhoneNumbers(this CrestAppsContactCenterSuiteBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCorePhoneNumbers();

        return builder;
    }

    /// <summary>
    /// Adds the service that parses, validates and formats a phone number.
    /// </summary>
    /// <remarks>
    /// A singleton, because the metadata behind it is read once and is the same for every caller.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCorePhoneNumbers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IPhoneNumberService, DefaultPhoneNumberService>();

        return services;
    }
}

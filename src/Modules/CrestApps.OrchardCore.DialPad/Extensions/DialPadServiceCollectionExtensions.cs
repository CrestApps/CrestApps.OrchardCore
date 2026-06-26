using CrestApps.OrchardCore.DialPad;
using CrestApps.OrchardCore.DialPad.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods to register the DialPad telephony provider.
/// </summary>
public static class DialPadServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DialPad telephony provider and its HTTP client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDialPadTelephonyProvider(this IServiceCollection services)
    {
        services.AddHttpClient(DialPadConstants.ProviderTechnicalName, client =>
        {
            client.BaseAddress = new Uri(DialPadConstants.DefaultApiBaseUrl);
        });

        return services.AddTelephonyProviderOptionsConfiguration<DialPadProviderOptionsConfigurations>();
    }
}

using CrestApps.OrchardCore.Dialpad.Models;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Registers the Dialpad provider with the telephony provider options and reflects whether it is
/// enabled based on the current tenant settings.
/// </summary>
public sealed class DialpadProviderOptionsConfigurations : IConfigureOptions<TelephonyProviderOptions>
{
    private readonly IOptions<DialpadResolvedOptions> _resolvedOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadProviderOptionsConfigurations"/> class.
    /// </summary>
    /// <param name="resolvedOptions">The active Dialpad settings resolved for the tenant shell.</param>
    public DialpadProviderOptionsConfigurations(IOptions<DialpadResolvedOptions> resolvedOptions)
    {
        _resolvedOptions = resolvedOptions;
    }

    /// <inheritdoc/>
    public void Configure(TelephonyProviderOptions options)
    {
        var typeOptions = new TelephonyProviderTypeOptions(typeof(DialpadTelephonyProvider))
        {
            IsEnabled = _resolvedOptions.Value.IsEnabled,
        };

        options.TryAddProvider(DialpadConstants.ProviderTechnicalName, typeOptions);
    }
}

using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Registers the Dialpad provider with the telephony provider options and reflects whether it is
/// enabled based on the current tenant settings.
/// </summary>
public sealed class DialpadProviderOptionsConfigurations : IConfigureOptions<TelephonyProviderOptions>
{
    private readonly IOptions<DialpadOptions> _dialpadOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadProviderOptionsConfigurations"/> class.
    /// </summary>
    /// <param name="dialpadOptions">The active Dialpad settings resolved for the tenant shell.</param>
    public DialpadProviderOptionsConfigurations(IOptions<DialpadOptions> dialpadOptions)
    {
        _dialpadOptions = dialpadOptions;
    }

    /// <inheritdoc/>
    public void Configure(TelephonyProviderOptions options)
    {
        var typeOptions = new TelephonyProviderTypeOptions(typeof(DialpadTelephonyProvider))
        {
            IsEnabled = _dialpadOptions.Value.IsEnabled,
        };

        options.TryAddProvider(DialpadConstants.ProviderTechnicalName, typeOptions);
    }
}

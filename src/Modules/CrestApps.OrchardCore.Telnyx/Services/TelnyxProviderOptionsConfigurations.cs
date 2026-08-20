using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Registers the Telnyx provider with the telephony provider options and reflects whether it is enabled
/// based on the current tenant settings.
/// </summary>
public sealed class TelnyxProviderOptionsConfigurations : IConfigureOptions<TelephonyProviderOptions>
{
    private readonly IOptions<TelnyxOptions> _telnyxOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxProviderOptionsConfigurations"/> class.
    /// </summary>
    /// <param name="telnyxOptions">The active Telnyx settings resolved for the tenant shell.</param>
    public TelnyxProviderOptionsConfigurations(IOptions<TelnyxOptions> telnyxOptions)
    {
        _telnyxOptions = telnyxOptions;
    }

    /// <inheritdoc/>
    public void Configure(TelephonyProviderOptions options)
    {
        var typeOptions = new TelephonyProviderTypeOptions(typeof(TelnyxTelephonyProvider))
        {
            IsEnabled = _telnyxOptions.Value.IsEnabled,
        };

        options.TryAddProvider(TelnyxConstants.ProviderTechnicalName, typeOptions);
    }
}

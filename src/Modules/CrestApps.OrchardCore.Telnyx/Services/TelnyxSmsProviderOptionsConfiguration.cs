using Microsoft.Extensions.Options;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Gates the Telnyx SMS provider in the SMS provider list: it is only enabled (selectable as the tenant SMS
/// provider) when the resolved <see cref="TelnyxSmsOptions"/> are valid — either configured from appsettings or
/// enabled and validated through the UI settings. This mirrors how OrchardCore's Twilio provider gates itself.
/// </summary>
internal sealed class TelnyxSmsProviderOptionsConfiguration : IConfigureOptions<SmsProviderOptions>
{
    private readonly IOptionsMonitor<TelnyxSmsOptions> _options;

    public TelnyxSmsProviderOptionsConfiguration(IOptionsMonitor<TelnyxSmsOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    public void Configure(SmsProviderOptions options)
    {
        options.ReplaceProvider(TelnyxConstants.ProviderTechnicalName, new SmsProviderTypeOptions(typeof(TelnyxSmsProvider))
        {
            IsEnabled = _options.CurrentValue.IsEnabled,
        });
    }
}

using CrestApps.OrchardCore.Core.Sms;
using Microsoft.Extensions.Localization;
using OrchardCore.Infrastructure;
using CoreSms = CrestApps.Core.Sms;
using OrchardSms = OrchardCore.Sms;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Registers the Telnyx provider with Orchard Core's SMS feature, so anything that sends through
/// Orchard rather than through the Contact Center keeps working.
/// </summary>
/// <remarks>
/// A shim, not a second implementation. It declares itself through
/// <see cref="ICoreSmsProviderAdapter"/> so the framework resolver hands back the inner provider
/// instead of wrapping this again, which would hide the provider message id that Telnyx reports and
/// that later delivery receipts match on.
/// </remarks>
public sealed class OrchardCoreTelnyxSmsProvider : OrchardSms.ISmsProvider, ICoreSmsProviderAdapter
{
    private readonly TelnyxSmsProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreTelnyxSmsProvider"/> class.
    /// </summary>
    /// <param name="provider">The Telnyx provider this shim delegates to.</param>
    public OrchardCoreTelnyxSmsProvider(TelnyxSmsProvider provider)
    {
        _provider = provider;
    }

    /// <inheritdoc/>
    public LocalizedString Name => _provider.DisplayName;

    /// <inheritdoc/>
    public CoreSms.ISmsProvider InnerProvider => _provider;

    /// <inheritdoc/>
    public async Task<Result> SendAsync(OrchardSms.SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var result = await _provider.SendAsync(
            new CoreSms.SmsMessage
            {
                From = message.From,
                To = message.To,
                Body = message.Body,
            },
            cancellationToken);

        return result.Succeeded
            ? Result.Success()
            : Result.Failed([.. result.Errors.Select(error => new ResultError { Message = new LocalizedString(error, error) })]);
    }
}

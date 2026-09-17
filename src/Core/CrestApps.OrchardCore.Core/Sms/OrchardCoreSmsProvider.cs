using CrestApps.Core.Sms;
using OrchardSms = OrchardCore.Sms;

namespace CrestApps.OrchardCore.Core.Sms;

/// <summary>
/// Presents an Orchard Core SMS provider as a framework provider.
/// </summary>
public sealed class OrchardCoreSmsProvider : ISmsProvider
{
    private readonly OrchardSms.ISmsProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreSmsProvider"/> class.
    /// </summary>
    /// <param name="name">The technical name the provider is registered under.</param>
    /// <param name="provider">The Orchard Core provider.</param>
    public OrchardCoreSmsProvider(string name, OrchardSms.ISmsProvider provider)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(provider);

        Name = name;
        _provider = provider;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public async Task<SmsResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var result = await _provider.SendAsync(
            new OrchardSms.SmsMessage
            {
                From = message.From,
                To = message.To,
                Body = message.Body,
            },
            cancellationToken);

        // Orchard's result carries no provider message id, so one cannot be reported here. A provider
        // that has an id implements the framework contract directly and is resolved unwrapped.
        return result.Succeeded
            ? SmsResult.Success()
            : SmsResult.Failed((result.Errors ?? []).Select(error => error.Message?.ToString()).ToArray());
    }
}

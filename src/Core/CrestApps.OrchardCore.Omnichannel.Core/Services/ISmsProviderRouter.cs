using OrchardCore.Infrastructure;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Picks the SMS provider that owns a sending number.
/// </summary>
/// <remarks>
/// Orchard Core's <see cref="ISmsService"/> always sends through the one tenant-default provider, so a tenant whose
/// numbers span carriers would send every text through the wrong one. The provider is resolved as: the sending number's
/// SMS channel endpoint, when it names a provider, else the tenant-default SMS provider. The messaging workspace and the
/// automated SMS conversations both resolve through this, so a number sends through the same provider either way.
/// </remarks>
public interface ISmsProviderRouter
{
    /// <summary>
    /// Resolves the technical name of the provider that sends from the specified number.
    /// </summary>
    /// <param name="fromNumber">The sending number in E.164 form. Empty resolves to the tenant default.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider technical name, or <see langword="null"/> when neither the number nor the tenant names one.</returns>
    ValueTask<string> ResolveProviderNameAsync(string fromNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the provider registered under the specified technical name.
    /// </summary>
    /// <param name="providerName">The provider technical name, usually from <see cref="ResolveProviderNameAsync"/>.</param>
    /// <returns>The provider, or <see langword="null"/> when no enabled provider is registered under that name.</returns>
    Task<ISmsProvider> GetProviderAsync(string providerName);

    /// <summary>
    /// Sends the message through the provider that owns its <see cref="SmsMessage.From"/> number.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider's result, or a failure when no provider could be resolved.</returns>
    Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}

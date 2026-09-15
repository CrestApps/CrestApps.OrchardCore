using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;

/// <summary>
/// The outcome of handing one outbound message to a provider, including the provider's own identifier for it.
/// Without that identifier a delivery receipt can only be matched by guessing at the newest outbound message,
/// which is wrong whenever two messages leave in the same second.
/// </summary>
public sealed class SmsDispatchResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the provider accepted the message.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the provider's own identifier for the accepted message, when it reported one.
    /// </summary>
    public string ProviderMessageId { get; set; }

    /// <summary>
    /// Gets or sets the errors the provider reported when it refused the message.
    /// </summary>
    public IList<LocalizedString> Errors { get; set; } = [];

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="providerMessageId">The provider's identifier for the message, when known.</param>
    /// <returns>The result.</returns>
    public static SmsDispatchResult Success(string providerMessageId = null)
        => new() { Succeeded = true, ProviderMessageId = providerMessageId };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errors">The errors the provider reported.</param>
    /// <returns>The result.</returns>
    public static SmsDispatchResult Failed(params LocalizedString[] errors)
        => new() { Succeeded = false, Errors = errors ?? [] };

    /// <summary>
    /// Creates a failed result carrying one message.
    /// </summary>
    /// <param name="error">The error text.</param>
    /// <returns>The result.</returns>
    public static SmsDispatchResult Failed(string error)
        => Failed(new LocalizedString(error, error));

    /// <summary>
    /// Gets the errors joined into one line, for storing on the message bubble.
    /// </summary>
    /// <returns>The joined error text, or <see langword="null"/> when there are none.</returns>
    public string GetErrorText()
        => Errors is null || Errors.Count == 0
            ? null
            : string.Join("; ", Errors.Select(error => error.Value));
}

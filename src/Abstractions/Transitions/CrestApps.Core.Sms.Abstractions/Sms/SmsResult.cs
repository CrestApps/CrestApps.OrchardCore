namespace CrestApps.Core.Sms;

/// <summary>
/// The outcome of asking a provider to send a message.
/// </summary>
public sealed class SmsResult
{
    private SmsResult(bool succeeded, string providerMessageId, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        ProviderMessageId = providerMessageId;
        Errors = errors ?? [];
    }

    /// <summary>
    /// Gets whether the provider accepted the message.
    /// </summary>
    /// <remarks>
    /// Accepted, not delivered. Delivery is reported later, out of band.
    /// </remarks>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the provider's own identifier for the accepted message, when it reported one.
    /// </summary>
    /// <remarks>
    /// This is what lets a later delivery receipt match this exact message rather than the most
    /// recent one without an identifier, so it is carried on the result rather than discarded.
    /// </remarks>
    public string ProviderMessageId { get; }

    /// <summary>
    /// Gets why the provider refused the message.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Creates an accepted result.
    /// </summary>
    /// <param name="providerMessageId">The provider's identifier for the message, when it reported one.</param>
    /// <returns>The result.</returns>
    public static SmsResult Success(string providerMessageId = null)
        => new(true, providerMessageId, []);

    /// <summary>
    /// Creates a refused result.
    /// </summary>
    /// <param name="errors">Why the provider refused the message.</param>
    /// <returns>The result.</returns>
    public static SmsResult Failed(params string[] errors)
        => new(false, null, errors ?? []);
}

using OrchardCore.Infrastructure;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Sms.Workspace.Services;

/// <summary>
/// Sends an outbound SMS through the provider that owns the sending number.
/// </summary>
/// <remarks>
/// The built-in <see cref="ISmsService"/> only ever sends through the single tenant-default provider and
/// cannot pick a provider based on the <c>From</c> number, so a portal whose numbers span multiple carriers
/// must route the send itself. The dispatcher resolves the provider as: the <c>From</c> number's pinned
/// provider name → else the tenant-default SMS provider → then calls that specific
/// <see cref="ISmsProvider"/> directly.
/// </remarks>
public interface ISmsDispatcher
{
    /// <summary>
    /// Resolves the provider that owns the message's <c>From</c> number and sends the message through it.
    /// </summary>
    /// <param name="message">The message to send. Its <see cref="SmsMessage.From"/> selects the provider.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider send result.</returns>
    Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the technical name of the provider that would be used to send from the specified number,
    /// applying the same number-pin → tenant-default resolution as <see cref="SendAsync"/>.
    /// </summary>
    /// <param name="fromNumber">The sending number (DID) in E.164 form.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The resolved provider technical name, or <see langword="null"/> when none can be resolved.</returns>
    ValueTask<string> ResolveProviderNameAsync(string fromNumber, CancellationToken cancellationToken = default);
}

using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Provides extension shapes for the floating soft phone widget.
/// </summary>
public interface ISoftPhoneWidgetExtensionProvider
{
    /// <summary>
    /// Builds extension shapes for the floating soft phone widget.
    /// </summary>
    /// <param name="context">The extension context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task BuildAsync(SoftPhoneWidgetExtensionContext context, CancellationToken cancellationToken = default);
}

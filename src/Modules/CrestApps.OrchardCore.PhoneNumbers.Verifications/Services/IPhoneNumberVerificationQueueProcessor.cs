using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Processes queued phone number verification content items.
/// </summary>
internal interface IPhoneNumberVerificationQueueProcessor
{
    /// <summary>
    /// Verifies queued content items sequentially while applying the configured provider-request delay.
    /// </summary>
    /// <param name="contentItems">The content items to process.</param>
    /// <param name="settings">The phone number verification settings.</param>
    /// <param name="delayBeforeFirstRequest">A value indicating whether the processor should delay before the first provider request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of content items that were processed.</returns>
    Task<int> ProcessAsync(
        IEnumerable<ContentItem> contentItems,
        PhoneNumberVerificationsSettings settings,
        bool delayBeforeFirstRequest = false,
        CancellationToken cancellationToken = default);
}

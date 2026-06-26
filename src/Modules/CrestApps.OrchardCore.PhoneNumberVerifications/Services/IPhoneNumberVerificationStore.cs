using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Reads and updates the verification results stored on content items, and determines
/// whether a content item has already been verified or requires revalidation.
/// Verification records act as the authoritative cache to minimize paid provider calls.
/// </summary>
public interface IPhoneNumberVerificationStore
{
    /// <summary>
    /// Reads the stored verification result for the supplied content item.
    /// </summary>
    /// <param name="contentItem">The content item that carries the verification part.</param>
    /// <returns>The stored result, or <see langword="null"/> when none is stored.</returns>
    PhoneNumberVerificationResult Read(ContentItem contentItem);

    /// <summary>
    /// Updates the verification data on the supplied content item in place.
    /// The caller is responsible for persisting the content item.
    /// </summary>
    /// <param name="contentItem">The content item to update.</param>
    /// <param name="result">The verification result to store.</param>
    /// <param name="verifiedByUserId">The identifier of the user who triggered the verification, if any.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateAsync(
        ContentItem contentItem,
        PhoneNumberVerificationResult result,
        string verifiedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the supplied content item requires (re)validation based on the
    /// configured revalidation interval.
    /// </summary>
    /// <param name="contentItem">The content item to evaluate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when verification should be performed; otherwise, <see langword="false"/>.</returns>
    Task<bool> RequiresRevalidationAsync(ContentItem contentItem, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the supplied content item has already been verified successfully.
    /// </summary>
    /// <param name="contentItem">The content item to evaluate.</param>
    /// <returns><see langword="true"/> when the content item is currently verified; otherwise, <see langword="false"/>.</returns>
    bool IsVerified(ContentItem contentItem);
}

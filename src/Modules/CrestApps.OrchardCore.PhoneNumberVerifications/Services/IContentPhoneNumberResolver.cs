using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Resolves the phone number that should be verified for a given content item.
/// Multiple resolvers can be registered; the verification manager uses the first
/// one that returns a non-empty value.
/// </summary>
public interface IContentPhoneNumberResolver
{
    /// <summary>
    /// Resolves the phone number associated with the supplied content item.
    /// </summary>
    /// <param name="contentItem">The content item to inspect.</param>
    /// <returns>The phone number, or <see langword="null"/> when none can be resolved.</returns>
    Task<string> GetPhoneNumberAsync(ContentItem contentItem);
}

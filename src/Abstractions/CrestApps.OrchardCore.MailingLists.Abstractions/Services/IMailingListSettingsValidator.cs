using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// Checks the settings a module keeps on a mailing list (in its <c>Properties</c>), such as a provider's audience.
/// The list's own rules call every validator, so the editor and recipes check the same set whichever feature carries
/// the settings.
/// </summary>
public interface IMailingListSettingsValidator
{
    /// <summary>
    /// Checks a mailing list's settings.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>What is wrong, or nothing.</returns>
    Task<IReadOnlyList<ValidationResult>> ValidateAsync(MailingList list, CancellationToken cancellationToken = default);
}

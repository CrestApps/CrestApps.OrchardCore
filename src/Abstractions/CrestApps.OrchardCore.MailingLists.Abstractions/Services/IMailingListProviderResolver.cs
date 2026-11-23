using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// Finds the provider of a mailing list.
/// </summary>
public interface IMailingListProviderResolver
{
    /// <summary>
    /// Returns every registered provider, the Local one first.
    /// </summary>
    IReadOnlyList<IMailingListProvider> GetAll();

    /// <summary>
    /// Returns the provider with the given name, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The provider's technical name.</param>
    IMailingListProvider Find(string name);

    /// <summary>
    /// Returns the list's provider, or the Local provider when the list's provider is missing (its feature is off), for
    /// callers that only read what the provider can do, such as whether it sends its own confirmation e-mail. The
    /// provider outbox does not use the fallback: the changes of a list whose provider is missing wait until it is back.
    /// </summary>
    /// <param name="list">The list.</param>
    IMailingListProvider Resolve(MailingList list);
}

namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// Reads where a mailing list lives.
/// </summary>
public static class MailingListExtensions
{
    /// <summary>
    /// Returns the name of the list's provider, or <see cref="MailingListsConstants.LocalProviderName"/> when none is named.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <returns>The provider's technical name.</returns>
    public static string GetProviderName(this MailingList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        return string.IsNullOrWhiteSpace(list.ProviderName) ? MailingListsConstants.LocalProviderName : list.ProviderName.Trim();
    }

    /// <summary>
    /// Returns whether the list is kept on the site (its provider is the Local one, or none is named), so its member
    /// changes need no push. A list whose provider is named but not registered (its feature is off) is not kept on the
    /// site: its changes wait in the provider outbox until the provider is back.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <returns><see langword="true"/> when the list is kept on the site.</returns>
    public static bool IsLocal(this MailingList list)
        => string.Equals(GetProviderName(list), MailingListsConstants.LocalProviderName, StringComparison.OrdinalIgnoreCase);
}

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Finds the account an e-mail address belongs to by its domain, with the Account E-mail Domains feature on.
/// </summary>
public interface IAccountEmailDomains
{
    /// <summary>
    /// Returns the id of the published account that lists the address's domain, or <see langword="null"/> when none
    /// does, or more than one does (the address can't be told apart).
    /// </summary>
    /// <param name="email">The e-mail address.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The account's content item id, or <see langword="null"/>.</returns>
    Task<string> FindAccountIdAsync(string email, CancellationToken cancellationToken = default);
}

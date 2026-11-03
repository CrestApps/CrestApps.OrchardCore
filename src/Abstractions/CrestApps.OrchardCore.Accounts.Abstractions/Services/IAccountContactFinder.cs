namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Finds one of an account's contacts by email. The module that knows where a contact keeps its email registers one,
/// so a module such as Accounts B2B can link a user to the account's contact without knowing what a contact is.
/// </summary>
public interface IAccountContactFinder
{
    /// <summary>
    /// Returns the content item id of the one contact in the account with the email, or <see langword="null"/> when
    /// none or more than one has it.
    /// </summary>
    /// <param name="accountId">The account's content item id.</param>
    /// <param name="email">The email to look for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<string> FindContactByEmailAsync(string accountId, string email, CancellationToken cancellationToken = default);
}

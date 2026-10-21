namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// How a change a customer makes to their own account went.
/// </summary>
public enum CustomerAccountSaveResult
{
    /// <summary>
    /// The change was saved.
    /// </summary>
    Saved,

    /// <summary>
    /// The address to change or delete is not in the customer's address book.
    /// </summary>
    NotFound,

    /// <summary>
    /// The form has errors, which are in the model state; nothing was saved.
    /// </summary>
    Invalid,

    /// <summary>
    /// The customer was changed somewhere else at the same time; nothing was saved.
    /// </summary>
    Conflict,
}

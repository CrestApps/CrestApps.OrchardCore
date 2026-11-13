namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// What became of a change to a party's customer credit.
/// </summary>
public sealed class CustomerCreditResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the credit was changed, now or by an earlier change with the same
    /// reference.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a change with the same reference had already been made, so nothing
    /// changed now.
    /// </summary>
    public bool AlreadyApplied { get; set; }

    /// <summary>
    /// Gets or sets the balance after the change.
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Gets or sets why the credit could not be changed, when it was not.
    /// </summary>
    public CustomerCreditError Error { get; set; }

    /// <summary>
    /// Creates a refused result.
    /// </summary>
    /// <param name="error">Why.</param>
    /// <param name="balance">The balance there is.</param>
    public static CustomerCreditResult Refused(CustomerCreditError error, decimal balance = 0m)
        => new() { Error = error, Balance = balance };
}

/// <summary>
/// Why a change to a party's customer credit was refused.
/// </summary>
public enum CustomerCreditError
{
    /// <summary>
    /// Nothing was refused.
    /// </summary>
    None,

    /// <summary>
    /// The party can't hold customer credit, such as a guest, an account or a contact.
    /// </summary>
    UnsupportedParty,

    /// <summary>
    /// The amount was not greater than zero, or the currency was missing.
    /// </summary>
    InvalidAmount,

    /// <summary>
    /// The party has less credit than the amount to spend.
    /// </summary>
    InsufficientBalance,

    /// <summary>
    /// The credit is being changed by someone else; try again in a moment.
    /// </summary>
    Busy,
}

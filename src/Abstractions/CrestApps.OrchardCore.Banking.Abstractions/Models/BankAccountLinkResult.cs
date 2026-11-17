namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// The account a person linked: its numbers as the bank gave them, and the status it moves to. It is kept in memory
/// only; the account number is protected when <see cref="Services.IBankAccountService.ApplyLink"/> writes it.
/// </summary>
public sealed class BankAccountLinkResult
{
    /// <summary>
    /// Gets or sets whether the bank was linked.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets why the bank was not linked, for the person who asked.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the routing number the bank gave.
    /// </summary>
    public string RoutingNumber { get; set; }

    /// <summary>
    /// Gets or sets the account number the bank gave, in clear text.
    /// </summary>
    public string AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets whether it is a checking or a savings account, when the bank said.
    /// </summary>
    public BankAccountKind? Kind { get; set; }

    /// <summary>
    /// Gets or sets the bank's name, when known.
    /// </summary>
    public string BankName { get; set; }

    /// <summary>
    /// Gets or sets the last digits of the account as the bank shows them, for a default account name.
    /// </summary>
    public string Mask { get; set; }

    /// <summary>
    /// Gets or sets the status the account moves to, usually <see cref="BankAccountStatus.Verified"/>.
    /// </summary>
    public BankAccountStatus Status { get; set; } = BankAccountStatus.Verified;

    /// <summary>
    /// Gets or sets what the provider keeps between <see cref="Services.IBankAccountLinkProvider.CompleteAsync"/> and
    /// <see cref="Services.IBankAccountLinkProvider.FinishAsync"/>. Banking never reads it.
    /// </summary>
    public object ProviderState { get; set; }

    /// <summary>
    /// Returns a result for a bank that could not be linked.
    /// </summary>
    /// <param name="message">Why.</param>
    /// <returns>The result.</returns>
    public static BankAccountLinkResult Refused(string message)
        => new() { Succeeded = false, Message = message };
}

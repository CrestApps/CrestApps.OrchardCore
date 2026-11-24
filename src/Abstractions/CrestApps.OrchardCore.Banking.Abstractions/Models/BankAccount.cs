using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.SensitiveData;

namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// A bank account: one of the business's own, or one of a person or organization the business pays. The account
/// number is stored protected, with its last digits kept for display; write it only through
/// <see cref="Services.IBankAccountService.ApplyNumbers"/>.
/// </summary>
/// <remarks>
/// Other modules keep their own data on the account in <see cref="ExtensibleEntity.Properties"/>.
/// </remarks>
public sealed class BankAccount : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name the account is listed by, such as "Payroll funding" or "Main checking".
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets who owns the account; an empty reference for the business's own accounts.
    /// </summary>
    public PartyRef Owner { get; set; }

    /// <summary>
    /// Gets or sets the name of the account holder as the bank knows it.
    /// </summary>
    public string HolderName { get; set; }

    /// <summary>
    /// Gets or sets the kind of account, such as checking, savings or a credit card. <see cref="BankAccountKinds"/> says
    /// what each kind can be used for.
    /// </summary>
    public BankAccountKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the two-letter ISO country code of the bank, which decides how the routing number is checked.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the three-letter ISO currency code the account holds.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the bank's routing number (in the United States, the nine-digit ABA number), with spaces and dashes
    /// removed. It names the bank, not the account, and is printed on every check.
    /// </summary>
    public string RoutingNumber { get; set; }

    /// <summary>
    /// Gets or sets the account number, protected, with its last digits as the hint.
    /// </summary>
    public ProtectedValue AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the last digits (four at most) of an account number the site doesn't hold, such as for an account
    /// imported from another system that knew only them. It only tells people which account it is: nothing is paid or
    /// printed from it, and the site never makes up the rest. Storing the full number with
    /// <see cref="Services.IBankAccountService.ApplyNumbers"/> clears it.
    /// </summary>
    public string AccountNumberLastFour { get; set; }

    /// <summary>
    /// Gets or sets the bank's name.
    /// </summary>
    public string BankName { get; set; }

    /// <summary>
    /// Gets or sets where the account is in its verification.
    /// </summary>
    public BankAccountStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the status last changed.
    /// </summary>
    public DateTime? StatusChangedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the account was verified.
    /// </summary>
    public DateTime? VerifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the verifier that checked, or is checking, the account, by name; empty when someone set the status
    /// by hand.
    /// </summary>
    public string VerificationMethod { get; set; }

    /// <summary>
    /// Gets or sets why the account failed, or why its status was last changed by hand.
    /// </summary>
    public string StatusReason { get; set; }

    /// <summary>
    /// Gets or sets when the routing or account number last changed.
    /// </summary>
    public DateTime? NumberChangedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the account was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the account was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Returns whether payments can go to the account.
    /// </summary>
    /// <returns><see langword="true"/> when the account is verified.</returns>
    public bool CanReceivePayments()
        => Status == BankAccountStatus.Verified;
}

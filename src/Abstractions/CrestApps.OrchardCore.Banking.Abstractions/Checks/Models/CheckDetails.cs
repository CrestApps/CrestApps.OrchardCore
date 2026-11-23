using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// What a check waiting to print may still change: the payee, the address, the amount, the date, the memo and the
/// extension data the module the check pays keeps on it (from which its voucher is built). The bank account, the number
/// and the source never change; void the check and write a new one instead.
/// </summary>
public sealed class CheckDetails
{
    /// <summary>
    /// Gets or sets who is paid, when the site knows them as a party.
    /// </summary>
    public PartyRef Payee { get; set; }

    /// <summary>
    /// Gets or sets the payee's name as it prints.
    /// </summary>
    public string PayeeName { get; set; }

    /// <summary>
    /// Gets or sets the payee's address as it prints, or <see langword="null"/> for none.
    /// </summary>
    public Address PayeeAddress { get; set; }

    /// <summary>
    /// Gets or sets the amount; it must be more than zero.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the date printed on the check.
    /// </summary>
    public DateOnly CheckDate { get; set; }

    /// <summary>
    /// Gets or sets the memo.
    /// </summary>
    public string Memo { get; set; }

    /// <summary>
    /// Gets or sets the check's extension data, as in <see cref="CrestApps.Core.ExtensibleEntity.Properties"/>: the
    /// whole set, such as a copy of the check's properties that editors changed. <see langword="null"/> keeps the check's
    /// properties as they are.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; }

    /// <summary>
    /// Returns the details of a check as they are now, with a copy of its properties.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <returns>The details.</returns>
    public static CheckDetails From(CheckRecord check)
    {
        ArgumentNullException.ThrowIfNull(check);

        return new CheckDetails
        {
            Payee = check.Payee,
            PayeeName = check.PayeeName,
            PayeeAddress = check.PayeeAddress,
            Amount = check.Amount,
            CheckDate = check.CheckDate,
            Memo = check.Memo,
            Properties = new Dictionary<string, object>(check.Properties ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase),
        };
    }
}

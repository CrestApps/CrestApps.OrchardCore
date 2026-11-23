using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// A tax return of a registration for a period: the lines the data providers gave, the boxes its form worked out from
/// them, the amount due, and what happened to it (prepared, ready, filed, paid, amended). One return per registration,
/// form and period is current; an amendment is a new return that names the one it corrects.
/// </summary>
public sealed class TaxReturn : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the registration the return is filed under.
    /// </summary>
    public string RegistrationId { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction the return is filed with, copied from the registration.
    /// </summary>
    public string JurisdictionId { get; set; }

    /// <summary>
    /// Gets or sets the tax type the registration covers, or <see langword="null"/> for every type.
    /// </summary>
    public string TaxType { get; set; }

    /// <summary>
    /// Gets or sets the registration number, as it was when the return was prepared.
    /// </summary>
    public string RegistrationNumber { get; set; }

    /// <summary>
    /// Gets or sets the name of the return form (<c>ITaxReturnForm</c>).
    /// </summary>
    public string Form { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the return is the yearly reconciliation filed besides the periodic
    /// returns.
    /// </summary>
    public bool IsAnnualReconciliation { get; set; }

    /// <summary>
    /// Gets or sets the frequency of the period (<see cref="TaxFilingFrequencies"/>).
    /// </summary>
    public string Frequency { get; set; }

    /// <summary>
    /// Gets or sets the key of the period, such as <c>2026-10</c>, <c>2026-Q3</c> or <c>2026</c>.
    /// </summary>
    public string PeriodKey { get; set; }

    /// <summary>
    /// Gets or sets the first day of the period.
    /// </summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the last day of the period.
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the day the return is due.
    /// </summary>
    public DateOnly DueDate { get; set; }

    /// <summary>
    /// Gets or sets the currency of the amounts, or <see langword="null"/> when the providers gave none.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public TaxReturnStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the figures the data providers gave, as they were when the return was last prepared.
    /// </summary>
    public IList<TaxReturnLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the form's boxes, by their number or code, such as <c>"1a"</c> or <c>"TaxDue"</c>.
    /// </summary>
    public Dictionary<string, decimal> Boxes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the amount the return says is due, after credits, discounts, penalty and interest. Below zero, the
    /// return claims a refund or a credit.
    /// </summary>
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Gets or sets the payments recorded against the return.
    /// </summary>
    public IList<TaxReturnPayment> Payments { get; set; } = [];

    /// <summary>
    /// Gets or sets the notes the providers and the form found, such as a figure that was not available.
    /// </summary>
    public IList<string> Notes { get; set; } = [];

    /// <summary>
    /// Gets or sets who prepared the return last.
    /// </summary>
    public string PreparedBy { get; set; }

    /// <summary>
    /// Gets or sets when the return was prepared last.
    /// </summary>
    public DateTime? PreparedUtc { get; set; }

    /// <summary>
    /// Gets or sets the day the return was filed.
    /// </summary>
    public DateOnly? FiledOn { get; set; }

    /// <summary>
    /// Gets or sets who marked it as filed.
    /// </summary>
    public string FiledBy { get; set; }

    /// <summary>
    /// Gets or sets the confirmation number the agency gave.
    /// </summary>
    public string ConfirmationNumber { get; set; }

    /// <summary>
    /// Gets or sets the filing channel the return was filed through, such as an e-file transport's name, or
    /// <see langword="null"/> when it was marked as filed by hand.
    /// </summary>
    public string FilingChannel { get; set; }

    /// <summary>
    /// Gets or sets the return this one corrects, or <see langword="null"/> for an original return.
    /// </summary>
    public string AmendsId { get; set; }

    /// <summary>
    /// Gets or sets the amendment number: 0 for the original return, 1 for its first amendment.
    /// </summary>
    public int Amendment { get; set; }

    /// <summary>
    /// Gets or sets what the business paid with the returns this one corrects, counted as paid on this one.
    /// </summary>
    public decimal PaidWithEarlier { get; set; }

    /// <summary>
    /// Gets or sets when the return was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets what was paid on the return, with what was paid with the returns it corrects.
    /// </summary>
    [JsonIgnore]
    public decimal AmountPaid
        => PaidWithEarlier + Payments.Sum(payment => payment.Amount);

    /// <summary>
    /// Gets what is left to pay; below zero, more was paid than is due.
    /// </summary>
    [JsonIgnore]
    public decimal Balance
        => AmountDue - AmountPaid;

    /// <summary>
    /// Gets the period the return covers.
    /// </summary>
    [JsonIgnore]
    public TaxReturnPeriod Period
        => new(Frequency, PeriodStart, PeriodEnd);
}

/// <summary>
/// Where a return is.
/// </summary>
public enum TaxReturnStatus
{
    /// <summary>
    /// Prepared from the providers' figures and open to preparing again.
    /// </summary>
    Draft,

    /// <summary>
    /// Checked and ready to file; its figures are kept until it is filed or reopened.
    /// </summary>
    Ready,

    /// <summary>
    /// Filed with the agency; its figures never change again.
    /// </summary>
    Filed,

    /// <summary>
    /// Filed, then corrected by a later return.
    /// </summary>
    Amended,
}

/// <summary>
/// A figure of a return a data provider gave: an amount of one kind (gross sales, an exemption, tax collected, use tax
/// purchases...) for a jurisdiction, a tax type and, when the form needs it, a line of the form.
/// </summary>
public sealed class TaxReturnLine
{
    /// <summary>
    /// Gets or sets the kind of figure (<see cref="TaxReturnLineKinds"/>, or a form's own).
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction the figure is for, or <see langword="null"/> for the return's own.
    /// </summary>
    public string JurisdictionId { get; set; }

    /// <summary>
    /// Gets or sets the tax type, such as <c>SalesTax</c> or <c>UseTax</c>.
    /// </summary>
    public string TaxType { get; set; }

    /// <summary>
    /// Gets or sets the line of the form the figure goes on, such as <c>"5g"</c>, or <see langword="null"/> to leave it
    /// to the form.
    /// </summary>
    public string FormLine { get; set; }

    /// <summary>
    /// Gets or sets a tax category or an exemption reason the figure is of, such as <c>Food</c> or <c>Resale</c>.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets what people see.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the amount the tax is on (sales, purchases, wages), or the figure itself for a kind with no base.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax of the figure, such as the tax collected or the use tax owed on purchases.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the name of the provider that gave the figure.
    /// </summary>
    public string Source { get; set; }
}

/// <summary>
/// The well-known kinds of return figures. A kind is text, so a pack can add its own.
/// </summary>
public static class TaxReturnLineKinds
{
    /// <summary>
    /// Gross sales, before exemptions and deductions. The tax amount is not used.
    /// </summary>
    public const string GrossSales = "GrossSales";

    /// <summary>
    /// Sales exempt from the tax or deducted on the return, by category or reason. The tax amount is not used.
    /// </summary>
    public const string ExemptSales = "ExemptSales";

    /// <summary>
    /// Taxable sales and the tax due or collected on them.
    /// </summary>
    public const string TaxableSales = "TaxableSales";

    /// <summary>
    /// Purchases the business owes use tax on, and that use tax.
    /// </summary>
    public const string UseTaxPurchases = "UseTaxPurchases";

    /// <summary>
    /// Tax withheld from wages filed on the same return, such as a state's withholding on a combined return.
    /// </summary>
    public const string Withholding = "Withholding";

    /// <summary>
    /// Tax already paid for the period, such as prepaid sales tax or an earlier payment; it reduces what is due.
    /// </summary>
    public const string Credit = "Credit";

    /// <summary>
    /// Tax collected above what was due, which is owed too.
    /// </summary>
    public const string ExcessCollected = "ExcessCollected";
}

/// <summary>
/// A payment recorded against a return.
/// </summary>
public sealed class TaxReturnPayment
{
    /// <summary>
    /// Gets or sets the payment's id, from the id generator.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the amount paid.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the day it was paid.
    /// </summary>
    public DateOnly PaidOn { get; set; }

    /// <summary>
    /// Gets or sets the agency's confirmation or the payment's reference.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets how it was paid, such as <c>Ach</c> or <c>Check</c>, or <see langword="null"/>.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets who recorded it.
    /// </summary>
    public string RecordedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was recorded.
    /// </summary>
    public DateTime RecordedUtc { get; set; }

    /// <summary>
    /// Gets or sets data other modules keep on the payment, such as the account it was paid from.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = [];
}

/// <summary>
/// A period a registration files for, with its due date and its current return, if one was prepared. Worked out from
/// the registrations and the returns; never stored.
/// </summary>
public sealed class TaxReturnPeriodSummary
{
    /// <summary>
    /// Gets or sets the registration.
    /// </summary>
    public MerchantTaxRegistration Registration { get; set; }

    /// <summary>
    /// Gets or sets the period.
    /// </summary>
    public TaxReturnPeriod Period { get; set; }

    /// <summary>
    /// Gets or sets the form of the return.
    /// </summary>
    public string Form { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is the yearly reconciliation return.
    /// </summary>
    public bool IsAnnualReconciliation { get; set; }

    /// <summary>
    /// Gets or sets the day the return is due.
    /// </summary>
    public DateOnly DueDate { get; set; }

    /// <summary>
    /// Gets or sets the current return (the draft, ready or filed one), or <see langword="null"/> when none was
    /// prepared yet.
    /// </summary>
    public TaxReturn Return { get; set; }
}

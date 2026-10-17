namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// One way to pay a balance in installments, offered to the customer on the "Choose how to pay" checkout step, for
/// example "3 monthly payments". The total and the currency are the balance's own; an option only says how it is
/// split.
/// </summary>
public sealed class InstallmentOption
{
    /// <summary>
    /// Gets or sets the identifier of the option, unique among the options offered for one transaction. It is what
    /// the customer's choice is posted as, so it carries no amounts.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what the customer sees, for example "3 monthly payments". When empty, a description is built from
    /// the number of payments and how often they fall due.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the number of payments after the down payment.
    /// </summary>
    public int InstallmentCount { get; set; }

    /// <summary>
    /// Gets or sets how often the payments fall due.
    /// </summary>
    public InstallmentFrequency Frequency { get; set; } = InstallmentFrequency.Monthly;

    /// <summary>
    /// Gets or sets the amount taken now, before the scheduled payments. Zero, the default, means the first of the
    /// scheduled payments is taken now instead.
    /// </summary>
    public decimal DownPaymentAmount { get; set; }

    /// <summary>
    /// Gets or sets values copied onto the plan, and from it onto every payment, in addition to the reference of the
    /// transaction the plan replaces.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; }
}

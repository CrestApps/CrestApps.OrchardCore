using System.Collections.Generic;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Describes the tax status of a customer participating in a transaction.
/// </summary>
public sealed class CustomerTaxProfile
{
    /// <summary>
    /// Gets or sets the identifier of the customer.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the tax classification of the customer.
    /// </summary>
    public CustomerTaxType CustomerType { get; set; } = CustomerTaxType.B2C;

    /// <summary>
    /// Gets or sets the general tax registration number of the customer.
    /// </summary>
    public string TaxRegistrationNumber { get; set; }

    /// <summary>
    /// Gets or sets the VAT number of the customer.
    /// </summary>
    public string VatNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the customer is tax exempt.
    /// </summary>
    public bool IsTaxExempt { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the exemption certificates that belong to the customer.
    /// </summary>
    public IList<string> ExemptionCertificateIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the residential address of the customer.
    /// </summary>
    public TaxAddress ResidenceAddress { get; set; }

    /// <summary>
    /// Gets or sets the business address of the customer.
    /// </summary>
    public TaxAddress BusinessAddress { get; set; }
}

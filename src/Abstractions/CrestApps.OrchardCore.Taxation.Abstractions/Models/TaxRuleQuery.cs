using System;
using System.Collections.Generic;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Describes the criteria used to resolve applicable tax rules for a taxable item.
/// </summary>
public sealed class TaxRuleQuery
{
    /// <summary>
    /// Gets or sets the identifiers of the jurisdictions to resolve rules for.
    /// </summary>
    public IReadOnlyCollection<string> JurisdictionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the tax category code of the taxable item.
    /// </summary>
    public string CategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the tax classification code of the taxable item, which refines the category.
    /// </summary>
    public string ClassificationCode { get; set; }

    /// <summary>
    /// Gets or sets the customer classification, when a customer is known.
    /// </summary>
    public CustomerTaxType? CustomerType { get; set; }

    /// <summary>
    /// Gets or sets the transaction date, in UTC, used to select effective rules.
    /// </summary>
    public DateTime TransactionDateUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the taxable item is a shipping charge.
    /// </summary>
    public bool IsShipping { get; set; }

    /// <summary>
    /// Gets or sets the taxable base of the item, used to evaluate amount thresholds.
    /// </summary>
    public decimal TaxableAmount { get; set; }
}

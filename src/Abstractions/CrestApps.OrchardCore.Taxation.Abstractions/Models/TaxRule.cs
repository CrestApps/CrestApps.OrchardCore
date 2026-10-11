using System;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Determines whether and how a tax applies. A rule combines the jurisdiction, tax type, classification,
/// customer criteria, and a calculation method. Rules are versioned and carry effective dates so that
/// historical determinations remain reproducible.
/// </summary>
public sealed class TaxRule : SourceCatalogEntry, INameAwareModel, IModifiedUtcAwareModel, ICloneable<TaxRule>
{
    /// <inheritdoc />
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the version of the rule. The version is captured on tax lines so historical
    /// transactions can be reproduced even after the rule is changed.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the rule is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the priority of the rule. Lower values are evaluated first.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the type of tax the rule produces.
    /// </summary>
    public string TaxType { get; set; } = TaxTypeNames.SalesTax;

    /// <summary>
    /// Gets or sets the human readable name of the tax the rule produces. When left empty the tax line
    /// falls back to <see cref="Name"/>.
    /// </summary>
    public string TaxName { get; set; }

    /// <summary>
    /// Gets or sets the code of the tax the rule produces.
    /// </summary>
    public string TaxCode { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the jurisdiction the rule belongs to.
    /// </summary>
    public string JurisdictionId { get; set; }

    /// <summary>
    /// Gets or sets the tax category code the rule applies to. A <see langword="null"/> value matches
    /// every category.
    /// </summary>
    public string CategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the customer classification the rule applies to. A <see langword="null"/> value
    /// matches every customer type.
    /// </summary>
    public CustomerTaxType? CustomerType { get; set; }

    /// <summary>
    /// Gets or sets the rate applied by the rule, expressed as a fraction (for example <c>0.2</c> for 20%).
    /// </summary>
    public decimal? Rate { get; set; }

    /// <summary>
    /// Gets or sets the fixed amount applied by the rule, when the method is amount based.
    /// </summary>
    public decimal? FixedAmount { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the tax table the rule uses, when the method is table based.
    /// </summary>
    public string TaxTableId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the produced tax is included in the item price.
    /// </summary>
    public bool IncludedInPrice { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the produced tax is compound (calculated on top of other taxes).
    /// </summary>
    public bool IsCompound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the recipient accounts for the tax (reverse charge). When
    /// set and the customer matches, the rule produces a zero-amount reverse-charge line instead of
    /// charging tax, for example for EU B2B cross-border supplies.
    /// </summary>
    public bool ReverseCharge { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rule applies to shipping charges.
    /// </summary>
    public bool AppliesToShipping { get; set; }

    /// <summary>
    /// Gets or sets the inclusive minimum taxable amount the rule applies to.
    /// </summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the exclusive maximum taxable amount the rule applies to.
    /// </summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the rule becomes effective.
    /// </summary>
    public DateTime? EffectiveFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the rule stops being effective.
    /// </summary>
    public DateTime? EffectiveToUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the rule was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the name of the user that authored the rule.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user that owns the rule.
    /// </summary>
    public string OwnerId { get; set; }

    /// <inheritdoc />
    public TaxRule Clone()
    {
        return new TaxRule
        {
            ItemId = ItemId,
            Source = Source,
            Name = Name,
            Version = Version,
            Enabled = Enabled,
            Priority = Priority,
            TaxType = TaxType,
            TaxName = TaxName,
            TaxCode = TaxCode,
            JurisdictionId = JurisdictionId,
            CategoryCode = CategoryCode,
            CustomerType = CustomerType,
            Rate = Rate,
            FixedAmount = FixedAmount,
            TaxTableId = TaxTableId,
            IncludedInPrice = IncludedInPrice,
            IsCompound = IsCompound,
            ReverseCharge = ReverseCharge,
            AppliesToShipping = AppliesToShipping,
            MinimumAmount = MinimumAmount,
            MaximumAmount = MaximumAmount,
            EffectiveFromUtc = EffectiveFromUtc,
            EffectiveToUtc = EffectiveToUtc,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}

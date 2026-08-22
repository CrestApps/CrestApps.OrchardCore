using System;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents the merchant's registration (nexus) in a jurisdiction for a tax type. A jurisdiction
/// levying a tax is not enough; the merchant must be registered to be obligated to collect it.
/// </summary>
public sealed class MerchantTaxRegistration : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<MerchantTaxRegistration>
{
    /// <inheritdoc />
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the jurisdiction the merchant is registered in.
    /// </summary>
    public string JurisdictionId { get; set; }

    /// <summary>
    /// Gets or sets the tax type the registration covers.
    /// </summary>
    public string TaxType { get; set; }

    /// <summary>
    /// Gets or sets the registration number issued by the jurisdiction.
    /// </summary>
    public string RegistrationNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the registration is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the economic-nexus threshold that must be reached before the merchant is obligated
    /// to collect the tax in the jurisdiction. A <see langword="null"/> value means the obligation
    /// applies as soon as the registration is active, regardless of sales volume.
    /// </summary>
    public decimal? ThresholdAmount { get; set; }

    /// <summary>
    /// Gets or sets the accumulated sales into the jurisdiction, measured against
    /// <see cref="ThresholdAmount"/>. The value is maintained by the host as sales are recorded, so the
    /// framework can enforce economic nexus without owning a sales ledger.
    /// </summary>
    public decimal ThresholdAccumulatedAmount { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the registration was granted.
    /// </summary>
    public DateTime? RegistrationDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the registration becomes effective.
    /// </summary>
    public DateTime? EffectiveFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the registration stops being effective.
    /// </summary>
    public DateTime? EffectiveToUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <inheritdoc />
    public MerchantTaxRegistration Clone()
    {
        return new MerchantTaxRegistration
        {
            ItemId = ItemId,
            Name = Name,
            JurisdictionId = JurisdictionId,
            TaxType = TaxType,
            RegistrationNumber = RegistrationNumber,
            IsActive = IsActive,
            ThresholdAmount = ThresholdAmount,
            ThresholdAccumulatedAmount = ThresholdAccumulatedAmount,
            RegistrationDateUtc = RegistrationDateUtc,
            EffectiveFromUtc = EffectiveFromUtc,
            EffectiveToUtc = EffectiveToUtc,
            ModifiedUtc = ModifiedUtc,
        };
    }
}

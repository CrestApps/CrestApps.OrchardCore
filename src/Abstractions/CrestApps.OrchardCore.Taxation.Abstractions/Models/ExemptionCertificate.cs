using System;
using System.Collections.Generic;
using System.Linq;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents an exemption certificate that removes a customer's obligation to be charged a tax for a
/// set of jurisdictions, tax types, and classifications.
/// </summary>
public sealed class ExemptionCertificate : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<ExemptionCertificate>
{
    /// <inheritdoc />
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the certificate number.
    /// </summary>
    public string CertificateNumber { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the customer the certificate belongs to.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the exemption type (for example <c>Resale</c> or <c>Government</c>).
    /// </summary>
    public string ExemptionType { get; set; }

    /// <summary>
    /// Gets or sets the status of the certificate.
    /// </summary>
    public ExemptionStatus Status { get; set; } = ExemptionStatus.Active;

    /// <summary>
    /// Gets or sets the UTC date the certificate becomes effective.
    /// </summary>
    public DateTime? EffectiveFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the certificate expires.
    /// </summary>
    public DateTime? ExpirationUtc { get; set; }

    /// <summary>
    /// Gets or sets the tax types the certificate exempts. An empty collection exempts every tax type.
    /// </summary>
    public IList<string> TaxTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the jurisdiction identifiers the certificate applies to. An empty collection
    /// applies to every jurisdiction.
    /// </summary>
    public IList<string> JurisdictionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the classification codes the certificate applies to. An empty collection applies to
    /// every classification.
    /// </summary>
    public IList<string> ClassificationCodes { get; set; } = [];

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <inheritdoc />
    public ExemptionCertificate Clone()
    {
        return new ExemptionCertificate
        {
            ItemId = ItemId,
            Name = Name,
            CertificateNumber = CertificateNumber,
            CustomerId = CustomerId,
            ExemptionType = ExemptionType,
            Status = Status,
            EffectiveFromUtc = EffectiveFromUtc,
            ExpirationUtc = ExpirationUtc,
            TaxTypes = TaxTypes.ToList(),
            JurisdictionIds = JurisdictionIds.ToList(),
            ClassificationCodes = ClassificationCodes.ToList(),
            ModifiedUtc = ModifiedUtc,
        };
    }
}

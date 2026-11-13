using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// The party a document is billed to: who it is, and a copy of its billing details, frozen when the document is issued
/// so a later change to the party never changes an issued document.
/// </summary>
public sealed class DocumentParty
{
    /// <summary>
    /// Gets or sets the party, such as an account, a customer or a user.
    /// </summary>
    public PartyRef Party { get; set; }

    /// <summary>
    /// Gets or sets the name, e-mail address, phone and billing address the document is sent to.
    /// </summary>
    public ContactInfo Contact { get; set; }

    /// <summary>
    /// Gets or sets where goods are delivered, when it differs from the billing address.
    /// </summary>
    public Address ShippingAddress { get; set; }

    /// <summary>
    /// Gets or sets the party's tax number, such as a VAT number.
    /// </summary>
    public string TaxId { get; set; }

    /// <summary>
    /// Gets or sets how tax applies to the party: taxable, exempt, zero rated, reverse charged or out of scope.
    /// </summary>
    public TaxTreatment TaxTreatment { get; set; }

    /// <summary>
    /// Gets or sets why no tax is charged, printed on the document when the treatment is not taxable.
    /// </summary>
    public string ExemptionReason { get; set; }

    /// <summary>
    /// Gets or sets other e-mail addresses that get a copy of the documents.
    /// </summary>
    public IList<string> CopyTo { get; set; } = [];
}

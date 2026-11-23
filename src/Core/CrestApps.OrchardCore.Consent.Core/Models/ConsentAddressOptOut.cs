using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Consent.Core.Models;

/// <summary>
/// A person's request to stop one purpose of contact (such as marketing) on a channel, recorded against the address
/// rather than a customer or a contact, so it holds for whoever is sent to the address.
/// </summary>
/// <remarks>
/// Only opt-outs with a purpose are kept here. An opt-out of every purpose at an address no contact holds stays in the
/// CRM's address opt-outs for now.
/// </remarks>
public sealed class ConsentAddressOptOut : CatalogItem
{
    /// <summary>
    /// Gets or sets the document's id, which an export pages through the records in the order of.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the channel, one of <see cref="ConsentChannels"/>.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the address in its normalized form: a lower-case e-mail address, or a phone number in E.164 form.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the purpose the person stopped, such as <see cref="ConsentPurposes.Marketing"/>.
    /// </summary>
    public string Purpose { get; set; }

    /// <summary>
    /// Gets or sets what recorded the opt-out, such as <c>unsubscribe-link</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets when the opt-out was last recorded, in UTC.
    /// </summary>
    public DateTime OptedOutUtc { get; set; }
}

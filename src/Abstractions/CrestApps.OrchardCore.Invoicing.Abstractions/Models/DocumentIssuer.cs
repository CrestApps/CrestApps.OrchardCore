using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// The business that issues a document, copied from the store profile when the document is issued.
/// </summary>
public sealed class DocumentIssuer
{
    /// <summary>
    /// Gets or sets the business name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the business address.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets the business's tax number.
    /// </summary>
    public string TaxId { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address customers write to.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number customers call, as it is printed.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the business's website.
    /// </summary>
    public string Website { get; set; }

    /// <summary>
    /// Gets or sets the address of the logo printed on the document.
    /// </summary>
    public string LogoUrl { get; set; }
}

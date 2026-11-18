namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// A party printed on a business document: the issuer, the recipient or the ship-to.
/// </summary>
public sealed class BusinessDocumentParty
{
    /// <summary>
    /// Gets or sets the name of the party.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the lines printed under the name, such as the lines of an address already formatted.
    /// </summary>
    public IList<string> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the tax identification number of the party.
    /// </summary>
    public string TaxId { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address of the party.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number of the party, already formatted.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the website of the party.
    /// </summary>
    public string Website { get; set; }
}

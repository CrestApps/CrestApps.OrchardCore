using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Commerce.Stores;

/// <summary>
/// Who the store is (Settings › Commerce › Store): its name, logo, tax ID, how to reach it and where it is. Receipts, the
/// address parcels ship from and the tax origin fall back to it when their own settings are empty.
/// </summary>
public sealed class StoreProfileSettings
{
    /// <summary>
    /// Gets or sets the store's name, such as the business's legal or trading name; the site's name when empty.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the address of the store's logo, such as a media library path or an absolute URL.
    /// </summary>
    public string LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the store's tax ID, such as a VAT, GST or EIN number.
    /// </summary>
    public string TaxId { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address customers reach the store at.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number customers reach the store at, in E.164 form when it parses.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the store's address, or <see langword="null"/>.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Returns how to reach the store.
    /// </summary>
    public ContactInfo ToContactInfo()
        => ContactInfo.From(Name, Email, Phone, Address);
}

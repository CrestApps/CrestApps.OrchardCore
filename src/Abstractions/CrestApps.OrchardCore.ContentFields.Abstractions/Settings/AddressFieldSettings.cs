namespace CrestApps.OrchardCore.ContentFields.Settings;

/// <summary>
/// Stores the settings for an <see cref="Fields.AddressField"/>.
/// </summary>
public sealed class AddressFieldSettings
{
    /// <summary>
    /// Gets or sets the hint text displayed below the field.
    /// </summary>
    public string Hint { get; set; }

    /// <summary>
    /// Gets or sets whether an address must be entered.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets whether the editor asks who receives mail at the address.
    /// </summary>
    public bool ShowName { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the editor asks for a company name.
    /// </summary>
    public bool ShowCompany { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the editor asks for a phone number.
    /// </summary>
    public bool ShowPhone { get; set; }

    /// <summary>
    /// Gets or sets the codes of the countries offered, or <see langword="null"/> for every country the site serves.
    /// </summary>
    public string[] Countries { get; set; }

    /// <summary>
    /// Gets or sets the country a new address starts in.
    /// </summary>
    public string DefaultCountry { get; set; }
}

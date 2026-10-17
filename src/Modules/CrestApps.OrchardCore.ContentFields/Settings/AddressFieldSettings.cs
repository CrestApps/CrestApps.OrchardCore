namespace CrestApps.OrchardCore.ContentFields.Settings;

public sealed class AddressFieldSettings
{
    public string Hint { get; set; }

    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets whether the editor asks who receives mail at the address.
    /// </summary>
    public bool ShowName { get; set; } = true;

    public bool ShowCompany { get; set; } = true;

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

namespace CrestApps.OrchardCore.Skills.ViewModels;

/// <summary>
/// The certification editor.
/// </summary>
public class CertificationEditViewModel
{
    /// <summary>
    /// Gets or sets the certification id, or empty for a new certification.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets who issues it.
    /// </summary>
    public string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how many months a certificate stays valid; empty when it never expires.
    /// </summary>
    public int? ValidityMonths { get; set; }

    /// <summary>
    /// Gets or sets whether a copy of the certificate is needed before it counts.
    /// </summary>
    public bool RequiresDocument { get; set; }

    /// <summary>
    /// Gets or sets the reminder days, comma-separated, such as <c>60, 30, 7</c>.
    /// </summary>
    public string ReminderDays { get; set; }

    /// <summary>
    /// Gets or sets whether the certification is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

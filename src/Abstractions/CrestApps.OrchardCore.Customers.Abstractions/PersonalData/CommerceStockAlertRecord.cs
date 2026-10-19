namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// A request the person made to be emailed when a sold-out product was back in stock.
/// </summary>
public sealed class CommerceStockAlertRecord
{
    /// <summary>
    /// Gets or sets the product asked about.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the variant asked about, or <see langword="null"/>.
    /// </summary>
    public string VariantId { get; set; }

    /// <summary>
    /// Gets or sets the email address the alert goes to.
    /// </summary>
    public string Email { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the email went out, or <see langword="null"/> while it's waiting.
    /// </summary>
    public DateTime? SentUtc { get; set; }
}

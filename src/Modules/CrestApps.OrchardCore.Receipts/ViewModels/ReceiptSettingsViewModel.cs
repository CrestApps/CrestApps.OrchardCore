namespace CrestApps.OrchardCore.Receipts.ViewModels;

/// <summary>
/// View model for editing the receipt branding settings.
/// </summary>
public class ReceiptSettingsViewModel
{
    /// <summary>
    /// Gets or sets the heading printed at the top of every receipt.
    /// </summary>
    public string HeaderTitle { get; set; }

    /// <summary>
    /// Gets or sets the issuing business name. When empty, the site name is used.
    /// </summary>
    public string BusinessName { get; set; }

    /// <summary>
    /// Gets or sets the URL of the issuing business logo.
    /// </summary>
    public string LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the issuing business postal address.
    /// </summary>
    public string BusinessAddress { get; set; }

    /// <summary>
    /// Gets or sets the issuing business contact email address.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the issuing business contact phone number.
    /// </summary>
    public string ContactPhone { get; set; }

    /// <summary>
    /// Gets or sets the issuing business website.
    /// </summary>
    public string Website { get; set; }

    /// <summary>
    /// Gets or sets the footer text printed at the bottom of every receipt.
    /// </summary>
    public string FooterText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a test-payment badge is shown for test-mode payments.
    /// </summary>
    public bool ShowTestPaymentBadge { get; set; }
}

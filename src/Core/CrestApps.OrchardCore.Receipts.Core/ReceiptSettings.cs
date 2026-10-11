namespace CrestApps.OrchardCore.Receipts.Core;

/// <summary>
/// The site settings that brand every receipt the tenant issues. These are merged into each built receipt
/// document so consumers never embed issuer branding themselves.
/// </summary>
public sealed class ReceiptSettings
{
    /// <summary>
    /// Gets or sets the heading printed at the top of every receipt (for example "Payment receipt").
    /// </summary>
    public string HeaderTitle { get; set; }

    /// <summary>
    /// Gets or sets the issuing business name. When empty, the site name is used as a fallback.
    /// </summary>
    public string BusinessName { get; set; }

    /// <summary>
    /// Gets or sets the URL of the issuing business logo.
    /// </summary>
    public string LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the issuing business postal address, printed as free-form text.
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
    /// Gets or sets a value indicating whether a test-payment badge is shown when a payment ran in a
    /// gateway test mode. Defaults to <see langword="true"/>.
    /// </summary>
    public bool ShowTestPaymentBadge { get; set; } = true;
}

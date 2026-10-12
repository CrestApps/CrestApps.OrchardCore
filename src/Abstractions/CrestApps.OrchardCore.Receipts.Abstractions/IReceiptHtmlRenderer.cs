using CrestApps.OrchardCore.Receipts.Models;

namespace CrestApps.OrchardCore.Receipts;

/// <summary>
/// Renders a <see cref="ReceiptDocument"/> as a self-contained HTML fragment with inline styles, for the body of an
/// email. A mail client applies no site stylesheet and runs no script, so the printable receipt page cannot be
/// reused there.
/// </summary>
public interface IReceiptHtmlRenderer
{
    /// <summary>
    /// Renders the receipt. Every value taken from the document is HTML-encoded.
    /// </summary>
    /// <param name="receipt">The receipt to render.</param>
    string Render(ReceiptDocument receipt);
}

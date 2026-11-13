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
    /// Renders the receipt. Every value taken from the document is HTML-encoded. Prefer <see cref="RenderAsync"/>, which
    /// can also read the look of the receipt from the site, such as its document template.
    /// </summary>
    /// <param name="receipt">The receipt to render.</param>
    string Render(ReceiptDocument receipt);

    /// <summary>
    /// Renders the receipt. Every value taken from the document is HTML-encoded. An implementation that has nothing to
    /// read renders it as <see cref="Render"/> does.
    /// </summary>
    /// <param name="receipt">The receipt to render.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The HTML fragment.</returns>
    Task<string> RenderAsync(ReceiptDocument receipt, CancellationToken cancellationToken = default)
        => Task.FromResult(Render(receipt));
}

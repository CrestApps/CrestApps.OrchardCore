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
    /// Renders the receipt without reading anything from the site, so without its document template. Every value taken
    /// from the document is HTML-encoded. Call <see cref="RenderAsync"/>: an implementation whose rendering works
    /// asynchronously cannot honor this method.
    /// </summary>
    /// <param name="receipt">The receipt to render.</param>
    [Obsolete("Call RenderAsync, which applies the document template and works with every renderer.")]
    string Render(ReceiptDocument receipt);

    /// <summary>
    /// Renders the receipt with the look the site gives it, such as its document template. Every value taken from the
    /// document is HTML-encoded. An implementation that has nothing to read renders it as <see cref="Render"/> does.
    /// </summary>
    /// <param name="receipt">The receipt to render.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The HTML fragment.</returns>
#pragma warning disable CS0618 // The default implementation serves renderers written before RenderAsync existed.
    Task<string> RenderAsync(ReceiptDocument receipt, CancellationToken cancellationToken = default)
        => Task.FromResult(Render(receipt));
#pragma warning restore CS0618
}

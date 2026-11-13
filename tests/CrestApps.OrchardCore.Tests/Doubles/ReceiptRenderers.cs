using System.Text.Encodings.Web;
using CrestApps.OrchardCore.BusinessDocuments.Core.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Receipts.Core.Services;
using CrestApps.OrchardCore.Receipts.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Builds the receipt renderers as the Receipts feature wires them: the receipt mapped to a business document, given the
/// look of the receipt template, and rendered by the shared HTML renderer, with localizers that return each text as
/// written.
/// </summary>
internal static class ReceiptRenderers
{
    /// <summary>
    /// Creates the receipt mapper.
    /// </summary>
    public static ReceiptBusinessDocumentMapper CreateMapper()
        => new(new DefaultMoneyFormatter(), new PassThroughStringLocalizer<ReceiptBusinessDocumentMapper>());

    /// <summary>
    /// Creates the receipt HTML renderer.
    /// </summary>
    /// <param name="templates">The document templates, or <see langword="null"/> for none.</param>
    public static DefaultReceiptHtmlRenderer CreateHtmlRenderer(InMemoryBusinessDocumentTemplateStore templates = null)
        => new(
            CreateMapper(),
            (templates ?? new InMemoryBusinessDocumentTemplateStore()).CreateManager(),
            new BusinessDocumentHtmlRenderer(HtmlEncoder.Default, new PassThroughStringLocalizer<BusinessDocumentHtmlRenderer>()),
            NullLogger<DefaultReceiptHtmlRenderer>.Instance);
}

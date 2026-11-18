using System.Text.Encodings.Web;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.BusinessDocuments.Core.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Receipts.Core.Services;
using CrestApps.OrchardCore.Receipts.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;

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
    /// Creates the receipt HTML renderer, on a site where Business Documents is enabled.
    /// </summary>
    /// <param name="templates">The document templates, or <see langword="null"/> for none.</param>
    public static DefaultReceiptHtmlRenderer CreateHtmlRenderer(InMemoryBusinessDocumentTemplateStore templates = null)
        => new(
            CreateMapper(),
            (templates ?? new InMemoryBusinessDocumentTemplateStore()).CreateManager(),
            new BusinessDocumentHtmlRenderer(HtmlEncoder.Default, new PassThroughStringLocalizer<BusinessDocumentHtmlRenderer>()),
            CreateFeatures(BusinessDocumentsConstants.Features.Area));

    /// <summary>
    /// Creates a features manager with the given features enabled.
    /// </summary>
    /// <param name="enabled">The ids of the enabled features.</param>
    public static IShellFeaturesManager CreateFeatures(params string[] enabled)
    {
        var manager = new Mock<IShellFeaturesManager>();
        manager
            .Setup(candidate => candidate.GetEnabledFeaturesAsync())
            .ReturnsAsync(enabled.Select(id => Mock.Of<IFeatureInfo>(feature => feature.Id == id)).ToArray());

        return manager.Object;
    }
}

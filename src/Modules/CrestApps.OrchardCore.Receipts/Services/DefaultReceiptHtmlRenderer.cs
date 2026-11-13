using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Receipts.Core.Services;
using CrestApps.OrchardCore.Receipts.Models;

namespace CrestApps.OrchardCore.Receipts.Services;

/// <summary>
/// The default <see cref="IReceiptHtmlRenderer"/>. It maps the receipt to a business document, applies the document
/// template for receipts, and renders it with the shared, e-mail-safe <see cref="IBusinessDocumentHtmlRenderer"/>, so
/// receipts look like every other business document.
/// </summary>
public sealed class DefaultReceiptHtmlRenderer : IReceiptHtmlRenderer
{
    private readonly ReceiptBusinessDocumentMapper _mapper;
    private readonly IBusinessDocumentTemplateManager _templateManager;
    private readonly IBusinessDocumentHtmlRenderer _renderer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultReceiptHtmlRenderer"/> class.
    /// </summary>
    /// <param name="mapper">The mapper that turns a receipt into a business document.</param>
    /// <param name="templateManager">The manager of the document templates.</param>
    /// <param name="renderer">The business document HTML renderer.</param>
    public DefaultReceiptHtmlRenderer(
        ReceiptBusinessDocumentMapper mapper,
        IBusinessDocumentTemplateManager templateManager,
        IBusinessDocumentHtmlRenderer renderer)
    {
        _mapper = mapper;
        _templateManager = templateManager;
        _renderer = renderer;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The synchronous contract cannot read the document template, so the receipt is rendered with its own look. It
    /// never blocks on a task: a renderer that does not complete at once is refused, and <see cref="RenderAsync"/> is the
    /// way to use it.
    /// </remarks>
    public string Render(ReceiptDocument receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var rendering = _renderer.RenderAsync(_mapper.Map(receipt));

        if (!rendering.IsCompletedSuccessfully)
        {
            throw new InvalidOperationException($"The business document HTML renderer works asynchronously; call {nameof(RenderAsync)} instead.");
        }

        return rendering.Result;
    }

    /// <inheritdoc/>
    public async Task<string> RenderAsync(ReceiptDocument receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var document = _mapper.Map(receipt);

        await _templateManager.ApplyAsync(document, cancellationToken: cancellationToken);

        return await _renderer.RenderAsync(document, cancellationToken);
    }
}

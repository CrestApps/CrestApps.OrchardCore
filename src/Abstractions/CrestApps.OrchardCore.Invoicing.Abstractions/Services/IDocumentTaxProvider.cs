namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Sets the taxes of a document's lines and charges from tax rules, such as the Taxation module's, before the calculator
/// computes the amounts. Providers run in <see cref="Order"/>; the first that handles the document wins. Without one, the
/// taxes typed on the lines are used.
/// </summary>
public interface IDocumentTaxProvider
{
    /// <summary>
    /// Gets the order the provider runs in; lower runs first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Sets the taxes of the lines and charges of the context.
    /// </summary>
    /// <param name="context">The document.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the provider set the taxes; <see langword="false"/> to let the next one try.</returns>
    Task<bool> ApplyTaxesAsync(DocumentCalculationContext context, CancellationToken cancellationToken = default);
}

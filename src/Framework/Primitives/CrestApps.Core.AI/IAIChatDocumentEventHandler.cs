namespace CrestApps.Core.AI;

/// <summary>
/// Observes chat document upload and removal operations so applications can
/// persist files, index chunks, or trigger other side effects.
/// </summary>
public interface IAIChatDocumentEventHandler
{
    /// <summary>
    /// Handles a completed chat document upload operation.
    /// </summary>
    /// <param name="context">The uploaded document context.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when the handler finishes processing the upload.</returns>
    Task UploadedAsync(AIChatDocumentUploadContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a completed chat document removal operation.
    /// </summary>
    /// <param name="context">The removed document context.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when the handler finishes processing the removal.</returns>
    Task RemovedAsync(AIChatDocumentRemoveContext context, CancellationToken cancellationToken = default);
}

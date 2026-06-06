using CrestApps.OrchardCore.DncRegistry.Models;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Manages local DNC lists including import, listing, and deletion.
/// </summary>
public interface ILocalDncListManager
{
    /// <summary>
    /// Queues a CSV file for local DNC import by first saving it to tenant-local storage.
    /// </summary>
    /// <param name="name">The display name for the list.</param>
    /// <param name="countryCode">The ISO 3166-1 alpha-2 country code.</param>
    /// <param name="uploadedFileName">The original uploaded file name.</param>
    /// <param name="fileStream">The uploaded CSV file stream.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The pending <see cref="LocalDncList"/> record.</returns>
    Task<LocalDncList> QueueImportAsync(
        string name,
        string countryCode,
        string uploadedFileName,
        Stream fileStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a queued local DNC import in the background.
    /// </summary>
    /// <param name="listId">The list identifier to process.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessImportAsync(
        string listId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of local DNC lists, optionally filtered by country code.
    /// </summary>
    /// <param name="countryCode">An optional country code filter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The total number of lists.</returns>
    Task<int> GetCountAsync(
        string countryCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all local DNC lists, optionally filtered by country code.
    /// </summary>
    /// <param name="countryCode">An optional country code filter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A collection of local DNC lists.</returns>
    Task<IEnumerable<LocalDncList>> GetListsAsync(
        string countryCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of local DNC lists, optionally filtered by country code.
    /// </summary>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="countryCode">An optional country code filter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A page of local DNC lists.</returns>
    Task<IEnumerable<LocalDncList>> GetListsAsync(
        int page,
        int pageSize,
        string countryCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a local DNC list and all its phone number entries.
    /// </summary>
    /// <param name="listId">The list identifier to delete.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(string listId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a local DNC list as being deleted so the UI can reflect the pending deletion.
    /// </summary>
    /// <param name="listId">The list identifier to mark.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkAsDeletingAsync(string listId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an in-progress local DNC import so it can be resumed later.
    /// </summary>
    /// <param name="listId">The list identifier to pause.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PauseImportAsync(string listId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused or failed import by setting the status to processing and clearing
    /// any previous error. The status is committed to the database immediately so that
    /// subsequent background processing sees the updated state.
    /// </summary>
    /// <param name="listId">The list identifier to resume.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResumeImportAsync(string listId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a local DNC list by its identifier.
    /// </summary>
    /// <param name="listId">The list identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The matching list or <c>null</c>.</returns>
    Task<LocalDncList> FindByIdAsync(string listId, CancellationToken cancellationToken = default);
}

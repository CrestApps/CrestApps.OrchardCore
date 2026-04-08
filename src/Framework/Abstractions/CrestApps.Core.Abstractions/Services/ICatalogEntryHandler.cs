using CrestApps.Core.Models;

namespace CrestApps.Core.Services;

/// <summary>
/// Handles lifecycle events raised during catalog entry operations such as
/// initialization, validation, creation, update, and deletion. Implementations
/// can enrich entries, enforce business rules, or trigger side effects.
/// </summary>
/// <typeparam name="T">The type of catalog entry.</typeparam>
public interface ICatalogEntryHandler<T>
{
    /// <summary>
    /// Called when a catalog entry is being initialized with default values.
    /// </summary>
    /// <param name="context">The context containing the entry being initialized.</param>
    Task InitializingAsync(InitializingContext<T> context);

    /// <summary>
    /// Called after a catalog entry has been initialized with default values.
    /// </summary>
    /// <param name="context">The context containing the initialized entry.</param>
    Task InitializedAsync(InitializedContext<T> context);

    /// <summary>
    /// Called after a catalog entry has been loaded from the store.
    /// </summary>
    /// <param name="context">The context containing the loaded entry.</param>
    Task LoadedAsync(LoadedContext<T> context);

    /// <summary>
    /// Called when a catalog entry is about to be validated.
    /// </summary>
    /// <param name="context">The context containing the entry to validate.</param>
    Task ValidatingAsync(ValidatingContext<T> context);

    /// <summary>
    /// Called after a catalog entry has been validated.
    /// </summary>
    /// <param name="context">The context containing the validated entry and any validation results.</param>
    Task ValidatedAsync(ValidatedContext<T> context);

    /// <summary>
    /// Called when a catalog entry is about to be deleted.
    /// </summary>
    /// <param name="context">The context containing the entry to delete.</param>
    Task DeletingAsync(DeletingContext<T> context);

    /// <summary>
    /// Called after a catalog entry has been deleted.
    /// </summary>
    /// <param name="context">The context containing the deleted entry.</param>
    Task DeletedAsync(DeletedContext<T> context);

    /// <summary>
    /// Called when a catalog entry is about to be updated.
    /// </summary>
    /// <param name="context">The context containing the entry to update.</param>
    Task UpdatingAsync(UpdatingContext<T> context);

    /// <summary>
    /// Called after a catalog entry has been updated.
    /// </summary>
    /// <param name="context">The context containing the updated entry.</param>
    Task UpdatedAsync(UpdatedContext<T> context);

    /// <summary>
    /// Called when a catalog entry is about to be created.
    /// </summary>
    /// <param name="context">The context containing the entry to create.</param>
    Task CreatingAsync(CreatingContext<T> context);

    /// <summary>
    /// Called after a catalog entry has been created.
    /// </summary>
    /// <param name="context">The context containing the created entry.</param>
    Task CreatedAsync(CreatedContext<T> context);
}

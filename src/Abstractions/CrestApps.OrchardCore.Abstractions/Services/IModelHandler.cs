using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface IModelHandler<T>
{
    /// <summary>
    /// This method in invoked during model initializing.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializingContext"/>.</param>
    Task InitializingAsync(InitializingContext<T> context);

    /// <summary>
    /// This method in invoked after the model was initialized.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializedContext"/>.</param>
    Task InitializedAsync(InitializedContext<T> context);

    /// <summary>
    /// This method in invoked after the model was loaded from the store.
    /// </summary>
    /// <param name="context">An instance of <see cref="LoadedContext"/>.</param>
    Task LoadedAsync(LoadedContext<T> context);

    /// <summary>
    /// This method in invoked during model validating.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatingContext"/>.</param>
    Task ValidatingAsync(ValidatingContext<T> context);

    /// <summary>
    /// This method in invoked after the model was validated.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatedContext"/>.</param>
    Task ValidatedAsync(ValidatedContext<T> context);

    /// <summary>
    /// This method in invoked during model removing.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletingContext"/>.</param>
    Task DeletingAsync(DeletingContext<T> context);

    /// <summary>
    /// This method in invoked after the model was removed.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletedContext"/>.</param>
    Task DeletedAsync(DeletedContext<T> context);

    /// <summary>
    /// This method in invoked during model updating.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatingContext"/>.</param>
    Task UpdatingAsync(UpdatingContext<T> context);

    /// <summary>
    /// This method in invoked after the model was updated.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatedContext"/>.</param>
    Task UpdatedAsync(UpdatedContext<T> context);

    /// <summary>
    /// This method in invoked during model saving.
    /// </summary>
    /// <param name="context">An instance of <see cref="CreatingContext"/>.</param>
    Task CreatingAsync(CreatingContext<T> context);

    /// <summary>
    /// This method in invoked after the model was saved.
    /// </summary>
    /// <param name="context">An instance of <see cref="CreatedContext"/>.</param>
    Task CreatedAsync(CreatedContext<T> context);
}

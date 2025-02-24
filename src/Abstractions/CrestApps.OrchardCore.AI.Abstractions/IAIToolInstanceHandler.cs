using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIToolInstanceHandler
{
    /// <summary>
    /// This method is invoked during tool instance initializing.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializingAIToolInstanceContext"/>.</param>
    Task InitializingAsync(InitializingAIToolInstanceContext context);

    /// <summary>
    /// This method is invoked after the tool instance was initialized.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializedAIToolInstanceContext"/>.</param>
    Task InitializedAsync(InitializedAIToolInstanceContext context);

    /// <summary>
    /// This method is invoked after the tool instance was loaded from the store.
    /// </summary>
    /// <param name="context">An instance of <see cref="LoadedAIToolInstanceContext"/>.</param>
    Task LoadedAsync(LoadedAIToolInstanceContext context);

    /// <summary>
    /// This method is invoked during tool instance validating.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatingAIToolInstanceContext"/>.</param>
    Task ValidatingAsync(ValidatingAIToolInstanceContext context);

    /// <summary>
    /// This method is invoked after the tool instance was validated.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatedAIToolInstanceContext"/>.</param>
    Task ValidatedAsync(ValidatedAIToolInstanceContext context);

    /// <summary>
    /// This method is invoked during tool instance removing.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletingAIToolInstanceContext"/>.</param>
    Task DeletingAsync(DeletingAIToolInstanceContext context);

    /// <summary>
    /// This method is invoked after the tool instance was removed.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletedAIToolInstanceContext"/>.</param>
    Task DeletedAsync(DeletedAIToolInstanceContext context);

    /// <summary>
    /// This method is invoked during tool instance updating.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatingAIToolInstanceContext"/>.</param>
    Task UpdatingAsync(UpdatingAIToolInstanceContext context);

    /// <summary>
    /// This method is invoked after the tool instance was updated.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatedAIToolInstanceContext"/>.</param>
    Task UpdatedAsync(UpdatedAIToolInstanceContext context);

    /// <summary>
    /// This method is invoked during tool instance saving.
    /// </summary>
    /// <param name="context">An instance of <see cref="SavingAIToolInstanceContext"/>.</param>
    Task SavingAsync(SavingAIToolInstanceContext context);

    /// <summary>
    /// This method is invoked after the tool instance was saved.
    /// </summary>
    /// <param name="context">An instance of <see cref="SavedAIToolInstanceContext"/>.</param>
    Task SavedAsync(SavedAIToolInstanceContext context);
}

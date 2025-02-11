using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIProfileHandler
{
    /// <summary>
    /// This method in invoked during profile initializing.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializingAIProfileContext"/>.</param>
    Task InitializingAsync(InitializingAIProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was initialized.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializedAIProfileContext"/>.</param>
    Task InitializedAsync(InitializedAIProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was loaded from the store.
    /// </summary>
    /// <param name="context">An instance of <see cref="LoadedAIProfileContext"/>.</param>
    Task LoadedAsync(LoadedAIProfileContext context);

    /// <summary>
    /// This method in invoked during profile validating.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatingAIProfileContext"/>.</param>
    Task ValidatingAsync(ValidatingAIProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was validated.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatedAIProfileContext"/>.</param>
    Task ValidatedAsync(ValidatedAIProfileContext context);

    /// <summary>
    /// This method in invoked during profile removing.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletingAIProfileContext"/>.</param>
    Task DeletingAsync(DeletingAIProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was removed.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletedAIProfileContext"/>.</param>
    Task DeletedAsync(DeletedAIProfileContext context);

    /// <summary>
    /// This method in invoked during profile updating.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatingAIProfileContext"/>.</param>
    Task UpdatingAsync(UpdatingAIProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was updated.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatedAIProfileContext"/>.</param>
    Task UpdatedAsync(UpdatedAIProfileContext context);

    /// <summary>
    /// This method in invoked during profile saving.
    /// </summary>
    /// <param name="context">An instance of <see cref="SavingAIProfileContext"/>.</param>
    Task SavingAsync(SavingAIProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was saved.
    /// </summary>
    /// <param name="context">An instance of <see cref="SavedAIProfileContext"/>.</param>
    Task SavedAsync(SavedAIProfileContext context);
}

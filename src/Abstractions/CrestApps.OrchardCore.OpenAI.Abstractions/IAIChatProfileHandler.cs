using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IAIChatProfileHandler
{
    /// <summary>
    /// This method in invoked during profile initializing.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializingAIChatProfileContext"/>.</param>
    Task InitializingAsync(InitializingAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was initialized.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializedAIChatProfileContext"/>.</param>
    Task InitializedAsync(InitializedAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was loaded from the store.
    /// </summary>
    /// <param name="context">An instance of <see cref="LoadedAIChatProfileContext"/>.</param>
    Task LoadedAsync(LoadedAIChatProfileContext context);

    /// <summary>
    /// This method in invoked during profile validating.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatingAIChatProfileContext"/>.</param>
    Task ValidatingAsync(ValidatingAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was validated.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatedAIChatProfileContext"/>.</param>
    Task ValidatedAsync(ValidatedAIChatProfileContext context);

    /// <summary>
    /// This method in invoked during profile removing.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletingAIChatProfileContext"/>.</param>
    Task DeletingAsync(DeletingAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was removed.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletedAIChatProfileContext"/>.</param>
    Task DeletedAsync(DeletedAIChatProfileContext context);

    /// <summary>
    /// This method in invoked during profile updating.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatingAIChatProfileContext"/>.</param>
    Task UpdatingAsync(UpdatingAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was updated.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatedAIChatProfileContext"/>.</param>
    Task UpdatedAsync(UpdatedAIChatProfileContext context);

    /// <summary>
    /// This method in invoked during profile saving.
    /// </summary>
    /// <param name="context">An instance of <see cref="SavingAIChatProfileContext"/>.</param>
    Task SavingAsync(SavingAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was saved.
    /// </summary>
    /// <param name="context">An instance of <see cref="SavedAIChatProfileContext"/>.</param>
    Task SavedAsync(SavedAIChatProfileContext context);
}

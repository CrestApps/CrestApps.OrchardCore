using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IOpenAIChatProfileHandler
{
    /// <summary>
    /// This method in invoked during profile initializing.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializingOpenAIChatProfileContext"/>.</param>
    Task InitializingAsync(InitializingOpenAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was initialized.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializedOpenAIChatProfileContext"/>.</param>
    Task InitializedAsync(InitializedOpenAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was loaded from the store.
    /// </summary>
    /// <param name="context">An instance of <see cref="LoadedOpenAIChatProfileContext"/>.</param>
    Task LoadedAsync(LoadedOpenAIChatProfileContext context);

    /// <summary>
    /// This method in invoked during profile validating.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatingOpenAIChatProfileContext"/>.</param>
    Task ValidatingAsync(ValidatingOpenAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was validated.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatedOpenAIChatProfileContext"/>.</param>
    Task ValidatedAsync(ValidatedOpenAIChatProfileContext context);

    /// <summary>
    /// This method in invoked during profile removing.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletingOpenAIChatProfileContext"/>.</param>
    Task DeletingAsync(DeletingOpenAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was removed.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletedOpenAIChatProfileContext"/>.</param>
    Task DeletedAsync(DeletedOpenAIChatProfileContext context);

    /// <summary>
    /// This method in invoked during profile updating.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatingOpenAIChatProfileContext"/>.</param>
    Task UpdatingAsync(UpdatingOpenAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was updated.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatedOpenAIChatProfileContext"/>.</param>
    Task UpdatedAsync(UpdatedOpenAIChatProfileContext context);

    /// <summary>
    /// This method in invoked during profile saving.
    /// </summary>
    /// <param name="context">An instance of <see cref="SavingOpenAIChatProfileContext"/>.</param>
    Task SavingAsync(SavingOpenAIChatProfileContext context);

    /// <summary>
    /// This method in invoked after the profile was saved.
    /// </summary>
    /// <param name="context">An instance of <see cref="SavedOpenAIChatProfileContext"/>.</param>
    Task SavedAsync(SavedOpenAIChatProfileContext context);
}


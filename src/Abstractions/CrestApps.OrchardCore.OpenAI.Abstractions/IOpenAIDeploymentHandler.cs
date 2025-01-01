using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IOpenAIDeploymentHandler
{
    /// <summary>
    /// Asynchronously handles the initialization phase of the model deployment profile.
    /// This method is invoked during the profile initialization process.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializingOpenAIDeploymentContext"/> that contains the details of the initialization process.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializingAsync(InitializingOpenAIDeploymentContext context);

    /// <summary>
    /// Asynchronously handles the post-initialization phase of the model deployment profile.
    /// This method is invoked after the profile has been successfully initialized.
    /// </summary>
    /// <param name="context">An instance of <see cref="InitializedOpenAIDeploymentContext"/> that contains the details of the initialization.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializedAsync(InitializedOpenAIDeploymentContext context);

    /// <summary>
    /// Asynchronously handles the loading phase of the model deployment profile.
    /// This method is invoked after the profile is loaded from the store.
    /// </summary>
    /// <param name="context">An instance of <see cref="LoadedOpenAIDeploymentContext"/> that contains the details of the loaded profile.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LoadedAsync(LoadedOpenAIDeploymentContext context);

    /// <summary>
    /// Asynchronously handles the validation phase of the model deployment profile.
    /// This method is invoked during the profile validation process.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatingOpenAIDeploymentContext"/> that contains the details of the validation process.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ValidatingAsync(ValidatingOpenAIDeploymentContext context);

    /// <summary>
    /// Asynchronously handles the post-validation phase of the model deployment profile.
    /// This method is invoked after the profile has been successfully validated.
    /// </summary>
    /// <param name="context">An instance of <see cref="ValidatedModelDeploymentContext"/> that contains the details of the validated profile.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ValidatedAsync(ValidatedModelDeploymentContext context);

    /// <summary>
    /// Asynchronously handles the deletion phase of the model deployment profile.
    /// This method is invoked during the profile removal process.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletingOpenAIDeploymentContext"/> that contains the details of the deletion process.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeletingAsync(DeletingOpenAIDeploymentContext context);

    /// <summary>
    /// Asynchronously handles the post-deletion phase of the model deployment profile.
    /// This method is invoked after the profile has been successfully removed.
    /// </summary>
    /// <param name="context">An instance of <see cref="DeletedOpenAIDeploymentContext"/> that contains the details of the removed profile.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeletedAsync(DeletedOpenAIDeploymentContext context);

    /// <summary>
    /// Asynchronously handles the updating phase of the model deployment profile.
    /// This method is invoked during the profile update process.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatingModelDeploymentContext"/> that contains the details of the update process.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdatingAsync(UpdatingModelDeploymentContext context);

    /// <summary>
    /// Asynchronously handles the post-update phase of the model deployment profile.
    /// This method is invoked after the profile has been successfully updated.
    /// </summary>
    /// <param name="context">An instance of <see cref="UpdatedModelDeploymentContext"/> that contains the details of the updated profile.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdatedAsync(UpdatedModelDeploymentContext context);

    /// <summary>
    /// Asynchronously handles the saving phase of the model deployment profile.
    /// This method is invoked during the profile saving process.
    /// </summary>
    /// <param name="context">An instance of <see cref="SavingModelDeploymentContext"/> that contains the details of the saving process.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SavingAsync(SavingModelDeploymentContext context);

    /// <summary>
    /// Asynchronously handles the post-saving phase of the model deployment profile.
    /// This method is invoked after the profile has been successfully saved.
    /// </summary>
    /// <param name="context">An instance of <see cref="SavedOpenAIDeploymentContext"/> that contains the details of the saved profile.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SavedAsync(SavedOpenAIDeploymentContext context);
}


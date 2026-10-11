namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Persists and retrieves <see cref="WizardSession"/> instances. Implementations enforce session ownership
/// so an anonymous wizard session can never be resumed by a different visitor.
/// </summary>
public interface IWizardSessionStore
{
    /// <summary>
    /// Returns the session with the given id, regardless of status or owner.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching session, or <see langword="null"/> when none exists.</returns>
    Task<WizardSession> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the session with the given id and status, but only when it belongs to the current caller.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="status">The required session status.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching session, or <see langword="null"/> when none exists or ownership fails.</returns>
    Task<WizardSession> GetAsync(string sessionId, WizardSessionStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and initializes a new wizard session for the given wizard type, running the registered wizard
    /// handlers so features can contribute their steps.
    /// </summary>
    /// <param name="wizardType">The wizard type discriminator.</param>
    /// <param name="definitionId">The optional identifier of the definition the wizard is started from.</param>
    /// <param name="definitionVersionId">The optional secondary identifier of the definition the wizard is started from.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The newly created session.</returns>
    Task<WizardSession> NewAsync(string wizardType, string definitionId = null, string definitionVersionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to the session. New sessions created by <see cref="NewAsync"/> are untracked until
    /// this method is called, so it must be invoked to persist them.
    /// </summary>
    /// <param name="session">The session to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task SaveAsync(WizardSession session, CancellationToken cancellationToken = default);
}

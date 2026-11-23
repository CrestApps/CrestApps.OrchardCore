namespace CrestApps.OrchardCore.PublicForms;

/// <summary>
/// A challenge a visitor passes before a public form (one anyone may post, such as a ticket form or a newsletter
/// sign-up) is accepted, for example ReCaptcha. A form asks every registered challenge that is configured.
/// </summary>
public interface IPublicFormChallenge
{
    /// <summary>
    /// Gets whether the challenge is set up on this site; a challenge that is not set up is skipped.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Checks the answer the current request carries.
    /// </summary>
    /// <returns><see langword="true"/> when the visitor passed the challenge.</returns>
    Task<bool> VerifyAsync();
}

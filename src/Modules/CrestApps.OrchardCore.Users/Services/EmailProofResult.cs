namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// What recording the proof of an account's address did.
/// </summary>
public enum EmailProofResult
{
    /// <summary>
    /// The proof could not be saved, and nothing of it was kept.
    /// </summary>
    Failed,

    /// <summary>
    /// The address is recorded as proved. It was proved before, so nothing else changed.
    /// </summary>
    Recorded,

    /// <summary>
    /// The address is recorded as proved for the first time, and the password or external logins the account had that the
    /// request didn't show were removed. Every earlier sign-in ended, the current browser's too.
    /// </summary>
    CredentialsRemoved,

    /// <summary>
    /// The address is recorded as proved for the first time, with nothing to remove: the account had no credentials, or
    /// only the one the request showed. Every earlier sign-in ended, the current browser's too.
    /// </summary>
    SignInsEnded,
}

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
    /// The address is recorded as proved.
    /// </summary>
    Recorded,

    /// <summary>
    /// The address is recorded as proved, and the password or external logins the account had before were removed.
    /// </summary>
    CredentialsRemoved,
}

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// The outcome of checking an emailed sign-in code.
/// </summary>
public enum EmailSignInCodeCheck
{
    /// <summary>
    /// There was no code to compare with: none was sent, it expired, its tries are used up, or it could not be checked.
    /// </summary>
    NoCode,

    /// <summary>
    /// A live code was compared and did not match.
    /// </summary>
    Wrong,

    /// <summary>
    /// The code matched, and is now used up.
    /// </summary>
    Right,
}

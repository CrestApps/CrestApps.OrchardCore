namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// What the <see cref="AICallActionTask"/> can do.
/// </summary>
public static class AICallActions
{
    /// <summary>
    /// Gives the AI a note to act on.
    /// </summary>
    public const string Message = "Message";

    /// <summary>
    /// Hands the caller to an agent.
    /// </summary>
    public const string TakeOver = "TakeOver";

    /// <summary>
    /// Transfers the caller to a phone number.
    /// </summary>
    public const string TransferToNumber = "TransferToNumber";

    /// <summary>
    /// Answers with another AI profile.
    /// </summary>
    public const string SwitchProfile = "SwitchProfile";

    /// <summary>
    /// Ends the call.
    /// </summary>
    public const string End = "End";
}

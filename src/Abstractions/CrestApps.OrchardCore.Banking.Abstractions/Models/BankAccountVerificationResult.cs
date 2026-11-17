namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// What a verifier did when asked to check a bank account.
/// </summary>
public sealed class BankAccountVerificationResult
{
    /// <summary>
    /// Gets or sets whether the check was started or done.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the status the account moves to: <see cref="BankAccountStatus.VerificationSent"/> while an answer
    /// is awaited, or <see cref="BankAccountStatus.Verified"/> or <see cref="BankAccountStatus.Failed"/> when the
    /// answer came at once.
    /// </summary>
    public BankAccountStatus Status { get; set; }

    /// <summary>
    /// Gets or sets why the check could not start, or why it failed, for the person who asked.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Returns a result for a check that was started.
    /// </summary>
    /// <param name="status">The status the account moves to.</param>
    /// <returns>The result.</returns>
    public static BankAccountVerificationResult Started(BankAccountStatus status = BankAccountStatus.VerificationSent)
        => new() { Succeeded = true, Status = status };

    /// <summary>
    /// Returns a result for a check that could not start.
    /// </summary>
    /// <param name="message">Why.</param>
    /// <returns>The result.</returns>
    public static BankAccountVerificationResult Refused(string message)
        => new() { Succeeded = false, Message = message };
}

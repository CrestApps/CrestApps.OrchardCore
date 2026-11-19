namespace CrestApps.OrchardCore.Commerce.Emails;

/// <summary>
/// Whether a store's email was sent, and why not when it wasn't, so a document's delivery log can record the reason.
/// </summary>
public sealed class CommerceEmailResult
{
    private static readonly CommerceEmailResult _success = new() { Succeeded = true };

    /// <summary>
    /// Gets a value indicating whether the email was sent.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets why the email wasn't sent, such as the email provider's error, or <see langword="null"/> when it was.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Returns the result of an email that was sent.
    /// </summary>
    public static CommerceEmailResult Success()
        => _success;

    /// <summary>
    /// Returns the result of an email that wasn't sent.
    /// </summary>
    /// <param name="error">Why it wasn't sent.</param>
    public static CommerceEmailResult Failed(string error)
        => new()
        {
            Succeeded = false,
            Error = error,
        };
}

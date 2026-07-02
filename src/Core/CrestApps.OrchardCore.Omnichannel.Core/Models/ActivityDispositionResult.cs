namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the result of applying a disposition to an omnichannel activity.
/// </summary>
public sealed class ActivityDispositionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the disposition was applied successfully.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the dispositioned activity.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

    /// <summary>
    /// Gets or sets the error message when the disposition could not be applied.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful result for the specified activity.
    /// </summary>
    /// <param name="activity">The dispositioned activity.</param>
    /// <returns>The successful disposition result.</returns>
    public static ActivityDispositionResult Success(OmnichannelActivity activity)
    {
        return new ActivityDispositionResult
        {
            Succeeded = true,
            Activity = activity,
        };
    }

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The failed disposition result.</returns>
    public static ActivityDispositionResult Failure(string errorMessage)
    {
        return new ActivityDispositionResult
        {
            Succeeded = false,
            ErrorMessage = errorMessage,
        };
    }
}

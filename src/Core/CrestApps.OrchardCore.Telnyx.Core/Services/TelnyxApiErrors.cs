using System.Net;
using System.Text.Json;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Reads what a Telnyx refusal means, for the refusals that are an expected answer rather than a failure.
/// </summary>
internal static class TelnyxApiErrors
{
    /// <summary>
    /// The code Telnyx refuses a command with when the call it names is no longer active.
    /// </summary>
    public const string CallAlreadyEndedCode = "90018";

    /// <summary>
    /// The code Telnyx refuses a conference join with when the call is already a participant of that conference.
    /// </summary>
    public const string AlreadyInConferenceCode = "90044";

    /// <summary>
    /// The code Telnyx refuses a conference create with when a conference of that name already exists (seen live after a
    /// merge that failed half way).
    /// </summary>
    public const string ConferenceNameTakenCode = "90033";

    /// <summary>
    /// The code Telnyx refuses a command with when the call it names has not been answered yet.
    /// </summary>
    public const string CallNotAnsweredYetCode = "90034";

    /// <summary>
    /// Gets whether Telnyx refused the command because the call has not been answered yet.
    /// </summary>
    /// <param name="result">The refused command's result.</param>
    public static bool IsCallNotAnsweredYet(TelnyxApiResult result)
        => HasErrorCode(result, CallNotAnsweredYetCode);

    /// <summary>
    /// Gets whether Telnyx refused a conference create because a conference of that name exists.
    /// </summary>
    /// <param name="result">The refused create's result.</param>
    public static bool IsConferenceNameTaken(TelnyxApiResult result)
        => HasErrorCode(result, ConferenceNameTakenCode);

    /// <summary>
    /// Gets whether Telnyx refused the command because the call has already ended.
    /// </summary>
    /// <param name="result">The refused command's result.</param>
    /// <returns><see langword="true"/> when the refusal says the call is over.</returns>
    /// <remarks>
    /// Telnyx answers 422 with error code 90018 ("Call has already ended") to any command on a call that is gone.
    /// Only the error code is trusted, not the status alone: a 422 also means an invalid command on a live call.
    /// </remarks>
    public static bool IsCallAlreadyEnded(TelnyxApiResult result)
        => HasErrorCode(result, CallAlreadyEndedCode);

    /// <summary>
    /// Gets whether Telnyx refused a conference join because the call is already in the conference: the join has
    /// nothing left to do.
    /// </summary>
    /// <param name="result">The refused join's result.</param>
    /// <returns><see langword="true"/> when the refusal says the call already joined.</returns>
    public static bool IsAlreadyInConference(TelnyxApiResult result)
        => HasErrorCode(result, AlreadyInConferenceCode);

    // Whether a 422 refusal carries the given Telnyx error code. The status alone is not trusted: a 422 also means an
    // invalid command on a live call.
    private static bool HasErrorCode(TelnyxApiResult result, string expectedCode)
    {
        if (result is null ||
            result.Succeeded ||
            result.StatusCode != HttpStatusCode.UnprocessableEntity ||
            string.IsNullOrWhiteSpace(result.ErrorBody))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(result.ErrorBody);

            if (!document.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var error in errors.EnumerateArray())
            {
                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("code", out var code) &&
                    string.Equals(code.ToString(), expectedCode, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            // Not the error document Telnyx sends, so nothing in it can be read as that refusal.
        }

        return false;
    }
}

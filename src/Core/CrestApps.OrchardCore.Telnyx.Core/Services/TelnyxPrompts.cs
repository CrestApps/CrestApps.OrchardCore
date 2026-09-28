namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The pieces every spoken prompt and single-key collection on a Telnyx leg shares: the tenant's voice and language,
/// and a terminating key that is never one of the keys the caller is asked to press.
/// </summary>
/// <remarks>
/// See the Telnyx Call Control reference for <c>speak</c> and <c>gather_using_speak</c>
/// (https://developers.telnyx.com/api-reference/call-commands/speak-text,
/// https://developers.telnyx.com/api-reference/call-commands/gather-using-speak): both list <c>payload</c> and
/// <c>voice</c> as required, and a command without a voice is refused, which the caller hears as silence.
/// </remarks>
public static class TelnyxPrompts
{
    // The keys a prompt can use, in the order a terminating key is picked from: '#' is Telnyx's default, and a key the
    // prompt itself offers must not be the one that ends collection with nothing collected.
    private const string TerminatingDigitCandidates = "#*0987654321";

    /// <summary>
    /// Adds the tenant's voice and language to a speech command's body.
    /// </summary>
    /// <param name="body">The command body.</param>
    /// <param name="options">The tenant's Telnyx options, or <see langword="null"/> for the defaults.</param>
    public static void ApplySpeech(IDictionary<string, object> body, TelnyxOptions options)
    {
        ArgumentNullException.ThrowIfNull(body);

        body["voice"] = ResolveVoice(options);
        body["language"] = ResolveLanguage(options);
    }

    /// <summary>
    /// The voice to speak in: the tenant's, or the platform default when none is set.
    /// </summary>
    /// <param name="options">The tenant's Telnyx options.</param>
    public static string ResolveVoice(TelnyxOptions options)
        => string.IsNullOrWhiteSpace(options?.TtsVoice) ? TelnyxConstants.Speech.DefaultVoice : options.TtsVoice.Trim();

    /// <summary>
    /// The language to speak in: the tenant's, or the platform default when none is set.
    /// </summary>
    /// <param name="options">The tenant's Telnyx options.</param>
    public static string ResolveLanguage(TelnyxOptions options)
        => string.IsNullOrWhiteSpace(options?.TtsLanguage) ? TelnyxConstants.Speech.DefaultLanguage : options.TtsLanguage.Trim();

    /// <summary>
    /// A key that ends collection and is not one of the keys the caller may choose.
    /// </summary>
    /// <param name="validDigits">The keys the prompt accepts.</param>
    public static string PickTerminatingDigit(string validDigits)
    {
        foreach (var candidate in TerminatingDigitCandidates)
        {
            if (string.IsNullOrEmpty(validDigits) || !validDigits.Contains(candidate, StringComparison.Ordinal))
            {
                return candidate.ToString();
            }
        }

        // Every key is an option. One key is collected at a time, so the first press ends collection anyway.
        return "#";
    }
}

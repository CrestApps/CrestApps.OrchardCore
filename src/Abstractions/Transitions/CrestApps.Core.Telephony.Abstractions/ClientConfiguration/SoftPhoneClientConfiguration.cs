namespace CrestApps.Core.Telephony.ClientConfiguration;

/// <summary>
/// What the soft phone in the browser is told about the server it is talking to.
/// </summary>
/// <remarks>
/// <para>
/// Serialized camel-cased into the page and read once on load. Every property here is a name the
/// script reads, so removing or renaming one is a change to a published contract even though no
/// compiler sees it: the script reads each key with a fallback, so a key that stops arriving turns a
/// feature off rather than failing.
/// </para>
/// <para>
/// The three capability values are integers rather than enums on purpose. The script compares them
/// numerically, and serializing an enum would write its name instead of its number and silently stop
/// every one of those comparisons matching.
/// </para>
/// </remarks>
public sealed class SoftPhoneClientConfiguration
{
    /// <summary>
    /// The default key the soft phone saves each agent's layout under.
    /// </summary>
    /// <remarks>
    /// The script appends a suffix to this and uses the result as a browser storage key holding the
    /// agent's chosen microphone, speaker, boost, playout delay and signaling region. Changing the
    /// string does not migrate any of that; it discards it, for every agent, silently.
    /// </remarks>
    public const string DefaultStorageKey = "telephony-soft-phone";

    /// <summary>
    /// Gets or sets the URL of the telephony hub.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL the browser fetches its media registration from, or empty when none is available.
    /// </summary>
    public string RegistrationConfigUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether voicemail can be played back.
    /// </summary>
    public bool VoicemailPlaybackEnabled { get; set; }

    /// <summary>
    /// Gets or sets the voicemail media URL, with the interaction id left as a token to substitute.
    /// </summary>
    public string VoicemailMediaUrlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether voicemail can be deleted.
    /// </summary>
    public bool VoicemailDeleteEnabled { get; set; }

    /// <summary>
    /// Gets or sets the voicemail delete URL, with the interaction id left as a token to substitute.
    /// </summary>
    public string VoicemailDeleteUrlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the call-control capabilities the provider offers, as a flags value.
    /// </summary>
    public int Capabilities { get; set; }

    /// <summary>
    /// Gets or sets the audio capabilities the provider offers, as a flags value.
    /// </summary>
    public int AudioCapabilities { get; set; }

    /// <summary>
    /// Gets or sets where call audio is carried.
    /// </summary>
    public int AudioMode { get; set; }

    /// <summary>
    /// Gets or sets the name of the browser media library to load, or <see langword="null"/> for none.
    /// </summary>
    public string BrowserMediaAdapterName { get; set; }

    /// <summary>
    /// Gets or sets how many recent calls to show.
    /// </summary>
    public int RecentCallsCount { get; set; }

    /// <summary>
    /// Gets or sets where the agent goes to connect their provider account.
    /// </summary>
    public string ConnectUrl { get; set; }

    /// <summary>
    /// Gets or sets where the agent goes to disconnect their provider account.
    /// </summary>
    public string DisconnectUrl { get; set; }

    /// <summary>
    /// Gets or sets the request token the browser sends back on every post.
    /// </summary>
    public string AntiForgeryToken { get; set; }

    /// <summary>
    /// Gets or sets the key the agent's saved layout is stored under.
    /// </summary>
    public string StorageKey { get; set; } = DefaultStorageKey;

    /// <summary>
    /// Gets or sets the country code an unqualified number is dialled against.
    /// </summary>
    public string DefaultCountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the diagnostic surfaces are reachable.
    /// </summary>
    public bool EnableDiagnostics { get; set; }

    /// <summary>
    /// Gets or sets the translated strings the soft phone renders.
    /// </summary>
    /// <remarks>
    /// Filled by the host rather than by this suite. Translations are keyed on where the string was
    /// written as much as on the string itself, so moving these out of the host's own templates would
    /// leave every existing translation unreachable.
    /// </remarks>
    public IDictionary<string, string> Strings { get; set; }
}

namespace CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;

/// <summary>
/// Represents a single HTTP operation declared by the Asterisk REST Interface specification.
/// </summary>
internal sealed class AriOperation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AriOperation"/> class.
    /// </summary>
    /// <param name="pathTemplate">The specification path template, for example <c>channels/{channelId}/hold</c>.</param>
    /// <param name="httpMethod">The declared HTTP method.</param>
    /// <param name="nickname">The operation nickname declared by the specification.</param>
    public AriOperation(
        string pathTemplate,
        string httpMethod,
        string nickname)
    {
        PathTemplate = pathTemplate;
        HttpMethod = httpMethod;
        Nickname = nickname;
    }

    /// <summary>
    /// Gets the specification path template with its leading separator removed.
    /// </summary>
    public string PathTemplate { get; }

    /// <summary>
    /// Gets the declared HTTP method.
    /// </summary>
    public string HttpMethod { get; }

    /// <summary>
    /// Gets the operation nickname declared by the specification.
    /// </summary>
    public string Nickname { get; }

    /// <summary>
    /// Gets the query string parameter names the specification declares for this operation.
    /// </summary>
    public HashSet<string> QueryParameterNames { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets a diagnostic description that identifies this operation in assertion messages.
    /// </summary>
    public string Description => $"{HttpMethod} {PathTemplate} ({Nickname})";
}

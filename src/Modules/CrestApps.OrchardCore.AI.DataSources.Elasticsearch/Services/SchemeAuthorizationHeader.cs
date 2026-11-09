using Elastic.Transport;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// An <c>Authorization</c> header with any scheme, such as <c>Bearer</c> or <c>ApiKey</c>, and the credentials as given.
/// </summary>
internal sealed class SchemeAuthorizationHeader : AuthorizationHeader
{
    private readonly string _parameter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemeAuthorizationHeader"/> class.
    /// </summary>
    /// <param name="scheme">The scheme.</param>
    /// <param name="parameter">The credentials.</param>
    public SchemeAuthorizationHeader(string scheme, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);

        AuthScheme = scheme;
        _parameter = parameter;
    }

    /// <inheritdoc/>
    public override string AuthScheme { get; }

    /// <inheritdoc/>
    public override bool TryGetAuthorizationParameters(out string value)
    {
        value = _parameter;

        return !string.IsNullOrEmpty(value);
    }
}

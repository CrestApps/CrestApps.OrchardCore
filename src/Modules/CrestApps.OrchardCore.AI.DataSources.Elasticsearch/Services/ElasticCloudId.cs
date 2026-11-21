using System.Text;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// Reads the address an Elastic Cloud ID sends the client to, as Elastic.Transport's <c>CloudNodePool</c> does. The ID
/// is <c>name:base64</c>; the decoded text is <c>host[:port]$elasticsearch-id[:port]$kibana-id...</c>, and the client
/// connects to <c>https://{elasticsearch-id}.{host}</c>, on port 443 unless the ID names another one.
/// </summary>
internal static class ElasticCloudId
{
    private const string DefaultPort = "443";

    /// <summary>
    /// Returns the address the client connects to for a Cloud ID.
    /// </summary>
    /// <param name="cloudId">The Cloud ID, trimmed as the handler passes it to the client.</param>
    /// <param name="endpoint">The address, or <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the text is not a Cloud ID the client accepts.</returns>
    public static bool TryGetEndpoint(string cloudId, out Uri endpoint)
    {
        endpoint = null;

        if (string.IsNullOrWhiteSpace(cloudId))
        {
            return false;
        }

        var tokens = cloudId.Split(':', 2);

        if (tokens.Length != 2 || string.IsNullOrWhiteSpace(tokens[1]))
        {
            return false;
        }

        string decoded;

        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(tokens[1]));
        }
        catch (FormatException)
        {
            return false;
        }

        var parts = decoded.Split('$');

        if (parts.Length < 2)
        {
            return false;
        }

        var (host, hostPort) = SplitPort(parts[0].Trim(), DefaultPort);

        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var (elasticsearchId, port) = SplitPort(parts[1].Trim(), hostPort);

        if (string.IsNullOrWhiteSpace(elasticsearchId))
        {
            return false;
        }

        var address = port == DefaultPort
            ? $"https://{elasticsearchId}.{host}"
            : $"https://{elasticsearchId}.{host}:{port}";

        return Uri.TryCreate(address, UriKind.Absolute, out endpoint);
    }

    private static (string Value, string Port) SplitPort(string value, string defaultPort)
    {
        var index = value.IndexOf(':');

        return index < 0 ? (value, defaultPort) : (value[..index], value[(index + 1)..]);
    }
}

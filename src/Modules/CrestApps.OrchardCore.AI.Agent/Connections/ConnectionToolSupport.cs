using CrestApps.Core;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Agent.Connections;

/// <summary>
/// What the MCP and A2A connection tools share: the admin links, the refusals, and which profiles use a connection.
/// </summary>
internal static class ConnectionToolSupport
{
    /// <summary>
    /// The largest number of connections a delete tool takes at once.
    /// </summary>
    public const int MaxConnections = 50;

    /// <summary>
    /// Gets the address of the MCP connections page.
    /// </summary>
    public static string GetMcpConnectionsPath(IServiceProvider services)
        => AgentToolLinks.GetAdminPath(services, McpPermissions.Feature.Area, "Connections", "Index");

    /// <summary>
    /// Gets the address of the page that makes an MCP connection of a transport.
    /// </summary>
    public static string GetMcpConnectionCreatePath(IServiceProvider services, string transport)
        => AgentToolLinks.GetAdminPath(services, McpPermissions.Feature.Area, "Connections", "Create", new RouteValueDictionary { ["source"] = transport });

    /// <summary>
    /// Gets the address of an MCP connection's editor.
    /// </summary>
    public static string GetMcpConnectionEditPath(IServiceProvider services, string connectionId)
        => AgentToolLinks.GetAdminPath(services, McpPermissions.Feature.Area, "Connections", "Edit", new RouteValueDictionary { ["id"] = connectionId });

    /// <summary>
    /// Gets the address of the page that makes an A2A connection.
    /// </summary>
    public static string GetA2AConnectionCreatePath(IServiceProvider services)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.A2A, "Connections", "Create");

    /// <summary>
    /// Gets the address of an A2A connection's editor.
    /// </summary>
    public static string GetA2AConnectionEditPath(IServiceProvider services, string connectionId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.A2A, "Connections", "Edit", new RouteValueDictionary { ["id"] = connectionId });

    /// <summary>
    /// The refusal for a change to a connection the running assistant uses.
    /// </summary>
    /// <param name="what">The connection, for example "the MCP connection 'GitHub'".</param>
    /// <param name="path">The admin page where a person can make the change, or <see langword="null"/>.</param>
    /// <returns>The refusal.</returns>
    public static string RunningAssistantRefusal(string what, string path)
        => $"Refused: the assistant running this chat uses {what}. An assistant cannot change or delete what it is built from from inside a chat. A person can make this change on the admin page{(string.IsNullOrEmpty(path) ? "." : $": {path}")}";

    /// <summary>
    /// The refusal for an argument that carries a credential.
    /// </summary>
    /// <param name="argumentName">The argument.</param>
    /// <param name="path">The admin page where a person enters the credential, or <see langword="null"/>.</param>
    /// <returns>The refusal.</returns>
    public static string CredentialRefusal(string argumentName, string path)
        => $"Nothing was saved: '{argumentName}' looks like a credential, and the assistant never takes or shows credentials. A person enters it on the admin page{(string.IsNullOrEmpty(path) ? "." : $": {path}")}";

    /// <summary>
    /// The refusal for an authentication type whose credential is not stored.
    /// </summary>
    /// <param name="type">The authentication type.</param>
    /// <param name="path">The admin page where a person chooses it and enters the credential, or <see langword="null"/>.</param>
    /// <param name="isNew">Whether the connection was being made.</param>
    /// <returns>The refusal.</returns>
    public static string NeedsCredentialRefusal(string type, string path, bool isNew)
        => $"Nothing was saved: {type} authentication needs a credential, which the assistant never takes. {(isNew ? "Create the connection" : "Choose it")} on the admin page, where a person enters the credential{(string.IsNullOrEmpty(path) ? "." : $": {path}")}{(isNew ? " Or create it here with anonymous access." : string.Empty)}";

    /// <summary>
    /// Whether a text is an absolute http or https address.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns><see langword="true"/> for an absolute http or https address.</returns>
    public static bool IsWebAddress(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    /// <summary>
    /// Gets the profiles that use each MCP connection.
    /// </summary>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profiles per connection ID.</returns>
    public static async Task<Dictionary<string, List<object>>> GetMcpUsersAsync(IAIProfileManager profileManager, CancellationToken cancellationToken)
        => Index(await profileManager.GetAllAsync(cancellationToken), profile => profile.TryGet<AIProfileMcpMetadata>(out var metadata) ? metadata.ConnectionIds : null);

    /// <summary>
    /// Gets the profiles that use each A2A connection.
    /// </summary>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profiles per connection ID.</returns>
    public static async Task<Dictionary<string, List<object>>> GetA2AUsersAsync(IAIProfileManager profileManager, CancellationToken cancellationToken)
        => Index(await profileManager.GetAllAsync(cancellationToken), profile => profile.TryGet<AIProfileA2AMetadata>(out var metadata) ? metadata.ConnectionIds : null);

    private static Dictionary<string, List<object>> Index(IEnumerable<AIProfile> profiles, Func<AIProfile, string[]> connectionIds)
    {
        var users = new Dictionary<string, List<object>>(StringComparer.Ordinal);

        foreach (var profile in profiles.OrderBy(AIConfigurationGuard.GetTitle, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var connectionId in (connectionIds(profile) ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal))
            {
                if (!users.TryGetValue(connectionId, out var list))
                {
                    users[connectionId] = list = [];
                }

                list.Add(new { id = profile.ItemId, title = AIConfigurationGuard.GetTitle(profile) });
            }
        }

        return users;
    }
}

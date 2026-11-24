using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Agent.McpServer;

/// <summary>
/// What the MCP server prompt and resource tools share: the admin links.
/// </summary>
internal static class McpServerToolSupport
{
    /// <summary>
    /// The largest number of entries a delete tool takes at once.
    /// </summary>
    public const int MaxEntries = 50;

    /// <summary>
    /// The resource types that read a remote FTP or SFTP folder with credentials stored on the resource.
    /// </summary>
    public static readonly string[] RemoteResourceTypes = ["ftp", "sftp"];

    /// <summary>
    /// The resource type that reads files of the server's own file providers, the name of the AI MCP module's
    /// <c>FileResourceTypeHandler</c>, whose assembly this module does not reference.
    /// </summary>
    public const string FileResourceType = "file";

    /// <summary>
    /// The keys the FTP and SFTP resource types keep their connection under. The types live in packages this module
    /// does not reference, so the entries are read by name.
    /// </summary>
    public static readonly string[] RemoteConnectionKeys = ["FtpConnectionMetadata", "SftpConnectionMetadata"];

    /// <summary>
    /// Gets the address of a prompt's editor.
    /// </summary>
    public static string GetPromptEditPath(IServiceProvider services, string promptId)
        => AgentToolLinks.GetAdminPath(services, McpPermissions.Feature.Area, "Prompts", "Edit", new RouteValueDictionary { ["id"] = promptId });

    /// <summary>
    /// Gets the address of a resource's editor.
    /// </summary>
    public static string GetResourceEditPath(IServiceProvider services, string resourceId)
        => AgentToolLinks.GetAdminPath(services, McpPermissions.Feature.Area, "Resources", "Edit", new RouteValueDictionary { ["id"] = resourceId });

    /// <summary>
    /// Gets the address of the page that makes a resource of a type.
    /// </summary>
    public static string GetResourceCreatePath(IServiceProvider services, string type)
        => AgentToolLinks.GetAdminPath(services, McpPermissions.Feature.Area, "Resources", "Create", new RouteValueDictionary { ["source"] = type });

    /// <summary>
    /// Whether a resource type reads a remote folder with stored credentials.
    /// </summary>
    /// <param name="type">The resource type.</param>
    /// <returns><see langword="true"/> for FTP and SFTP.</returns>
    public static bool IsRemoteType(string type)
        => RemoteResourceTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
}

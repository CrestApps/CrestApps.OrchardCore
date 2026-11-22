using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// The permission each AI tool needs from the person driving the conversation. A tool runs for whoever is chatting, a
/// public visitor included, and the model can be steered by what it reads, so a tool that reads private data or changes
/// the site checks its permission against the caller (<see cref="AIToolCallerAuthorization.IsAuthorizedForToolAsync"/>)
/// before it acts, and a profile that people without the permission can use doesn't save with the tool.
/// </summary>
/// <remarks>
/// Register a tool's permission with
/// <see cref="Extensions.AIToolPermissionServiceCollectionExtensions.RequireAIToolPermission"/>.
/// </remarks>
public sealed class AIToolPermissionOptions
{
    private readonly Dictionary<string, Permission> _permissions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the permission of each tool, by the name the tool is registered under. Names are matched without regard to
    /// case, as the tool catalog matches them.
    /// </summary>
    public IReadOnlyDictionary<string, Permission> Permissions => _permissions;

    /// <summary>
    /// Sets the permission a tool needs.
    /// </summary>
    /// <param name="toolName">The name the tool is registered under.</param>
    /// <param name="permission">The permission the caller must hold.</param>
    public void Require(string toolName, Permission permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(permission);

        _permissions[toolName.Trim()] = permission;
    }

    /// <summary>
    /// Gets the permission a tool needs.
    /// </summary>
    /// <param name="toolName">The name the tool is registered under.</param>
    /// <returns>The permission, or <see langword="null"/> when none is registered.</returns>
    public Permission GetPermission(string toolName)
        => !string.IsNullOrWhiteSpace(toolName) && _permissions.TryGetValue(toolName, out var permission) ? permission : null;
}

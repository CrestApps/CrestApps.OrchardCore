using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Provides chat analytics permission functionality.
/// </summary>
public sealed class ChatAnalyticsPermissionProvider : IPermissionProvider
{
    public static readonly Permission ViewChatAnalytics = new("ViewChatAnalytics", "View AI Chat Analytics", isSecurityCritical: false);

    public static readonly Permission ExportChatAnalytics = new("ExportChatAnalytics", "Export AI Chat Analytics", isSecurityCritical: false);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ViewChatAnalytics,
        ExportChatAnalytics,
    ];

    private readonly IEnumerable<Permission> _generalPermissions =
    [
        ViewChatAnalytics,
        ExportChatAnalytics,
    ];

    /// <summary>
    /// Retrieves the permissions async.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <summary>
    /// Retrieves the default stereotypes.
    /// </summary>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
        Name = OrchardCoreConstants.Roles.Administrator,
        Permissions = _generalPermissions,
        },
    ];
}

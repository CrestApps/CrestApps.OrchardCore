using CrestApps.OrchardCore.AI.Core;
using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Provides chat analytics permission functionality.
/// </summary>
public sealed class ChatAnalyticsPermissionProvider : IPermissionProvider
{
    public static readonly Permission ViewChatAnalytics = AIPermissions.ViewChatAnalytics;

    public static readonly Permission ExportChatAnalytics = new("ExportChatAnalytics", LocalizationSource.Create<ChatAnalyticsPermissionProvider>("Export AI Chat Analytics"), isSecurityCritical: false);

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

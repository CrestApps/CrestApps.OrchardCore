using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContentTransfer;

public sealed class ContentTransferPermissions
{
    public static readonly Permission ListContentTransferEntries = new("ListContentTransferEntries", LocalizationSource.Create<ContentTransferPermissions>("List content transfer entries"));
    public static readonly Permission DeleteContentTransferEntries = new("DeleteContentTransferEntries", LocalizationSource.Create<ContentTransferPermissions>("Delete content transfer entries"), new[] { ListContentTransferEntries });

    public static readonly Permission ImportContentFromFile = new("ImportContentFromFile", LocalizationSource.Create<ContentTransferPermissions>("Import content items from file"));
    public static readonly Permission ImportContentFromFileOfType = new("ImportContentFromFile_{0}", LocalizationSource.Create<ContentTransferPermissions>("Import {0} content items from file"), new[] { ImportContentFromFile });

    public static readonly Permission ExportContentFromFile = new("ExportContentFromFile", LocalizationSource.Create<ContentTransferPermissions>("Export content items from file"));
    public static readonly Permission ExportContentFromFileOfType = new("ExportContentFromFile_{0}", LocalizationSource.Create<ContentTransferPermissions>("Export {0} content items from file"), new[] { ExportContentFromFile });

    private static Dictionary<ValueTuple<string, string>, Permission> _permissionsByType = new();

    public static Permission CreateDynamicPermission(Permission template, string contentType)
    {
        ArgumentNullException.ThrowIfNull(template);

        var key = new ValueTuple<string, string>(template.Name, contentType);

        if (_permissionsByType.TryGetValue(key, out var permission))
        {
            return permission;
        }

        // The description is the template's text with a name filled in at run time, so it cannot be extracted.
#pragma warning disable OCLOC103 // The value of the localization source is not a compile-time constant
        permission = new Permission(
            string.Format(template.Name, contentType),
            LocalizationSource.Create<ContentTransferPermissions>(string.Format(template.Description?.Value, contentType)),
            (template.ImpliedBy ?? []).Select(t => CreateDynamicPermission(t, contentType)
            )
        );
#pragma warning restore OCLOC103

        var localPermissions = new Dictionary<ValueTuple<string, string>, Permission>(_permissionsByType)
        {
            [key] = permission,
        };
        _permissionsByType = localPermissions;

        return permission;
    }
}

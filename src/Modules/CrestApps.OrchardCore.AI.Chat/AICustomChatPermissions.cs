using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat;

public static class AICustomChatPermissions
{
    public static readonly Permission ManageOwnCustomChatInstances = new("ManageOwnCustomChatInstances", "Manage own custom AI chat instances");
}

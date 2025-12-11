using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat;

public static class AICustomChatConstants
{
    public const string CollectionName = "AICustomChat";

    public static class Permissions
    {
        public static readonly Permission ManageOwnCustomChatInstances = new("ManageOwnCustomChatInstances", "Manage own custom AI chat instances");
    }
}

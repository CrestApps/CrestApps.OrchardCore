using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public static class AIChatProfilePermissions
{
    public static readonly Permission ManageAIChatProfiles = new("ManageAIChatProfiles", "Manage AI chat profiles");
}

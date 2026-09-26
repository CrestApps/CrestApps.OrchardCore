namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

internal static class AgentProfileLock
{
    public static string GetKey(string userId)
    {
        return $"ContactCenterAgentProfile:{userId}";
    }
}

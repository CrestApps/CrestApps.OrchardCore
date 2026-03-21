namespace CrestApps.OrchardCore.AI.Agent;

internal static class AgentConstants
{
    public const string DefaultSessionId = "default";
    public const string BrowserPageUrlQueryKey = "browserPageUrl";
    public const string BrowserParentPageUrlQueryKey = "browserParentPageUrl";

    public const string SessionsCategory = "Browser Sessions";
    public const string NavigationCategory = "Browser Navigation";
    public const string InspectionCategory = "Browser Inspection";
    public const string InteractionCategory = "Browser Interaction";
    public const string FormsCategory = "Browser Forms";
    public const string WaitingCategory = "Browser Waiting";
    public const string TroubleshootingCategory = "Browser Troubleshooting";

    public const int DefaultTimeoutMs = 30_000;
    public const int MaxTimeoutMs = 120_000;
    public const int DefaultMaxItems = 25;
    public const int MaxCollectionItems = 100;
    public const int MaxStoredConsoleMessages = 200;
    public const int MaxStoredNetworkEvents = 300;
    public const int DefaultMaxTextLength = 4_000;
    public static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromMinutes(30);
}

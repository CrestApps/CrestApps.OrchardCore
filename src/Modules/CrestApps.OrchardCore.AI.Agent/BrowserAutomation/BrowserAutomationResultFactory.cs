namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

internal static class BrowserAutomationResultFactory
{
    public static string Success(string action, object data)
        => BrowserAutomationJson.Serialize(data);

    public static string Failure(string action, string message)
        => message;
}

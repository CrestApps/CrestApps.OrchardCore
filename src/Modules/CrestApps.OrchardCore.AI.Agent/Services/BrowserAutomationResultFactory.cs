namespace CrestApps.OrchardCore.AI.Agent.Services;

internal static class BrowserAutomationResultFactory
{
    public static string Success(string action, object data)
        => BrowserAutomationJson.Serialize(data);

    public static string Failure(string action, string message)
        => message;
}

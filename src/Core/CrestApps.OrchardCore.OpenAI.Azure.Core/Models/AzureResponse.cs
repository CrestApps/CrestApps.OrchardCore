namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureResponse<T>
{
    public T[] Data { get; set; }
}

public class AzureModelEntry
{
    public string Id { get; set; }
}

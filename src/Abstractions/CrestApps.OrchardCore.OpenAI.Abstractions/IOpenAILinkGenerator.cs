
namespace CrestApps.OrchardCore.OpenAI;

public interface IOpenAILinkGenerator
{
    string GetContentItemPath(string contentItemId, IDictionary<string, object> metadata);
}

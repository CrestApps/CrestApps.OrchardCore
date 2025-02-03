
namespace CrestApps.OrchardCore.AI;

public interface IAILinkGenerator
{
    string GetContentItemPath(string contentItemId, IDictionary<string, object> metadata);
}

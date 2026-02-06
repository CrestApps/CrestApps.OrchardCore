using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

public interface IChatInteractionDocumentStore : ICatalog<ChatInteractionDocument>
{
    Task<IReadOnlyCollection<ChatInteractionDocument>> GetDocuments(string chatInteractionId);
}

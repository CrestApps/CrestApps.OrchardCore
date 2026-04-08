using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.EntityCore.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.Data.EntityCore.Services;

internal static class CatalogRecordFactory
{
    public static string GetEntityType<T>()
        => GetEntityType(typeof(T));

    public static string GetEntityType(Type type)
        => type.FullName ?? type.Name;

    public static CatalogRecord Create<T>(T model)
        where T : CatalogItem
    {
        ArgumentNullException.ThrowIfNull(model);

        var record = new CatalogRecord
        {
            EntityType = GetEntityType<T>(),
            ItemId = model.ItemId,
            Name = (model as INameAwareModel)?.Name,
            DisplayText = (model as IDisplayTextAwareModel)?.DisplayText,
            Source = (model as ISourceAwareModel)?.Source,
            Payload = EntityCoreStoreSerializer.Serialize(model),
        };

        switch (model)
        {
            case AIChatSessionPrompt prompt:
                record.SessionId = prompt.SessionId;
                record.CreatedUtc = prompt.CreatedUtc;
                break;
            case ChatInteractionPrompt prompt:
                record.ChatInteractionId = prompt.ChatInteractionId;
                record.CreatedUtc = prompt.CreatedUtc;
                break;
            case AIDocument document:
                record.ReferenceId = document.ReferenceId;
                record.ReferenceType = document.ReferenceType;
                record.CreatedUtc = document.UploadedUtc;
                break;
            case AIDocumentChunk chunk:
                record.AIDocumentId = chunk.AIDocumentId;
                record.ReferenceId = chunk.ReferenceId;
                record.ReferenceType = chunk.ReferenceType;
                break;
            case AIMemoryEntry memory:
                record.UserId = memory.UserId;
                record.Name = memory.Name;
                record.UpdatedUtc = memory.UpdatedUtc;
                record.CreatedUtc = memory.CreatedUtc;
                break;
            case SearchIndexProfile indexProfile:
                record.Type = indexProfile.Type;
                record.CreatedUtc = indexProfile.CreatedUtc;
                break;
        }

        return record;
    }

    public static void Update<T>(CatalogRecord record, T model)
        where T : CatalogItem
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(model);

        var updated = Create(model);

        record.Name = updated.Name;
        record.DisplayText = updated.DisplayText;
        record.Source = updated.Source;
        record.SessionId = updated.SessionId;
        record.ChatInteractionId = updated.ChatInteractionId;
        record.ReferenceId = updated.ReferenceId;
        record.ReferenceType = updated.ReferenceType;
        record.AIDocumentId = updated.AIDocumentId;
        record.UserId = updated.UserId;
        record.Type = updated.Type;
        record.CreatedUtc = updated.CreatedUtc;
        record.UpdatedUtc = updated.UpdatedUtc;
        record.Payload = updated.Payload;
    }

    public static T Materialize<T>(CatalogRecord record)
        where T : CatalogItem
    {
        ArgumentNullException.ThrowIfNull(record);

        return EntityCoreStoreSerializer.Deserialize<T>(record.Payload);
    }
}

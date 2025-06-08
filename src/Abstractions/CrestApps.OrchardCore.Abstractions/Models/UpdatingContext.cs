using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Models;

public sealed class UpdatingContext<T> : HandlerContextBase<T>
{
    public JsonNode Data { get; }

    public UpdatingContext(T model, JsonNode data)
        : base(model)
    {
        Data = data ?? new JsonObject();
    }
}

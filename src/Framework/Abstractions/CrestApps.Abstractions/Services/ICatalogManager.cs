using System.Text.Json.Nodes;
using CrestApps.Models;

namespace CrestApps.Services;

public interface ICatalogManager<T> : IReadCatalogManager<T>
{
    ValueTask<bool> DeleteAsync(T model);
    ValueTask<T> NewAsync(JsonNode data = null);
    ValueTask CreateAsync(T model);
    ValueTask UpdateAsync(T model, JsonNode data = null);
    ValueTask<ValidationResultDetails> ValidateAsync(T model);
}

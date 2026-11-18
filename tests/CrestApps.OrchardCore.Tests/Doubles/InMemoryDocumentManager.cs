using System.Text.Json;
using OrchardCore.Data.Documents;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Keeps an Orchard Core document in memory, as a copy, for services that keep their state in one (such as the pending
/// resumable steps). An update is visible to every later read.
/// </summary>
/// <typeparam name="TDocument">The document.</typeparam>
public sealed class InMemoryDocumentManager<TDocument> : IDocumentManager<TDocument>
    where TDocument : class, IDocument, new()
{
    private string _json;

    /// <inheritdoc/>
    public Task<TDocument> GetOrCreateMutableAsync(Func<Task<TDocument>> factoryAsync = null)
        => Task.FromResult(Read());

    /// <inheritdoc/>
    public Task<TDocument> GetOrCreateImmutableAsync(Func<Task<TDocument>> factoryAsync = null)
        => Task.FromResult(Read());

    /// <inheritdoc/>
    public Task UpdateAsync(TDocument document, Func<TDocument, Task> afterUpdateAsync = null)
    {
        _json = JsonSerializer.Serialize(document);

        return afterUpdateAsync is null ? Task.CompletedTask : afterUpdateAsync(document);
    }

    private TDocument Read()
        => _json is null ? new TDocument() : JsonSerializer.Deserialize<TDocument>(_json);
}

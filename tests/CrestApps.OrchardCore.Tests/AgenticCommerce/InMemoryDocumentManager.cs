using System.Text.Json;
using OrchardCore.Data.Documents;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// Keeps one document in memory: the immutable copy is the stored one, a mutable copy is a clone that only shows once
/// it is updated, as with the real document manager.
/// </summary>
/// <typeparam name="TDocument">The document's type.</typeparam>
internal sealed class InMemoryDocumentManager<TDocument> : IDocumentManager<TDocument>
    where TDocument : Document, new()
{
    public TDocument Stored { get; private set; }

    public int Updates { get; private set; }

    public async Task<TDocument> GetOrCreateMutableAsync(Func<Task<TDocument>> factory)
        => Clone(Stored) ?? await CreateAsync(factory);

    public async Task<TDocument> GetOrCreateImmutableAsync(Func<Task<TDocument>> factory)
        => Stored ?? await CreateAsync(factory);

    public Task UpdateAsync(TDocument document)
    {
        Stored = Clone(document);
        Updates++;

        return Task.CompletedTask;
    }

    public async Task UpdateAsync(TDocument document, Func<TDocument, Task> afterUpdate)
    {
        await UpdateAsync(document);

        if (afterUpdate is not null)
        {
            await afterUpdate(document);
        }
    }

    private static async Task<TDocument> CreateAsync(Func<Task<TDocument>> factory)
        => factory is null ? new TDocument() : await factory();

    private static TDocument Clone(TDocument document)
        => document is null ? null : JsonSerializer.Deserialize<TDocument>(JsonSerializer.Serialize(document));
}

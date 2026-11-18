using System.Text.Json;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// The committed state of the documents a <see cref="CommittingDocumentManager{T}"/> manages, shared by every scope.
/// </summary>
public sealed class CommittedDocuments
{
    private readonly Dictionary<Type, string> _documents = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Reads the committed copy of a document.
    /// </summary>
    /// <typeparam name="TDocument">The document.</typeparam>
    /// <returns>A copy, or a new document when none was committed.</returns>
    public TDocument Read<TDocument>()
        where TDocument : new()
    {
        lock (_lock)
        {
            return _documents.TryGetValue(typeof(TDocument), out var json) ? JsonSerializer.Deserialize<TDocument>(json) : new TDocument();
        }
    }

    /// <summary>
    /// Commits a document.
    /// </summary>
    /// <typeparam name="TDocument">The document.</typeparam>
    /// <param name="document">The document.</param>
    public void Commit<TDocument>(TDocument document)
    {
        lock (_lock)
        {
            _documents[typeof(TDocument)] = JsonSerializer.Serialize(document);
        }
    }
}

/// <summary>
/// A document manager that behaves like Orchard Core's: an immutable read returns the committed document, a mutable read
/// returns this scope's copy, and an update becomes visible to other reads only when the scope ends (Orchard Core updates
/// its cache after the transaction commits). Lets a test go through the real <c>Catalog</c> and see what a migration that
/// writes and reads back in one scope really sees.
/// </summary>
/// <typeparam name="T">The record.</typeparam>
public sealed class CommittingDocumentManager<T> : IDocumentManager<DictionaryDocument<T>>, IAsyncDisposable
    where T : CatalogItem
{
    private readonly CommittedDocuments _committed;
    private DictionaryDocument<T> _mutable;
    private DictionaryDocument<T> _pending;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommittingDocumentManager{T}"/> class.
    /// </summary>
    /// <param name="committed">The committed documents.</param>
    public CommittingDocumentManager(CommittedDocuments committed)
    {
        _committed = committed;
    }

    /// <inheritdoc/>
    public Task<DictionaryDocument<T>> GetOrCreateMutableAsync(Func<Task<DictionaryDocument<T>>> factoryAsync = null)
        => Task.FromResult(_mutable ??= _committed.Read<DictionaryDocument<T>>());

    /// <inheritdoc/>
    public Task<DictionaryDocument<T>> GetOrCreateImmutableAsync(Func<Task<DictionaryDocument<T>>> factoryAsync = null)
        => Task.FromResult(_committed.Read<DictionaryDocument<T>>());

    /// <inheritdoc/>
    public Task UpdateAsync(DictionaryDocument<T> document, Func<DictionaryDocument<T>, Task> afterUpdateAsync = null)
    {
        _pending = document;

        return afterUpdateAsync is null ? Task.CompletedTask : afterUpdateAsync(document);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_pending is not null)
        {
            _committed.Commit(_pending);
        }

        return ValueTask.CompletedTask;
    }
}

using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.RecordTools;

/// <summary>
/// The records the generic record tools saved or deleted in the current scope. Catalog stores read the records from a
/// cached document that is refreshed only once the scope commits, while every tool call of one chat message runs in
/// the same scope. Without this record, a second change to a record in the same message would start from the record as
/// it was before the first change and undo it, and a deleted record would still be listed.
/// </summary>
internal sealed class AIRecordScopeChanges
{
    private readonly Dictionary<(string Kind, string Id), CatalogItem> _saved = [];
    private readonly HashSet<(string Kind, string Id)> _deleted = [];

    /// <summary>
    /// Gets the record of the scope of a tool invocation.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The record, or a new empty one when none is registered.</returns>
    public static AIRecordScopeChanges Get(IServiceProvider services)
        => services.GetService<AIRecordScopeChanges>() ?? new AIRecordScopeChanges();

    /// <summary>
    /// Finds a record as it is after the changes of this scope.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="kind">The record kind.</param>
    /// <param name="id">The ID of the record.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The record, or <see langword="null"/> when there is none or it was deleted in this scope.</returns>
    public async ValueTask<T> FindAsync<T>(IServiceProvider services, AIRecordKind<T> kind, string id, CancellationToken cancellationToken)
        where T : CatalogItem, new()
    {
        var key = (kind.Name, id);

        if (_deleted.Contains(key))
        {
            return null;
        }

        // The record saved in this scope is the instance the next change starts from, as the store would hand it out
        // after the commit; callers copy it before they change anything they may not save.
        if (_saved.TryGetValue(key, out var saved))
        {
            return (T)saved;
        }

        return await kind.FindAsync(services, id, cancellationToken);
    }

    /// <summary>
    /// Gets a listed record as it is after the changes of this scope.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="kind">The record kind.</param>
    /// <param name="listed">The record as the store listed it.</param>
    /// <returns>The record, or <see langword="null"/> when it was deleted in this scope.</returns>
    public T Apply<T>(AIRecordKind<T> kind, T listed)
        where T : CatalogItem, new()
    {
        if (listed is null || string.IsNullOrEmpty(listed.ItemId))
        {
            return listed;
        }

        var key = (kind.Name, listed.ItemId);

        if (_deleted.Contains(key))
        {
            return null;
        }

        return _saved.TryGetValue(key, out var saved)
            ? (T)saved
            : listed;
    }

    /// <summary>
    /// Records a saved record.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="kind">The record kind.</param>
    /// <param name="record">The record as it was saved.</param>
    public void Saved<T>(AIRecordKind<T> kind, T record)
        where T : CatalogItem, new()
    {
        if (string.IsNullOrEmpty(record?.ItemId))
        {
            return;
        }

        var key = (kind.Name, record.ItemId);

        _saved[key] = record;
        _deleted.Remove(key);
    }

    /// <summary>
    /// Records a deleted record.
    /// </summary>
    /// <param name="kind">The record kind.</param>
    /// <param name="id">The ID of the record.</param>
    public void Deleted(AIRecordKind kind, string id)
    {
        var key = (kind.Name, id);

        _saved.Remove(key);
        _deleted.Add(key);
    }
}

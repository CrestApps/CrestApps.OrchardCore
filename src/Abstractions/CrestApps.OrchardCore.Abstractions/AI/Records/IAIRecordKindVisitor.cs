using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AI.Records;

/// <summary>
/// Works with a record kind through its record type: <see cref="AIRecordKind.Accept{TResult}"/> calls
/// <see cref="Visit{T}"/> with the typed kind.
/// </summary>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface IAIRecordKindVisitor<TResult>
{
    /// <summary>
    /// Works with the typed kind.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="kind">The kind.</param>
    /// <returns>The result.</returns>
    TResult Visit<T>(AIRecordKind<T> kind)
        where T : CatalogItem, new();
}

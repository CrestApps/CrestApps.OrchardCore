using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Tests.Agent.AIKnowledge;

/// <summary>
/// Protects the data sources it is given, as the AI Agent module's guard does for the running assistant's.
/// </summary>
internal sealed class FakeRunningAssistantGuard : CrestApps.OrchardCore.AI.Core.IAIRunningAssistantGuard
{
    public HashSet<string> ProtectedDataSourceIds { get; } = new(StringComparer.Ordinal);

    public Task<bool> IsProtectedDataSourceAsync(string dataSourceId, CancellationToken cancellationToken = default)
        => Task.FromResult(dataSourceId is not null && ProtectedDataSourceIds.Contains(dataSourceId));
}

/// <summary>
/// An in-memory catalog of records kept by source, such as data sources, file sources and web crawlers, that records what
/// the tools create, update and delete. The catalog handlers do not run; <see cref="Validate"/> stands in for them.
/// </summary>
internal sealed class FakeSourceCatalogManager<T> : ISourceCatalogManager<T>
    where T : SourceCatalogEntry, new()
{
    private int _nextId;

    public List<T> Items { get; } = [];

    public List<T> Created { get; } = [];

    public List<T> Updated { get; } = [];

    public List<T> Deleted { get; } = [];

    public Func<T, ValidationResultDetails> Validate { get; set; } = _ => new ValidationResultDetails();

    public T Add(T item)
    {
        Items.Add(item);

        return item;
    }

    public ValueTask<T> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Items.FirstOrDefault(item => string.Equals(item.ItemId, id, StringComparison.Ordinal)));

    public ValueTask<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IEnumerable<T>>(Items.ToList());

    public ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        var matches = Items
            .Where(item => string.IsNullOrEmpty(context?.Source) || string.Equals(item.Source, context.Source, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrEmpty(context?.Name) || (item as IDisplayTextAwareModel)?.DisplayText?.Contains(context.Name, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        return ValueTask.FromResult(new PageResult<T>
        {
            Count = matches.Count,
            Entries = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        });
    }

    public ValueTask<bool> DeleteAsync(T model, CancellationToken cancellationToken = default)
    {
        Deleted.Add(model);

        return ValueTask.FromResult(Items.Remove(model));
    }

    public ValueTask CreateAsync(T model, CancellationToken cancellationToken = default)
    {
        Created.Add(model);
        Items.Add(model);

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(T model, JsonNode data = null, CancellationToken cancellationToken = default)
    {
        Updated.Add(model);

        return ValueTask.CompletedTask;
    }

    public ValueTask<ValidationResultDetails> ValidateAsync(T model, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Validate(model));

    public ValueTask<T> NewAsync(string source, JsonNode data = null, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new T
        {
            ItemId = $"new-{++_nextId}",
            Source = source,
        });

    public ValueTask<IEnumerable<T>> GetAsync(string source, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IEnumerable<T>>(Items.Where(item => string.Equals(item.Source, source, StringComparison.OrdinalIgnoreCase)).ToList());

    public ValueTask<IEnumerable<T>> FindBySourceAsync(string source, CancellationToken cancellationToken = default)
        => GetAsync(source, cancellationToken);
}

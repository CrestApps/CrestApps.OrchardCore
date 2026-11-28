using OrchardCore.Queries;

namespace CrestApps.OrchardCore.Tests.Agent.Queries;

/// <summary>
/// Stands in for an enabled query source in a tool test. The tools only read its name; queries are run by the query
/// manager.
/// </summary>
internal sealed class FakeQuerySource : IQuerySource
{
    public FakeQuerySource(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public Task<IQueryResults> ExecuteQueryAsync(Query query, IDictionary<string, object> parameters)
        => throw new NotSupportedException("A tool runs a query through the query manager.");
}

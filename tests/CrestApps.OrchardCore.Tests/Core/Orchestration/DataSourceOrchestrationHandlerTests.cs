using CrestApps.AI;
using CrestApps.AI.Handlers;
using CrestApps.AI.Models;
using CrestApps.AI.Tooling;
using CrestApps.Templates.Models;
using CrestApps.Templates.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class DataSourceOrchestrationHandlerTests
{
    [Fact]
    public async Task BuiltAsync_WithConfiguredDataSource_AddsAvailabilityInstructions()
    {
        var handler = CreateHandler();
        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext
            {
                DataSourceId = "ds1",
            },
        };

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new AIProfile(), context));

        var systemMessage = context.SystemMessageBuilder.ToString();
        Assert.Contains("[Configured Data Source]", systemMessage);
        Assert.Contains(SystemToolNames.SearchDataSources, systemMessage);
        Assert.Contains(SystemToolNames.SearchDataSources, context.MustIncludeTools);
    }

    [Fact]
    public async Task BuiltAsync_WithoutConfiguredDataSource_DoesNothing()
    {
        var handler = CreateHandler();
        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext(),
        };

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new AIProfile(), context));

        Assert.Equal(string.Empty, context.SystemMessageBuilder.ToString());
        Assert.Empty(context.MustIncludeTools);
    }

    private static DataSourceOrchestrationHandler CreateHandler()
    {
        var toolOptions = new AIToolDefinitionOptions();
        toolOptions.SetTool(SystemToolNames.SearchDataSources, new AIToolDefinitionEntry(typeof(object))
        {
            Name = SystemToolNames.SearchDataSources,
            Description = "Searches configured data sources.",
            Purpose = AIToolPurposes.DataSourceSearch,
        });

        return new DataSourceOrchestrationHandler(
            Options.Create(toolOptions),
            new FakeTemplateService(),
            NullLogger<DataSourceOrchestrationHandler>.Instance);
    }

    private sealed class FakeTemplateService : ITemplateService
    {
        public Task<IReadOnlyList<Template>> ListAsync()
            => Task.FromResult<IReadOnlyList<Template>>([]);

        public Task<Template> GetAsync(string id)
            => Task.FromResult<Template>(null);

        public Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
        {
            if (id == AITemplateIds.DataSourceAvailability)
            {
                var searchToolName = arguments != null &&
                    arguments.TryGetValue("searchToolName", out var searchToolNameObject)
                        ? searchToolNameObject?.ToString()
                        : null;

                return Task.FromResult(string.IsNullOrWhiteSpace(searchToolName)
                    ? "[Configured Data Source]"
                    : $"[Configured Data Source]{Environment.NewLine}{searchToolName}");
            }

            return Task.FromResult($"[Template: {id}]");
        }

        public Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = "\n\n")
            => Task.FromResult(string.Join(separator, ids.Select(id => $"[Template: {id}]")));
    }
}

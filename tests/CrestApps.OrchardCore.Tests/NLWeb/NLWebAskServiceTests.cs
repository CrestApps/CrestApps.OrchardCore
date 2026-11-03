using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.NLWeb;

public sealed class NLWebAskServiceTests
{
    [Fact]
    public async Task AskAsync_WhenNoRetrieverIsEnabled_ReturnsANotConfiguredFailure()
    {
        // Arrange
        var service = CreateService(retrievers: []);

        // Act
        var events = await AskAsync(service, "pies");

        // Assert
        Assert.IsType<NLWebStartedEvent>(events[0]);
        var failure = Assert.IsType<NLWebFailureEvent>(Assert.Single(events, nlwebEvent => nlwebEvent is NLWebFailureEvent));
        Assert.Equal(NLWebProtocol.ErrorCodes.NotConfigured, failure.Code);
        Assert.DoesNotContain(events, nlwebEvent => nlwebEvent is NLWebCompletedEvent);
    }

    [Fact]
    public async Task AskAsync_ReturnsTheResolvedResultsInOrderAndCompletes()
    {
        // Arrange
        var service = CreateService([new FakeRetriever("index", "a", "b", "c")]);

        // Act
        var events = await AskAsync(service, "pies");

        // Assert
        var results = events.OfType<NLWebResultEvent>().ToList();
        Assert.Equal(["a", "b", "c"], results.Select(result => result.Result.Id));
        Assert.Equal([0, 1, 2], results.Select(result => result.Index));
        var completed = Assert.IsType<NLWebCompletedEvent>(events[events.Count - 1]);
        Assert.Equal(3, completed.TotalItems);
    }

    [Fact]
    public async Task AskAsync_KeepsOnlyTheMaximumNumberOfResults()
    {
        // Arrange
        var processor = new DelegateProcessor(context => context.MaxResults = 2);
        var service = CreateService([new FakeRetriever("index", "a", "b", "c", "d")], processors: [processor]);

        // Act
        var events = await AskAsync(service, "pies");

        // Assert
        Assert.Equal(2, events.OfType<NLWebResultEvent>().Count());
    }

    [Fact]
    public async Task AskAsync_MergesTheListsOfSeveralRetrievers()
    {
        // Arrange
        var service = CreateService(
        [
            new FakeRetriever("index", "a", "b"),
            new FakeRetriever("vector", "b", "c"),
        ]);

        // Act
        var events = await AskAsync(service, "pies");

        // Assert
        var ids = events.OfType<NLWebResultEvent>().Select(result => result.Result.Id).ToList();
        Assert.Equal("b", ids[0]);
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task AskAsync_WhenARankerFails_StillAnswers()
    {
        // Arrange
        var service = CreateService([new FakeRetriever("index", "a", "b")], rankers: [new ThrowingRanker()]);

        // Act
        var events = await AskAsync(service, "pies");

        // Assert
        Assert.Equal(2, events.OfType<NLWebResultEvent>().Count());
        Assert.IsType<NLWebCompletedEvent>(events[events.Count - 1]);
    }

    [Fact]
    public async Task AskAsync_InSummarizeMode_AddsTheGeneratedText()
    {
        // Arrange
        var service = CreateService([new FakeRetriever("index", "a")], generators: [new FakeGenerator("Summary.")]);

        // Act
        var events = await AskAsync(service, "pies", NLWebProtocol.Modes.Summarize);

        // Assert
        var text = Assert.Single(events.OfType<NLWebTextEvent>());
        Assert.Equal("Summary.", text.Text);
        Assert.Equal(NLWebProtocol.Modes.Summarize, text.Mode);
    }

    [Fact]
    public async Task AskAsync_InListMode_DoesNotGenerateText()
    {
        // Arrange
        var service = CreateService([new FakeRetriever("index", "a")], generators: [new FakeGenerator("Summary.")]);

        // Act
        var events = await AskAsync(service, "pies", NLWebProtocol.Modes.List);

        // Assert
        Assert.Empty(events.OfType<NLWebTextEvent>());
    }

    [Fact]
    public async Task AskAsync_WhenAProcessorAsksForMoreInformation_ReturnsTheElicitation()
    {
        // Arrange
        var processor = new DelegateProcessor(context => context.Elicitation = new NLWebElicitationEvent("Which kind?", []));
        var retriever = new FakeRetriever("index", "a");
        var service = CreateService([retriever], processors: [processor]);

        // Act
        var events = await AskAsync(service, "dinner");

        // Assert
        Assert.IsType<NLWebElicitationEvent>(events[events.Count - 1]);
        Assert.False(retriever.WasCalled);
    }

    [Fact]
    public async Task AskAsync_WhenTheResolverThrows_ReturnsAnInternalErrorFailure()
    {
        // Arrange
        var service = new NLWebAskService(
            [],
            [new FakeRetriever("index", "a")],
            new ThrowingResolver(),
            [],
            [],
            [],
            NullLogger<NLWebAskService>.Instance);

        // Act
        var events = await AskAsync(service, "pies");

        // Assert
        var failure = Assert.IsType<NLWebFailureEvent>(events[events.Count - 1]);
        Assert.Equal(NLWebProtocol.ErrorCodes.InternalError, failure.Code);
    }

    [Fact]
    public async Task AskAsync_SearchesTheDecontextualizedQueryWhenTheCallerSentOne()
    {
        // Arrange
        var retriever = new FakeRetriever("index", "a");
        var service = CreateService([retriever]);
        var request = CreateRequest("with cinnamon?");
        request.Query.DecontextualizedText = "muffins with cinnamon";

        // Act
        await foreach (var _ in service.AskAsync(request, new ClaimsPrincipal(), TestContext.Current.CancellationToken))
        {
        }

        // Assert
        Assert.Equal("muffins with cinnamon", retriever.LastQuery);
    }

    private static NLWebAskService CreateService(
        IEnumerable<INLWebRetriever> retrievers,
        IEnumerable<INLWebQueryProcessor> processors = null,
        IEnumerable<INLWebResultRanker> rankers = null,
        IEnumerable<INLWebResponseGenerator> generators = null)
        => new(
            processors ?? [],
            retrievers,
            new FakeResolver(),
            rankers ?? [],
            generators ?? [],
            [],
            NullLogger<NLWebAskService>.Instance);

    private static NLWebAskRequest CreateRequest(string text, string mode = null)
    {
        var request = new NLWebAskRequest();
        request.Query.Text = text;
        request.Prefer.Mode = mode;

        return request;
    }

    private static async Task<List<NLWebEvent>> AskAsync(NLWebAskService service, string text, string mode = null)
    {
        var events = new List<NLWebEvent>();

        await foreach (var nlwebEvent in service.AskAsync(CreateRequest(text, mode), new ClaimsPrincipal(), TestContext.Current.CancellationToken))
        {
            events.Add(nlwebEvent);
        }

        return events;
    }

    private sealed class FakeRetriever : INLWebRetriever
    {
        private readonly string[] _ids;

        public FakeRetriever(string name, params string[] ids)
        {
            Name = name;
            _ids = ids;
        }

        public string Name { get; }

        public bool WasCalled { get; private set; }

        public string LastQuery { get; private set; }

        public Task<bool> IsEnabledAsync(NLWebAskContext context)
            => Task.FromResult(true);

        public Task<IReadOnlyList<NLWebCandidate>> RetrieveAsync(NLWebAskContext context, int count, CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastQuery = context.QueryText;

            return Task.FromResult<IReadOnlyList<NLWebCandidate>>([.. _ids.Take(count).Select(id => new NLWebCandidate(id, null, Name))]);
        }
    }

    private sealed class FakeResolver : INLWebResultResolver
    {
        public Task<IReadOnlyList<NLWebResult>> ResolveAsync(NLWebAskContext context, IReadOnlyList<NLWebCandidate> candidates, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<NLWebResult>>(
            [
                .. candidates.Select(candidate => new NLWebResult
                {
                    Id = candidate.Id,
                    Name = candidate.Id,
                    Item = new JsonObject
                    {
                        ["@type"] = "Thing",
                        ["name"] = candidate.Id,
                    },
                }),
            ]);
    }

    private sealed class ThrowingResolver : INLWebResultResolver
    {
        public Task<IReadOnlyList<NLWebResult>> ResolveAsync(NLWebAskContext context, IReadOnlyList<NLWebCandidate> candidates, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Broken.");
    }

    private sealed class ThrowingRanker : INLWebResultRanker
    {
        public Task RankAsync(NLWebAskContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Broken.");
    }

    private sealed class FakeGenerator : INLWebResponseGenerator
    {
        private readonly string _text;

        public FakeGenerator(string text)
        {
            _text = text;
        }

        public bool CanGenerate(string mode)
            => mode == NLWebProtocol.Modes.Summarize;

        public Task<string> GenerateAsync(NLWebAskContext context, CancellationToken cancellationToken)
            => Task.FromResult(_text);
    }

    private sealed class DelegateProcessor : INLWebQueryProcessor
    {
        private readonly Action<NLWebAskContext> _action;

        public DelegateProcessor(Action<NLWebAskContext> action)
        {
            _action = action;
        }

        public Task ProcessAsync(NLWebAskContext context, CancellationToken cancellationToken)
        {
            _action(context);

            return Task.CompletedTask;
        }
    }
}

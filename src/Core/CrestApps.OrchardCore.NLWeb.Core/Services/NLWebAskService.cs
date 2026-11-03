using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using CrestApps.OrchardCore.NLWeb.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Answers NLWeb questions: runs the query processors, merges what the retrievers find, resolves the candidates into
/// schema.org results, ranks them, and writes the summary or the answer the mode asks for.
/// </summary>
public sealed class NLWebAskService : INLWebAskService
{
    private readonly IEnumerable<INLWebQueryProcessor> _queryProcessors;
    private readonly IEnumerable<INLWebRetriever> _retrievers;
    private readonly INLWebResultResolver _resultResolver;
    private readonly IEnumerable<INLWebResultRanker> _rankers;
    private readonly IEnumerable<INLWebResponseGenerator> _generators;
    private readonly IEnumerable<INLWebAskHandler> _handlers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebAskService"/> class.
    /// </summary>
    /// <param name="queryProcessors">The query processors.</param>
    /// <param name="retrievers">The retrievers.</param>
    /// <param name="resultResolver">The result resolver.</param>
    /// <param name="rankers">The result rankers.</param>
    /// <param name="generators">The response generators.</param>
    /// <param name="handlers">The handlers told about every question.</param>
    /// <param name="logger">The logger.</param>
    public NLWebAskService(
        IEnumerable<INLWebQueryProcessor> queryProcessors,
        IEnumerable<INLWebRetriever> retrievers,
        INLWebResultResolver resultResolver,
        IEnumerable<INLWebResultRanker> rankers,
        IEnumerable<INLWebResponseGenerator> generators,
        IEnumerable<INLWebAskHandler> handlers,
        ILogger<NLWebAskService> logger)
    {
        _queryProcessors = queryProcessors;
        _retrievers = retrievers;
        _resultResolver = resultResolver;
        _rankers = rankers;
        _generators = generators;
        _handlers = handlers;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<NLWebEvent> AskAsync(
        NLWebAskRequest request,
        ClaimsPrincipal user,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        var context = new NLWebAskContext(request, user);
        var queryId = request.Meta.QueryId ?? Guid.NewGuid().ToString("n");

        yield return new NLWebStartedEvent(request.Meta.ConversationId, queryId);

        var failure = await RunAsync(() => PrepareAsync(context, cancellationToken), cancellationToken);

        if (failure is null && !context.IsCompleted)
        {
            failure = await RunAsync(() => FindResultsAsync(context, cancellationToken), cancellationToken);
        }

        if (failure is not null)
        {
            context.Failure = failure;
        }

        if (context.Failure is not null)
        {
            await NotifyAsync(context);

            yield return context.Failure;

            yield break;
        }

        if (context.Elicitation is not null)
        {
            await NotifyAsync(context);

            yield return context.Elicitation;

            yield break;
        }

        for (var i = 0; i < context.Results.Count; i++)
        {
            yield return new NLWebResultEvent(i, context.Results[i]);
        }

        string text = null;
        var generator = context.Mode == NLWebProtocol.Modes.List || context.Results.Count == 0
            ? null
            : _generators.FirstOrDefault(candidate => candidate.CanGenerate(context.Mode));

        if (generator is not null)
        {
            // The results are already sent, so a failure to write the text leaves an answer without it.
            await RunAsync(async () => text = await generator.GenerateAsync(context, cancellationToken), cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            yield return new NLWebTextEvent(text, context.Mode);
        }

        await NotifyAsync(context);

        yield return new NLWebCompletedEvent(context.Results.Count, stopwatch.ElapsedMilliseconds, context.SessionContext);
    }

    private async Task PrepareAsync(NLWebAskContext context, CancellationToken cancellationToken)
    {
        foreach (var processor in _queryProcessors.OrderBy(processor => processor.Order))
        {
            await processor.ProcessAsync(context, cancellationToken);

            if (context.IsCompleted)
            {
                return;
            }
        }
    }

    private async Task FindResultsAsync(NLWebAskContext context, CancellationToken cancellationToken)
    {
        var candidateCount = Math.Max(context.MaxResults, 1) * Math.Max(context.CandidateFactor, 1);
        var lists = new List<IReadOnlyList<NLWebCandidate>>();

        foreach (var retriever in _retrievers)
        {
            if (!await retriever.IsEnabledAsync(context))
            {
                continue;
            }

            try
            {
                lists.Add(await retriever.RetrieveAsync(context, candidateCount, cancellationToken) ?? []);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Another retriever can still answer, so one failing retriever does not fail the question.
                _logger.LogError(ex, "The NLWeb retriever '{Retriever}' failed.", retriever.Name);
            }
        }

        if (lists.Count == 0)
        {
            context.Failure = new NLWebFailureEvent(
                NLWebProtocol.ErrorCodes.NotConfigured,
                "The site has no search index for natural language questions yet.");

            return;
        }

        var candidates = NLWebRankFusion.Fuse(lists, candidateCount);

        if (candidates.Count == 0)
        {
            return;
        }

        var results = await _resultResolver.ResolveAsync(context, candidates, cancellationToken);

        foreach (var result in results)
        {
            context.Results.Add(result);
        }

        foreach (var ranker in _rankers)
        {
            try
            {
                await ranker.RankAsync(context, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The results keep the order the retrievers gave them, which still answers the question.
                _logger.LogError(ex, "The NLWeb ranker '{Ranker}' failed.", ranker.GetType().FullName);
            }
        }

        while (context.Results.Count > context.MaxResults)
        {
            context.Results.RemoveAt(context.Results.Count - 1);
        }
    }

    private async Task<NLWebFailureEvent> RunAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        try
        {
            await action();

            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An NLWeb question could not be answered.");

            return new NLWebFailureEvent(NLWebProtocol.ErrorCodes.InternalError, "The site could not answer the question.");
        }
    }

    private async Task NotifyAsync(NLWebAskContext context)
    {
        foreach (var handler in _handlers)
        {
            try
            {
                await handler.AskedAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The NLWeb ask handler '{Handler}' failed.", handler.GetType().FullName);
            }
        }
    }
}

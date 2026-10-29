using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

/// <summary>
/// The AI tests API: list the test cases, queue a batch of runs from a CI pipeline, and read its result.
/// </summary>
internal static class AITestsApiEndpoints
{
    /// <summary>
    /// The OpenAPI tag of the endpoints.
    /// </summary>
    public const string Tag = "AI Tests";

    /// <summary>
    /// Maps the endpoints under <c>api/ai/v1</c>.
    /// </summary>
    /// <param name="routes">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapAITestsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(AIConversationsApiEndpoints.RoutePrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api })
            .DisableAntiforgery()
            .WithTags(Tag);

        group.MapGet("tests", ListAsync)
            .WithName("ListAITests")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the AI test cases with the outcome of each one's latest run.");

        group.MapPost("tests/batches", QueueAsync)
            .WithName("QueueAITestBatch")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Queues runs of the given test cases, or of every test case of a profile, a number of times each. Runs start within a minute; poll the batch for its result.");

        group.MapGet("tests/batches/{batchId}", GetBatchAsync)
            .WithName("GetAITestBatch")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets a batch of AI test runs: each run's status, score and summary, the pass rate, and whether it has finished.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiAITestCase>>, ProblemHttpResult>> ListAsync(
        string profileId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAITestCaseStore store)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIQuality))
        {
            return Forbidden();
        }

        var testCases = (await store.GetAllAsync())
            .Where(testCase => string.IsNullOrEmpty(profileId) || testCase.ProfileId == profileId)
            .OrderBy(testCase => testCase.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(testCase => new ApiAITestCase(testCase.ItemId, testCase.Name, testCase.ProfileId, testCase.ProfileVersion, testCase.LastStatus?.ToString(), testCase.LastRunUtc))
            .ToList();

        return TypedResults.Ok<IReadOnlyList<ApiAITestCase>>(testCases);
    }

    private static async Task<Results<Ok<ApiAITestBatch>, ValidationProblem, ProblemHttpResult>> QueueAsync(
        ApiAITestBatchRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAITestCaseStore store,
        AITestRunner runner)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIQuality))
        {
            return Forbidden();
        }

        if (request is null || ((request.TestCaseIds is null || request.TestCaseIds.Count == 0) && string.IsNullOrWhiteSpace(request.ProfileId)))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["testCaseIds"] = ["Give testCaseIds, or a profileId to run all of its test cases."] });
        }

        var testCases = request.TestCaseIds is { Count: > 0 }
            ? await store.GetAsync(request.TestCaseIds)
            : (await store.GetAllAsync()).Where(testCase => testCase.ProfileId == request.ProfileId).ToList();

        if (testCases.Count == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["testCaseIds"] = ["No test case matches."] });
        }

        var (batchId, runs) = await runner.QueueAsync(testCases, request.Repeats ?? 1, httpContext.User.Identity?.Name ?? "API");

        return TypedResults.Ok(ToBatch(batchId, runs));
    }

    private static async Task<Results<Ok<ApiAITestBatch>, ProblemHttpResult>> GetBatchAsync(
        string batchId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAITestRunStore runStore)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIQuality))
        {
            return Forbidden();
        }

        var runs = await runStore.ListByBatchAsync(batchId);

        return runs.Count == 0
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "The batch was not found.")
            : TypedResults.Ok(ToBatch(batchId, runs));
    }

    /// <summary>
    /// The API view of a batch.
    /// </summary>
    /// <param name="batchId">The batch.</param>
    /// <param name="runs">Its runs.</param>
    /// <returns>The batch.</returns>
    internal static ApiAITestBatch ToBatch(string batchId, IReadOnlyList<AITestRun> runs)
        => new(
            batchId,
            Completed: runs.All(run => run.Status is not (AITestRunStatus.Queued or AITestRunStatus.Running)),
            PassRate: AITestRunner.PassRate(runs),
            runs.Select(run => new ApiAITestRun(run.ItemId, run.TestCaseId, run.TestCaseName, run.Status.ToString(), run.Score, run.Summary, run.Error, run.ProfileVersion, run.CompletedUtc)).ToList());

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "You are not allowed to manage AI tests.");
}

/// <summary>
/// An AI test case.
/// </summary>
/// <param name="Id">The test case's identifier.</param>
/// <param name="Name">Its name.</param>
/// <param name="ProfileId">The profile it tests.</param>
/// <param name="ProfileVersion">The version it tests, or <see langword="null"/> for the current one.</param>
/// <param name="LastStatus">The outcome of its latest run.</param>
/// <param name="LastRunUtc">When it last ran.</param>
public sealed record ApiAITestCase(string Id, string Name, string ProfileId, int? ProfileVersion, string LastStatus, DateTime? LastRunUtc);

/// <summary>
/// A request to run AI test cases.
/// </summary>
/// <param name="TestCaseIds">The test cases to run.</param>
/// <param name="ProfileId">Runs every test case of this profile when <paramref name="TestCaseIds"/> is empty.</param>
/// <param name="Repeats">How many times to run each, 1 to 20.</param>
public sealed record ApiAITestBatchRequest(IReadOnlyList<string> TestCaseIds, string ProfileId, int? Repeats);

/// <summary>
/// A batch of AI test runs.
/// </summary>
/// <param name="BatchId">The batch's identifier.</param>
/// <param name="Completed">Whether every run has finished.</param>
/// <param name="PassRate">The share of finished runs that passed, from 0 to 1.</param>
/// <param name="Runs">The runs.</param>
public sealed record ApiAITestBatch(string BatchId, bool Completed, double? PassRate, IReadOnlyList<ApiAITestRun> Runs);

/// <summary>
/// One AI test run.
/// </summary>
/// <param name="Id">The run's identifier.</param>
/// <param name="TestCaseId">The test that ran.</param>
/// <param name="TestCaseName">Its name.</param>
/// <param name="Status">Queued, Running, Passed, Failed or Error.</param>
/// <param name="Score">The judge's score out of 100.</param>
/// <param name="Summary">The judge's summary.</param>
/// <param name="Error">Why the run could not finish.</param>
/// <param name="ProfileVersion">The version of the profile that answered.</param>
/// <param name="CompletedUtc">When it finished.</param>
public sealed record ApiAITestRun(string Id, string TestCaseId, string TestCaseName, string Status, int? Score, string Summary, string Error, int? ProfileVersion, DateTime? CompletedUtc);

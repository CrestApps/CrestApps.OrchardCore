using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore;
using YesSql;
using ISession = YesSql.ISession;
using YesSql.Services;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

/// <summary>
/// AI conversations over the API: list and read them with their transcript and analysis, evaluate them, analyze them
/// again, delete them, run test conversations, read a profile's versions and read or replace a contact's memory.
/// Callers authenticate with a bearer token from the tenant's OpenID server and need Manage AI profiles.
/// </summary>
internal static class AIConversationsApiEndpoints
{
    public const string RoutePrefix = "api/ai/v1";

    public const string Tag = "AI Conversations";

    public static IEndpointRouteBuilder MapAIConversationsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutePrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api })
            .DisableAntiforgery()
            .WithTags(Tag);

        group.MapGet("conversations", ListAsync)
            .WithName("ListAIConversations")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists AI conversations, newest activity first, optionally for one profile, one status or since a time.");

        group.MapGet("conversations/{sessionId}", GetAsync)
            .WithName("GetAIConversation")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets an AI conversation with its transcript, variables, extracted fields, analysis results and evaluations.");

        group.MapDelete("conversations/{sessionId}", DeleteAsync)
            .WithName("DeleteAIConversation")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Deletes an AI conversation with its transcript, extracted data and evaluations, for example on a customer's request to be forgotten.");

        group.MapPost("conversations/{sessionId}/evaluations", EvaluateAsync)
            .WithName("EvaluateAIConversation")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Scores an AI conversation against criteria with the utility model and saves the evaluation.");

        group.MapPost("conversations/{sessionId}/analysis", AnalyzeAgainAsync)
            .WithName("AnalyzeAIConversationAgain")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Runs the profile's post-session analysis on an AI conversation again, with the profile's current tasks.");

        group.MapPost("profiles/{profileId}/simulations", SimulateAsync)
            .WithName("SimulateAIConversation")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Runs a test conversation between the profile and a customer the utility model plays, and evaluates it when criteria are given. The profile's tools run, told it is a simulation.");

        group.MapGet("profiles/{profileId}/versions", ListVersionsAsync)
            .WithName("ListAIProfileVersions")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Lists the saved versions of an AI profile, newest first.");

        group.MapGet("contacts/{contactId}/memory", GetMemoryAsync)
            .WithName("GetAIContactMemory")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Gets what the AI remembers about a contact.");

        group.MapPut("contacts/{contactId}/memory", SetMemoryAsync)
            .WithName("SetAIContactMemory")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Replaces what the AI remembers about a contact. Empty text forgets it.");

        return routes;
    }

    private static async Task<Results<Ok<ApiAIPage<ApiAIConversationSummary>>, ValidationProblem, ProblemHttpResult>> ListAsync(
        string profileId,
        string status,
        DateTime? since,
        int? skip,
        int? take,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ISession session,
        IOptions<YesSqlStoreOptions> storeOptions)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        ChatSessionStatus? statusFilter = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ChatSessionStatus>(status, ignoreCase: true, out var parsed))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Use Active, Closed or Abandoned."] });
            }

            statusFilter = parsed;
        }

        var query = session.Query<AIChatSession, AIChatSessionIndex>(storeOptions.Value.AICollectionName);

        if (!string.IsNullOrWhiteSpace(profileId))
        {
            query = query.Where(index => index.ProfileId == profileId);
        }

        if (statusFilter is { } wanted)
        {
            query = query.Where(index => index.Status == wanted);
        }

        if (since is { } sinceUtc)
        {
            var from = sinceUtc.ToUniversalTime();
            query = query.Where(index => index.LastActivityUtc >= from);
        }

        var pageSkip = ApiAIPage<ApiAIConversationSummary>.ClampSkip(skip);
        var pageTake = ApiAIPage<ApiAIConversationSummary>.ClampTake(take);
        var total = await query.CountAsync();
        var conversations = await query
            .OrderByDescending(index => index.LastActivityUtc)
            .Skip(pageSkip)
            .Take(pageTake)
            .ListAsync();

        return TypedResults.Ok(new ApiAIPage<ApiAIConversationSummary>(conversations.Select(ToSummary).ToList(), total, pageSkip, pageTake));
    }

    private static async Task<Results<Ok<ApiAIConversation>, ProblemHttpResult>> GetAsync(
        string sessionId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IAIConversationEvaluationStore evaluationStore)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var conversation = await sessionManager.FindByIdAsync(sessionId);

        if (conversation is null)
        {
            return NotFound();
        }

        var transcript = AIConversationTranscript.GetTurns(await promptStore.GetPromptsAsync(conversation.SessionId))
            .Select(turn => new ApiAIConversationTurn(turn.Role, turn.Text, turn.CreatedUtc))
            .ToList();

        return TypedResults.Ok(new ApiAIConversation(
            ToSummary(conversation),
            conversation.TryGet<AIConversationContactMetadata>(out var contact) ? contact.ContactId : null,
            transcript,
            new Dictionary<string, string>(conversation.GetConversationVariables(), StringComparer.OrdinalIgnoreCase),
            conversation.ExtractedData.ToDictionary(field => field.Key, field => (IReadOnlyList<string>)(field.Value?.Values ?? [])),
            conversation.PostSessionResults.ToDictionary(result => result.Key, result => result.Value?.Value),
            await evaluationStore.ListBySessionAsync(conversation.SessionId)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        string sessionId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        AIConversationPrivacyService privacy)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.DeleteAllChatSessions))
        {
            return Forbidden();
        }

        return await privacy.DeleteAsync(sessionId)
            ? TypedResults.NoContent()
            : NotFound();
    }

    private static async Task<Results<Ok<AIConversationEvaluation>, ValidationProblem, ProblemHttpResult>> EvaluateAsync(
        string sessionId,
        ApiAIEvaluationRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIChatSessionManager sessionManager,
        AIConversationEvaluationService evaluationService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        if (request?.PassingScore is < 0 or > 100)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["passingScore"] = ["Use a score from 0 to 100."] });
        }

        if (await sessionManager.FindByIdAsync(sessionId) is null)
        {
            return NotFound();
        }

        var evaluation = await evaluationService.EvaluateSessionAsync(
            sessionId,
            Clean(request?.Criteria),
            request?.PassingScore ?? AIConversationEvaluator.DefaultPassingScore,
            request?.Label?.Trim(),
            httpContext.RequestAborted);

        return evaluation is null
            ? TypedResults.Problem("The conversation has nothing the customer said, or no utility model is available.", statusCode: StatusCodes.Status409Conflict, title: "Not evaluated")
            : TypedResults.Ok(evaluation);
    }

    private static async Task<Results<Ok<IReadOnlyDictionary<string, string>>, ProblemHttpResult>> AnalyzeAgainAsync(
        string sessionId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        AIChatSessionPostCloseProcessor postCloseProcessor)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var conversation = await sessionManager.FindByIdAsync(sessionId);
        var profile = string.IsNullOrEmpty(conversation?.ProfileId) ? null : await profileManager.FindByIdAsync(conversation.ProfileId);

        if (conversation is null || profile is null)
        {
            return NotFound();
        }

        RerunConversationAnalysisTask.Reset(conversation);

        if (!postCloseProcessor.QueueIfNeeded(profile, conversation))
        {
            return TypedResults.Problem("The profile has no post-session analysis to run.", statusCode: StatusCodes.Status409Conflict, title: "Nothing to analyze");
        }

        try
        {
            await postCloseProcessor.ProcessAsync(profile, conversation, await promptStore.GetPromptsAsync(conversation.SessionId), httpContext.RequestAborted);
        }
        finally
        {
            // Whatever was redone is kept; what failed is retried by the session close task like any other.
            await sessionManager.SaveAsync(conversation);
        }

        return TypedResults.Ok<IReadOnlyDictionary<string, string>>(conversation.PostSessionResults.ToDictionary(result => result.Key, result => result.Value?.Value));
    }

    private static async Task<Results<Ok<ApiAISimulation>, ValidationProblem, ProblemHttpResult>> SimulateAsync(
        string profileId,
        ApiAISimulationRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        AIConversationSimulator simulator,
        AIConversationEvaluationService evaluationService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request?.Scenario))
        {
            errors["scenario"] = ["Describe who the customer is and what they want."];
        }

        if (request?.MaxTurns is < 1 or > 20)
        {
            errors["maxTurns"] = ["Use from 1 to 20 turns."];
        }

        if (request?.PassingScore is < 0 or > 100)
        {
            errors["passingScore"] = ["Use a score from 0 to 100."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var profile = await profileManager.FindByIdAsync(profileId);

        if (profile is null)
        {
            return NotFound();
        }

        var simulation = await simulator.SimulateAsync(new AIConversationSimulationRequest
        {
            Profile = profile,
            Scenario = request.Scenario.Trim(),
            Variables = request.Variables ?? [],
            MaxTurns = request.MaxTurns ?? AIConversationSimulator.DefaultMaxTurns,
        }, httpContext.RequestAborted);

        var criteria = Clean(request.Criteria);
        var evaluation = criteria.Count == 0
            ? null
            : await evaluationService.EvaluateTurnsAsync(profile, null, simulation.Turns, criteria, request.PassingScore ?? AIConversationEvaluator.DefaultPassingScore, request.Label?.Trim(), simulation.Instructions, httpContext.RequestAborted);

        return TypedResults.Ok(new ApiAISimulation(
            simulation.Turns.Select(turn => new ApiAIConversationTurn(turn.Role, turn.Text, turn.CreatedUtc)).ToList(),
            simulation.CustomerEnded,
            evaluation));
    }

    private static async Task<Results<Ok<ApiAIPage<ApiAIProfileVersion>>, ProblemHttpResult>> ListVersionsAsync(
        string profileId,
        int? skip,
        int? take,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIProfileVersionStore versionStore)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        if (await profileManager.FindByIdAsync(profileId) is null)
        {
            return NotFound();
        }

        // The store pages by page number, so the skip is rounded down to a whole page.
        var pageTake = ApiAIPage<ApiAIProfileVersion>.ClampTake(take);
        var page = (ApiAIPage<ApiAIProfileVersion>.ClampSkip(skip) / pageTake) + 1;
        var (versions, count) = await versionStore.PageAsync(profileId, page, pageTake);

        return TypedResults.Ok(new ApiAIPage<ApiAIProfileVersion>(
            versions.Select(version => new ApiAIProfileVersion(version.ItemId, version.Version, version.CreatedUtc, version.Author, version.Note)).ToList(),
            count,
            (page - 1) * pageTake,
            pageTake));
    }

    private static async Task<Results<Ok<ApiAIContactMemory>, ProblemHttpResult>> GetMemoryAsync(
        string contactId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        AIContactMemoryService memoryService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        return TypedResults.Ok(new ApiAIContactMemory(contactId, await memoryService.GetAsync(contactId, httpContext.RequestAborted)));
    }

    private static async Task<Results<Ok<ApiAIContactMemory>, ValidationProblem, ProblemHttpResult>> SetMemoryAsync(
        string contactId,
        ApiAIContactMemoryRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        AIContactMemoryService memoryService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        if (request?.Text?.Length > AIContactMemory.MaxLength)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["text"] = [$"Keep the note to {AIContactMemory.MaxLength} characters."] });
        }

        await memoryService.SetAsync(contactId, request?.Text, cancellationToken: httpContext.RequestAborted);

        return TypedResults.Ok(new ApiAIContactMemory(contactId, await memoryService.GetAsync(contactId, httpContext.RequestAborted)));
    }

    internal static ApiAIConversationSummary ToSummary(AIChatSession conversation)
        => new(
            conversation.SessionId,
            conversation.ProfileId,
            conversation.TryGet<AIProfileVersionMetadata>(out var version) ? version.Version : null,
            conversation.Title,
            conversation.Status.ToString(),
            conversation.CreatedUtc,
            conversation.LastActivityUtc,
            conversation.ClosedAtUtc,
            conversation.TryGet<AIConversationPrivacyMetadata>(out var privacy) ? privacy.Mode.ToString() : null);

    private static List<string> Clean(IEnumerable<string> criteria)
        => (criteria ?? [])
            .Where(criterion => !string.IsNullOrWhiteSpace(criterion))
            .Select(criterion => criterion.Trim())
            .ToList();

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    private static ProblemHttpResult NotFound()
        => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found");
}

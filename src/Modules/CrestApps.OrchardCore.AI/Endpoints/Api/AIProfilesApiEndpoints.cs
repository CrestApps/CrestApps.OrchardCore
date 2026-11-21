using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

/// <summary>
/// AI profiles over the API: list, read, create, update and delete them, and restore a saved version. Every save is a
/// version, so restoring one is how an older version is published again. Callers need Manage AI profiles.
/// </summary>
internal static class AIProfilesApiEndpoints
{
    public const string Tag = "AI Profiles";

    private const string LegacyFunctionInvocationKey = "AIProfileFunctionInvocationMetadata";

    private static readonly JsonNodeOptions _caseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions _caseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

    public static IEndpointRouteBuilder MapAIProfilesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(AIConversationsApiEndpoints.RoutePrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AIApiKeys.ApiSchemes })
            .DisableAntiforgery()
            .WithTags(Tag);

        group.MapGet("profiles", ListAsync)
            .WithName("ListAIProfiles")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists AI profiles by name, optionally filtered by a part of the name.");

        group.MapGet("profiles/{profileId}", GetAsync)
            .WithName("GetAIProfile")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets an AI profile with all its settings, in the shape the AI profile recipe step takes.");

        group.MapPost("profiles", CreateAsync)
            .WithName("CreateAIProfile")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Creates an AI profile from the same JSON the AI profile recipe step takes.");

        group.MapPut("profiles/{profileId}", UpdateAsync)
            .WithName("UpdateAIProfile")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .WithSummary("Updates an AI profile with the given settings, leaving the rest as they are. The save is a new version.");

        group.MapDelete("profiles/{profileId}", DeleteAsync)
            .WithName("DeleteAIProfile")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Deletes an AI profile.");

        group.MapPost("profiles/{profileId}/versions/{versionId}/restore", RestoreAsync)
            .WithName("RestoreAIProfileVersion")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Publishes a saved version again: the profile takes its settings back, saved as a new version.");

        return routes;
    }

    private static async Task<Results<Ok<ApiAIPage<ApiAIProfileSummary>>, ProblemHttpResult>> ListAsync(
        string name,
        int? skip,
        int? take,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Profiles, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var pageTake = ApiAIPage<ApiAIProfileSummary>.ClampTake(take);
        var page = (ApiAIPage<ApiAIProfileSummary>.ClampSkip(skip) / pageTake) + 1;
        var result = await profileManager.PageAsync(page, pageTake, new AIProfileQueryContext { Name = name }, httpContext.RequestAborted);

        return TypedResults.Ok(new ApiAIPage<ApiAIProfileSummary>(
            result.Entries.Select(ToSummary).ToList(),
            result.Count,
            (page - 1) * pageTake,
            pageTake));
    }

    private static async Task<Results<Ok<AIProfile>, ProblemHttpResult>> GetAsync(
        string profileId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Profiles, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var profile = await profileManager.FindByIdAsync(profileId, httpContext.RequestAborted);

        return profile is null ? NotFound() : TypedResults.Ok(profile);
    }

    internal static async Task<Results<Created<AIProfile>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        JsonObject body,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IAIToolInstanceAccessor instanceAccessor)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Profiles, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        if (body is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Send the profile as JSON."] });
        }

        // The id is always new, so a copied profile is not mistaken for the one it came from.
        body.Remove(nameof(AIProfile.ItemId));

        var name = body[nameof(AIProfile.Name)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(name) && await profileManager.FindByNameAsync(name, httpContext.RequestAborted) is not null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Another profile has this name."] });
        }

        var profile = await profileManager.NewAsync(body, httpContext.RequestAborted);

        if (await RefuseUnauthorizedToolsAsync(AsStored(profile), existing: null, httpContext, toolAccessEvaluator, instanceAccessor) is { } toolsRefused)
        {
            return toolsRefused;
        }

        var validation = await profileManager.ValidateAsync(profile, httpContext.RequestAborted);

        if (!validation.Succeeded)
        {
            return AISourceCatalogApi.Invalid(validation);
        }

        await profileManager.CreateAsync(profile, httpContext.RequestAborted);

        return TypedResults.Created($"/{AIConversationsApiEndpoints.RoutePrefix}/profiles/{profile.ItemId}", profile);
    }

    internal static async Task<Results<Ok<AIProfile>, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        string profileId,
        JsonObject body,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IAIToolInstanceAccessor instanceAccessor)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Profiles, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var profile = await profileManager.FindByIdAsync(profileId, httpContext.RequestAborted);

        if (profile is null)
        {
            return NotFound();
        }

        if (body is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Send the settings to change as JSON."] });
        }

        // The body is bound without regard to case, while the profile's own JSON is not: its keys take the profile's
        // spelling first, so the copy whose tools are checked and the update that is saved read the same values. A
        // lower-case "settings" or "properties" key slipped new tools past the check that way. Both objects match keys
        // without regard to case, as the bound body did, so a flat key such as "systemMessage" still reaches the handler.
        var current = JsonNode.Parse(JsonSerializer.Serialize(profile), _caseInsensitive) as JsonObject ?? [];
        var changes = AISourceCatalogApi.NormalizeKeys(body, current);

        // The id and the name are what links and tools point to, so they stay.
        changes.Remove(nameof(AIProfile.ItemId));
        changes.Remove(nameof(AIProfile.Name));

        // Validate a copy first: the manager saves as it updates.
        var copy = await profileManager.NewAsync(AISourceCatalogApi.Merge(current, changes), httpContext.RequestAborted);
        copy.ItemId = profile.ItemId;
        copy.Name = profile.Name;

        if (await RefuseUnauthorizedToolsAsync(AsStored(copy), profile, httpContext, toolAccessEvaluator, instanceAccessor) is { } toolsRefused)
        {
            return toolsRefused;
        }

        var validation = await profileManager.ValidateAsync(copy, httpContext.RequestAborted);

        if (!validation.Succeeded)
        {
            return AISourceCatalogApi.Invalid(validation);
        }

        await profileManager.UpdateAsync(profile, changes, httpContext.RequestAborted);

        return TypedResults.Ok(profile);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        string profileId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Profiles, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var profile = await profileManager.FindByIdAsync(profileId, httpContext.RequestAborted);

        if (profile is null)
        {
            return NotFound();
        }

        await profileManager.DeleteAsync(profile, httpContext.RequestAborted);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<AIProfile>, ProblemHttpResult>> RestoreAsync(
        string profileId,
        string versionId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIProfileVersionStore versionStore)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Profiles, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var version = await versionStore.FindByIdAsync(versionId, httpContext.RequestAborted);
        var profile = version?.ProfileId == profileId ? await profileManager.FindByIdAsync(profileId, httpContext.RequestAborted) : null;

        if (profile is null || version.Snapshot is null)
        {
            return NotFound();
        }

        AIProfileVersionRestorer.Restore(profile, version.Snapshot);

        var metadata = profile.GetOrCreate<AIProfileVersionMetadata>();
        metadata.PendingNote = $"Restored from version {version.Version} over the API";
        profile.Put(metadata);

        await profileManager.UpdateAsync(profile, cancellationToken: httpContext.RequestAborted);

        return TypedResults.Ok(profile);
    }

    private static ApiAIProfileSummary ToSummary(AIProfile profile)
        => new(
            profile.ItemId,
            profile.Name,
            profile.DisplayText,
            profile.Type.ToString(),
            profile.TryGet<AIProfileVersionMetadata>(out var version) ? version.Version : null,
            profile.CreatedUtc);

    // The package's handler builds Properties and Settings as case-sensitive dictionaries, while the store reads the saved
    // document back without regard to case. The tools are checked on the profile as the store will read it, so an inner key
    // in another case, such as "functioninvocationmetadata", cannot hide a tool from the check and run later.
    private static AIProfile AsStored(AIProfile profile)
        => JsonSerializer.Deserialize<AIProfile>(JsonSerializer.Serialize(profile)) ?? profile;

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    private static ProblemHttpResult NotFound()
        => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found");

    // The editors offer only the tools and tool instances the caller may use; the API takes raw JSON, so the same rule is
    // applied to every place a profile gets them: its tools (and the older settings key the package falls back to when
    // there are none), its tool instances, and the post-session tools and instances of the profile and of each task, which
    // run after the session with no user. A tool or instance the caller may not use (and the profile did not already
    // have in the same place) is a validation error. An instance is checked by its function, as the editor offers it.
    private static async Task<ValidationProblem> RefuseUnauthorizedToolsAsync(
        AIProfile profile,
        AIProfile existing,
        HttpContext httpContext,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IAIToolInstanceAccessor instanceAccessor)
    {
        Func<string, Task<bool>> isAuthorized = tool => toolAccessEvaluator.IsAuthorizedAsync(httpContext.User, tool);
        var accessibleInstances = (await instanceAccessor.GetAccessibleInstancesAsync(httpContext.RequestAborted))
            .Select(instance => instance.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Func<string, Task<bool>> isInstanceAuthorized = name => Task.FromResult(accessibleInstances.Contains(name));

        var refused = new List<string>();
        refused.AddRange(await AIToolSelectionFilter.GetUnauthorizedAdditionsAsync(SessionTools(profile), existing is null ? null : SessionTools(existing), isAuthorized));
        refused.AddRange(await AIToolSelectionFilter.GetUnauthorizedAdditionsAsync(PostSessionTools(profile), existing is null ? null : PostSessionTools(existing), isAuthorized));
        refused.AddRange(await AIToolSelectionFilter.GetUnauthorizedAdditionsAsync(profile.GetOrCreate<AIToolInstanceMetadata>().ToolInstanceNames, existing?.GetOrCreate<AIToolInstanceMetadata>().ToolInstanceNames, isInstanceAuthorized));
        refused.AddRange(await AIToolSelectionFilter.GetUnauthorizedAdditionsAsync(PostSessionInstances(profile), existing is null ? null : PostSessionInstances(existing), isInstanceAuthorized));
        refused = refused.Distinct(StringComparer.Ordinal).ToList();

        return refused.Count == 0
            ? null
            : TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tools"] = [$"You may not give a profile these tools: {string.Join(", ", refused)}."],
            });
    }

    private static IEnumerable<string> SessionTools(AIProfile profile)
        => (profile.GetOrCreate<FunctionInvocationMetadata>().Names ?? []).Concat(LegacyToolNames(profile));

    private static IEnumerable<string> PostSessionTools(AIProfile profile)
    {
        var settings = profile.GetSettings<AIProfilePostSessionSettings>();

        return (settings?.ToolNames ?? []).Concat(settings?.PostSessionTasks?.SelectMany(task => task?.ToolNames ?? []) ?? []);
    }

    private static IEnumerable<string> PostSessionInstances(AIProfile profile)
    {
        var settings = profile.GetSettings<AIProfilePostSessionSettings>();

        return (settings?.ToolInstanceNames ?? []).Concat(settings?.PostSessionTasks?.SelectMany(task => task?.ToolInstanceNames ?? []) ?? []);
    }

    // The package reads the tools of a profile that has none from this older settings key. It is read here in any case
    // and without regard to the case of its members, so the check covers whatever the package could read.
    private static IEnumerable<string> LegacyToolNames(AIProfile profile)
    {
        foreach (var (key, value) in profile.Settings ?? new Dictionary<string, object>())
        {
            if (!string.Equals(key, LegacyFunctionInvocationKey, StringComparison.OrdinalIgnoreCase) || value is null)
            {
                continue;
            }

            FunctionInvocationMetadata metadata;

            try
            {
                metadata = JsonSerializer.Deserialize<FunctionInvocationMetadata>(JsonSerializer.Serialize(value), _caseInsensitiveJson);
            }
            catch (JsonException)
            {
                continue;
            }

            foreach (var name in metadata?.Names ?? [])
            {
                yield return name;
            }
        }
    }
}

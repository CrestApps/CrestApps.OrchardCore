using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
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

    private static async Task<Results<Created<AIProfile>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        JsonObject body,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager)
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
        var validation = await profileManager.ValidateAsync(profile, httpContext.RequestAborted);

        if (!validation.Succeeded)
        {
            return AISourceCatalogApi.Invalid(validation);
        }

        await profileManager.CreateAsync(profile, httpContext.RequestAborted);

        return TypedResults.Created($"/{AIConversationsApiEndpoints.RoutePrefix}/profiles/{profile.ItemId}", profile);
    }

    private static async Task<Results<Ok<AIProfile>, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        string profileId,
        JsonObject body,
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

        if (body is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Send the settings to change as JSON."] });
        }

        // The id and the name are what links and tools point to, so they stay.
        body.Remove(nameof(AIProfile.ItemId));
        body.Remove(nameof(AIProfile.Name));

        // Validate a copy first: the manager saves as it updates.
        var copy = await profileManager.NewAsync(JsonSerializer.SerializeToNode(profile) is JsonObject current ? AISourceCatalogApi.Merge(current, body) : body, httpContext.RequestAborted);
        copy.ItemId = profile.ItemId;
        copy.Name = profile.Name;
        var validation = await profileManager.ValidateAsync(copy, httpContext.RequestAborted);

        if (!validation.Succeeded)
        {
            return AISourceCatalogApi.Invalid(validation);
        }

        await profileManager.UpdateAsync(profile, body, httpContext.RequestAborted);

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

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    private static ProblemHttpResult NotFound()
        => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found");
}

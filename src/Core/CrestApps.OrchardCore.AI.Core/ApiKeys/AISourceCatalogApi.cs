using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core.ApiKeys;

/// <summary>
/// Maps list, get, create, update and delete endpoints for records kept by source, such as data sources, file sources
/// and web crawlers, in the shape their recipe steps take.
/// </summary>
public static class AISourceCatalogApi
{
    /// <summary>
    /// Maps the endpoints under <c>api/ai/v1/{path}</c>.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="routes">The route builder.</param>
    /// <param name="path">The path, such as <c>data-sources</c>.</param>
    /// <param name="entityName">The record's name in operation ids, such as <c>AIDataSource</c>.</param>
    /// <param name="displayName">The record's name in summaries, such as <c>data source</c>.</param>
    /// <param name="permission">The permission to manage the records.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapAISourceCatalogApi<T>(this IEndpointRouteBuilder routes, string path, string entityName, string displayName, Permission permission)
        where T : CatalogItem, ISourceAwareModel
    {
        var group = routes.MapGroup($"api/ai/v1/{path}")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AIApiKeys.ApiSchemes })
            .DisableAntiforgery()
            .WithTags("AI Knowledge");

        group.MapGet(string.Empty, async Task<Results<Ok<IReadOnlyList<T>>, ProblemHttpResult>> (string source, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager) =>
        {
            if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Knowledge, permission))
            {
                return Forbidden();
            }

            var items = string.IsNullOrWhiteSpace(source)
                ? await manager.GetAllAsync(httpContext.RequestAborted)
                : await manager.GetAsync(source, httpContext.RequestAborted);

            return TypedResults.Ok<IReadOnlyList<T>>(items.ToList());
        })
            .WithName($"List{entityName}s")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary($"Lists every {displayName}, optionally of one source.");

        group.MapGet("{id}", async Task<Results<Ok<T>, ProblemHttpResult>> (string id, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager) =>
        {
            if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Knowledge, permission))
            {
                return Forbidden();
            }

            var item = await manager.FindByIdAsync(id, httpContext.RequestAborted);

            return item is null ? NotFound() : TypedResults.Ok(item);
        })
            .WithName($"Get{entityName}")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary($"Gets a {displayName}.");

        group.MapPost(string.Empty, async Task<Results<Created<T>, ValidationProblem, ProblemHttpResult>> (JsonObject body, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager) =>
        {
            if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Knowledge, permission))
            {
                return Forbidden();
            }

            var source = body?["Source"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(source))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["source"] = [$"Name the {displayName}'s source."] });
            }

            body.Remove(nameof(CatalogItem.ItemId));
            var item = await manager.NewAsync(source, body, httpContext.RequestAborted);
            var validation = await manager.ValidateAsync(item, httpContext.RequestAborted);

            if (!validation.Succeeded)
            {
                return Invalid(validation);
            }

            await manager.CreateAsync(item, httpContext.RequestAborted);

            return TypedResults.Created($"/api/ai/v1/{path}/{item.ItemId}", item);
        })
            .WithName($"Create{entityName}")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary($"Creates a {displayName} from the JSON its recipe step takes, with its Source.");

        group.MapPut("{id}", async Task<Results<Ok<T>, ValidationProblem, ProblemHttpResult>> (string id, JsonObject body, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager) =>
        {
            if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Knowledge, permission))
            {
                return Forbidden();
            }

            var item = await manager.FindByIdAsync(id, httpContext.RequestAborted);

            if (item is null)
            {
                return NotFound();
            }

            // The body is bound without regard to case, while the record's own JSON is not: its keys take the record's
            // spelling first, so the copy that is validated and the update that is saved read the same values.
            var current = JsonSerializer.SerializeToNode(item) as JsonObject ?? [];
            var changes = NormalizeKeys(body, current);
            changes.Remove(nameof(CatalogItem.ItemId));
            changes.Remove("Source");

            // Validate a copy first: the manager saves as it updates.
            var copy = await manager.NewAsync(item.Source, Merge(current, changes), httpContext.RequestAborted);
            copy.ItemId = item.ItemId;
            var validation = await manager.ValidateAsync(copy, httpContext.RequestAborted);

            if (!validation.Succeeded)
            {
                return Invalid(validation);
            }

            await manager.UpdateAsync(item, changes, httpContext.RequestAborted);

            return TypedResults.Ok(item);
        })
            .WithName($"Update{entityName}")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .WithSummary($"Updates a {displayName} with the given settings, leaving the rest as they are.");

        group.MapDelete("{id}", async Task<Results<NoContent, ProblemHttpResult>> (string id, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager) =>
        {
            if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Knowledge, permission))
            {
                return Forbidden();
            }

            var item = await manager.FindByIdAsync(id, httpContext.RequestAborted);

            if (item is null)
            {
                return NotFound();
            }

            await manager.DeleteAsync(item, httpContext.RequestAborted);

            return TypedResults.NoContent();
        })
            .WithName($"Delete{entityName}")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary($"Deletes a {displayName}.");

        return routes;
    }

    /// <summary>
    /// Merges changes into a JSON object, object by object. A change whose key differs from one of the target's only in
    /// case changes that key, so the target never holds the same name twice.
    /// </summary>
    /// <param name="target">The object changed.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The target.</returns>
    public static JsonObject Merge(JsonObject target, JsonObject changes)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (var (key, value) in changes ?? [])
        {
            var targetKey = FindKey(target, key) ?? key;

            if (value is JsonObject child && target[targetKey] is JsonObject existing)
            {
                Merge(existing, child);

                continue;
            }

            target[targetKey] = value?.DeepClone();
        }

        return target;
    }

    /// <summary>
    /// Returns a copy of posted changes whose keys, at every depth, are spelled as the record's are, and in which two keys
    /// that differ only in case are one. A request body is bound without regard to case but a record's own JSON is not,
    /// so without this a check made on the merged record and the update that is saved could read different values.
    /// </summary>
    /// <param name="changes">The posted changes.</param>
    /// <param name="reference">The record's current JSON, or <see langword="null"/>.</param>
    /// <returns>The normalized changes, a new object.</returns>
    public static JsonObject NormalizeKeys(JsonObject changes, JsonObject reference)
    {
        var result = new JsonObject();

        foreach (var (key, value) in changes ?? [])
        {
            var canonical = FindKey(reference, key) ?? FindKey(result, key) ?? key;
            var normalized = value is JsonObject child
                ? NormalizeKeys(child, reference?[canonical] as JsonObject)
                : value?.DeepClone();

            if (normalized is JsonObject normalizedObject && result[canonical] is JsonObject existing)
            {
                Merge(existing, normalizedObject);

                continue;
            }

            result[canonical] = normalized;
        }

        return result;
    }

    /// <summary>
    /// Turns a validation result into a validation problem.
    /// </summary>
    /// <param name="validation">The result.</param>
    /// <returns>The problem.</returns>
    public static ValidationProblem Invalid(ValidationResultDetails validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        return TypedResults.ValidationProblem(validation.Errors
            .GroupBy(error => error.MemberNames?.FirstOrDefault() ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()));
    }

    private static string FindKey(JsonObject node, string key)
    {
        if (node is null)
        {
            return null;
        }

        string match = null;

        foreach (var (name, _) in node)
        {
            if (string.Equals(name, key, StringComparison.Ordinal))
            {
                return name;
            }

            if (match is null && string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                match = name;
            }
        }

        return match;
    }

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    private static ProblemHttpResult NotFound()
        => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found");
}

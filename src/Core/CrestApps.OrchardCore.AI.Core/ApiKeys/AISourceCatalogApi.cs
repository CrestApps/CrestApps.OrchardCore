using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core.ApiKeys;

/// <summary>
/// Maps list, get, create, update and delete endpoints for records kept by source, such as data sources, file sources
/// and web crawlers, in the shape their recipe steps take.
/// </summary>
public static class AISourceCatalogApi
{
    // A JSON object that matches property names without regard to case, as a bound request body does.
    private static readonly JsonNodeOptions _caseInsensitiveNodes = new() { PropertyNameCaseInsensitive = true };

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

        group.MapGet(string.Empty, (string source, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager)
            => ListAsync(source, httpContext, authorizationService, manager, permission))
            .WithName($"List{entityName}s")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary($"Lists every {displayName}, optionally of one source. Secrets are left out.");

        group.MapGet("{id}", (string id, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager)
            => GetAsync(id, httpContext, authorizationService, manager, permission))
            .WithName($"Get{entityName}")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary($"Gets a {displayName}. Secrets are left out.");

        group.MapPost(string.Empty, (JsonObject body, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager, IDataProtectionProvider dataProtectionProvider, IOptions<IngestionSourceSecretOptions> secretOptions)
            => CreateAsync(body, httpContext, authorizationService, manager, new AISourceSecrets(dataProtectionProvider, secretOptions.Value), path, displayName, permission))
            .WithName($"Create{entityName}")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary($"Creates a {displayName} from the JSON its recipe step takes, with its Source.");

        group.MapPut("{id}", (string id, JsonObject body, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager, IDataProtectionProvider dataProtectionProvider, IOptions<IngestionSourceSecretOptions> secretOptions)
            => UpdateAsync(id, body, httpContext, authorizationService, manager, new AISourceSecrets(dataProtectionProvider, secretOptions.Value), permission))
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
    /// Lists the records, optionally of one source, with their secrets left out.
    /// </summary>
    internal static async Task<Results<Ok<IReadOnlyList<T>>, ProblemHttpResult>> ListAsync<T>(string source, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager, Permission permission)
        where T : CatalogItem, ISourceAwareModel
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Knowledge, permission))
        {
            return Forbidden();
        }

        var items = string.IsNullOrWhiteSpace(source)
            ? await manager.GetAllAsync(httpContext.RequestAborted)
            : await manager.GetAsync(source, httpContext.RequestAborted);

        return TypedResults.Ok<IReadOnlyList<T>>(items.Select(WithoutSecrets).ToList());
    }

    /// <summary>
    /// Gets a record with its secrets left out.
    /// </summary>
    internal static async Task<Results<Ok<T>, ProblemHttpResult>> GetAsync<T>(string id, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager, Permission permission)
        where T : CatalogItem, ISourceAwareModel
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Knowledge, permission))
        {
            return Forbidden();
        }

        var item = await manager.FindByIdAsync(id, httpContext.RequestAborted);

        return item is null ? NotFound() : TypedResults.Ok(WithoutSecrets(item));
    }

    /// <summary>
    /// Creates a record from the JSON its recipe step takes, with its source.
    /// </summary>
    internal static async Task<Results<Created<T>, ValidationProblem, ProblemHttpResult>> CreateAsync<T>(JsonObject body, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager, AISourceSecrets secrets, string path, string displayName, Permission permission)
        where T : CatalogItem, ISourceAwareModel
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

        if (RefuseProtectedSecrets<T>(body, stored: null, secrets) is { } refused)
        {
            return refused;
        }

        var item = await manager.NewAsync(source, body, httpContext.RequestAborted);
        var validation = await manager.ValidateAsync(item, httpContext.RequestAborted);

        if (!validation.Succeeded)
        {
            return Invalid(validation);
        }

        await manager.CreateAsync(item, httpContext.RequestAborted);

        return TypedResults.Created($"/api/ai/v1/{path}/{item.ItemId}", item);
    }

    /// <summary>
    /// Updates a record with the given settings, leaving the rest, its other properties included, as they are. A secret
    /// given in clear text is protected; one that is already protected is refused, unless it is the stored one. A stored
    /// secret, such as a data source's API key, is kept when the body leaves it out or sends it blank; when the update
    /// also moves the record to another address or way of signing in, that kept secret is dropped, as the record's editor
    /// and recipe step drop it, so it must be given again.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="id">The record's id.</param>
    /// <param name="body">The settings to change, in the shape the record's recipe step takes.</param>
    /// <param name="httpContext">The request.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The records' manager.</param>
    /// <param name="secrets">What protects the secrets the body gives.</param>
    /// <param name="permission">The permission to manage the records.</param>
    /// <returns>The updated record, or why it was not updated.</returns>
    internal static async Task<Results<Ok<T>, ValidationProblem, ProblemHttpResult>> UpdateAsync<T>(string id, JsonObject body, HttpContext httpContext, IAuthorizationService authorizationService, ISourceCatalogManager<T> manager, AISourceSecrets secrets, Permission permission)
        where T : CatalogItem, ISourceAwareModel
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

        var before = RecipeSecretDestinations.Snapshot(item);

        // The body is bound without regard to case, while the record's own JSON is not: its keys take the record's
        // spelling first, so the copy that is validated and the update that is saved read the same values.
        var current = JsonSerializer.SerializeToNode(item) as JsonObject ?? [];
        var changes = NormalizeKeys(body, current);
        changes.Remove(nameof(CatalogItem.ItemId));
        changes.Remove("Source");

        if (RefuseProtectedSecrets<T>(changes, current, secrets) is { } refused)
        {
            return refused;
        }

        KeepStoredProperties(changes, current);

        // Validate a copy first: the manager saves as it updates. The copy drops the secrets the update would drop, so a
        // required one that must be given again is reported.
        var copy = await manager.NewAsync(item.Source, Merge(current, changes), httpContext.RequestAborted);
        copy.ItemId = item.ItemId;
        RecipeSecretDestinations.DropKeptSecrets(copy, before);
        var validation = await manager.ValidateAsync(copy, httpContext.RequestAborted);

        if (!validation.Succeeded)
        {
            return Invalid(validation);
        }

        await RecipeSecretDestinations.UpdateAsync(item, changes, data => manager.UpdateAsync(item, data, httpContext.RequestAborted));

        return TypedResults.Ok(item);
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
    /// so without this a check made on the merged record and the update that is saved could read different values. The
    /// copy matches keys without regard to case, as the bound body did, so a handler that reads a flat key in its own
    /// spelling finds one posted in another.
    /// </summary>
    /// <param name="changes">The posted changes.</param>
    /// <param name="reference">The record's current JSON, or <see langword="null"/>.</param>
    /// <returns>The normalized changes, a new object.</returns>
    public static JsonObject NormalizeKeys(JsonObject changes, JsonObject reference)
    {
        var result = new JsonObject(_caseInsensitiveNodes);

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

    // A read leaves the secrets out: the protected value is not bound to a record or an address, so whoever holds it could
    // give it to another record that sends it to a host they chose (review 7, F9).
    private static T WithoutSecrets<T>(T item)
        where T : CatalogItem
    {
        var json = JsonSerializer.SerializeToNode(item, item.GetType());
        IngestionSourceSecrets.Remove(json);

        return (T)json.Deserialize(item.GetType());
    }

    // A secret the body gives in clear text is protected as the record's editor protects it; one that already reads back
    // under its purpose is refused, since a caller can only have it from another record.
    private static ValidationProblem RefuseProtectedSecrets<T>(JsonObject body, JsonObject stored, AISourceSecrets secrets)
    {
        var refused = IngestionSourceSecretProtection.Protect(
            typeof(T),
            Child(body, nameof(ExtensibleEntity.Properties)),
            Child(stored, nameof(ExtensibleEntity.Properties)),
            secrets.Options,
            secrets.DataProtectionProvider,
            refuseProtected: true);

        return refused.Count == 0
            ? null
            : TypedResults.ValidationProblem(refused.ToDictionary(
                member => member,
                member => new[] { "Send the secret itself. A protected value is not accepted: a read never returns one, so it can only come from another record." }));
    }

    // The package's handler replaces every property with the posted ones, so the posted properties are merged into the
    // stored ones: what the body leaves out stays, and a secret it leaves out or sends blank keeps the stored one. The rule
    // that drops a kept secret when the record's address changes then applies to it.
    private static void KeepStoredProperties(JsonObject changes, JsonObject current)
    {
        var key = FindKey(changes, nameof(ExtensibleEntity.Properties));

        if (key is null || changes[key] is not JsonObject posted || Child(current, nameof(ExtensibleEntity.Properties)) is not { } stored)
        {
            return;
        }

        var merged = Merge(stored.DeepClone().AsObject(), posted);
        IngestionSourceSecrets.KeepStored(merged, stored);
        changes[key] = merged;
    }

    private static JsonObject Child(JsonObject node, string name)
        => FindKey(node, name) is { } key ? node[key] as JsonObject : null;

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

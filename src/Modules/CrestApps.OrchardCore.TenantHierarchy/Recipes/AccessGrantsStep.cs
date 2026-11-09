using System.Text.Json.Nodes;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Recipes;

/// <summary>
/// Adds or updates access grants from a <c>TenantHierarchyAccessGrants</c> recipe step: who in the parent tenant may
/// sign in to child tenants, and with which roles there. Each grant goes through the access grant manager, as on the
/// admin screen, so a user is found by user name or email, a role must exist, and a grant for the same user or role and
/// child tenant is updated instead of added again.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "TenantHierarchyAccessGrants", "Grants": [
///   { "PrincipalType": "Role", "Principal": "Administrator", "ChildRoles": [ "Administrator" ] },
///   { "PrincipalType": "User", "Principal": "ana", "ChildSlug": "north", "ChildRoles": [ "Editor" ] } ] }
/// </code>
/// A grant without a child tenant covers every child tenant. A child tenant is named by its registry entry id
/// (<c>ChildEntryId</c>), or by its <c>ChildSlug</c> when the entry id isn't known on this site. Grants carry no secret.
/// </remarks>
internal sealed class AccessGrantsStep : NamedRecipeStepHandler
{
    public const string EntriesName = "Grants";

    private readonly AccessGrantManager _grantManager;
    private readonly ChildTenantEntryStore _entryStore;

    public AccessGrantsStep(AccessGrantManager grantManager, ChildTenantEntryStore entryStore)
        : base(TenantHierarchyConstants.Recipes.AccessGrants)
    {
        _grantManager = grantManager;
        _entryStore = entryStore;
    }

    /// <summary>
    /// Writes a grant the way this step reads it.
    /// </summary>
    /// <param name="grant">The grant.</param>
    /// <param name="entry">The child tenant the grant is for, or <see langword="null"/> for every child tenant.</param>
    /// <returns>The JSON of the grant.</returns>
    internal static JsonObject Export(AccessGrant grant, ChildTenantEntry entry)
    {
        var node = new JsonObject
        {
            ["PrincipalType"] = grant.PrincipalType.ToString(),
            ["Principal"] = grant.PrincipalName ?? grant.PrincipalId,
            ["ChildRoles"] = new JsonArray([.. (grant.ChildRoles ?? []).Select(role => (JsonNode)role)]),
        };

        if (!string.IsNullOrEmpty(grant.ChildEntryId))
        {
            node["ChildEntryId"] = grant.ChildEntryId;
            node["ChildSlug"] = entry?.Slug;
        }

        return node;
    }

    /// <summary>
    /// Reads a grant of the step.
    /// </summary>
    /// <param name="node">The JSON of the grant.</param>
    /// <returns>The grant's request, or <see langword="null"/> when its principal type isn't known.</returns>
    internal static AccessGrantRequest Read(JsonObject node)
    {
        if (!Enum.TryParse<AccessGrantPrincipalType>(Text(node, "PrincipalType"), ignoreCase: true, out var principalType))
        {
            return null;
        }

        return new AccessGrantRequest(
            principalType,
            Text(node, "Principal"),
            Text(node, "ChildEntryId"),
            Text(node, "ChildSlug"),
            [.. (node["ChildRoles"] as JsonArray ?? []).Select(role => role is JsonValue value && value.TryGetValue<string>(out var text) ? text : null).Where(role => !string.IsNullOrWhiteSpace(role))]);
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var node in (context.Step[EntriesName] as JsonArray ?? []).OfType<JsonObject>())
        {
            var request = Read(node);

            if (request is null)
            {
                context.Errors.Add($"A grant in the {TenantHierarchyConstants.Recipes.AccessGrants} step needs a PrincipalType of User or Role.");

                continue;
            }

            string childEntryId = null;

            if (!string.IsNullOrEmpty(request.ChildEntryId) || !string.IsNullOrEmpty(request.ChildSlug))
            {
                var entry = (string.IsNullOrEmpty(request.ChildEntryId) ? null : await _entryStore.FindByEntryIdAsync(request.ChildEntryId))
                    ?? (string.IsNullOrEmpty(request.ChildSlug) ? null : await _entryStore.FindBySlugAsync(request.ChildSlug));

                if (entry is null)
                {
                    context.Errors.Add($"The grant for '{request.Principal}' in the {TenantHierarchyConstants.Recipes.AccessGrants} step names a child tenant that doesn't exist: '{request.ChildSlug ?? request.ChildEntryId}'.");

                    continue;
                }

                childEntryId = entry.EntryId;
            }

            var result = await _grantManager.AddAsync(request.PrincipalType, request.Principal, childEntryId, request.ChildRoles);

            if (!result.Succeeded)
            {
                context.Errors.Add($"The grant for '{request.Principal}' in the {TenantHierarchyConstants.Recipes.AccessGrants} step was refused: {result.Error}");
            }
        }
    }

    private static string Text(JsonObject node, string name)
        => node[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
}

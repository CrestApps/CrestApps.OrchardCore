using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.Models;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Accounts.AI;

/// <summary>
/// Removes a location from an account, as clearing its row in the locations editor does. It needs <c>EditContent</c> on
/// the account. Orders and work orders keep the address they were given: they store a copy of it, not the location id.
/// </summary>
internal sealed class DeleteAccountLocationTool : AccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAccountLocation";

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The account's id, from listAccounts."
        },
        "locationId": {
          "type": "string",
          "description": "The id of the location to remove, from listAccounts."
        },
        "saveAsDraft": {
          "type": "boolean",
          "description": "{{SaveAsDraftDescription}}"
        }
      },
      "required": ["contentItemId", "locationId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Removes a location from an account by its location id. Orders and work orders that already used it keep their copy of the address.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (account, error) = await LoadAccountAsync(services, ReadString(arguments, "contentItemId"), AccountsConstants.ContentParts.Locations);

        if (account is null)
        {
            return error;
        }

        if (!await IsAuthorizedAsync(services, caller, CommonPermissions.EditContent, account))
        {
            return NotAllowedMessage;
        }

        var locationId = ReadString(arguments, "locationId");
        var locations = GetPart<AccountLocationsPart>(account)?.Locations?.Where(location => location is not null).ToList() ?? [];
        var location = locationId is null ? null : locations.FirstOrDefault(item => string.Equals(item.LocationId, locationId, StringComparison.Ordinal));

        if (location is null)
        {
            return $"Nothing was removed: the account has no location with the id '{locationId}'. Use listAccounts for its locations.";
        }

        var remaining = locations.Where(item => !ReferenceEquals(item, location)).ToList();
        var (item, published, saveError) = await SaveAccountAsync(services, caller, account, ReadBool(arguments, "saveAsDraft") == true, version =>
            version.Alter<AccountLocationsPart>(part => part.Locations = remaining));

        if (item is null)
        {
            return saveError;
        }

        return Json(new
        {
            contentItemId = item.ContentItemId,
            account = item.DisplayText,
            locationId = location.LocationId,
            name = location.Name,
            removed = true,
            remainingLocations = remaining.Count,
            published,
            savedAsDraft = !published,
            message = published
                ? $"The location '{location.Name}' was removed and the account was published. Orders and work orders that used it keep their copy of the address."
                : $"The location '{location.Name}' was removed in a draft. The published account, if any, keeps it until the draft is published.",
        });
    }
}

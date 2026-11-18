using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Accounts.AI;

/// <summary>
/// Adds a location to an account, or changes one by its location id, with the locations editor's rules
/// (<see cref="AccountLocationRules"/>): the address is addressed to the account and checked by the shared address editor
/// against its country's rules, a location keeps its id, and a location with no name is named after its city. It needs
/// <c>EditContent</c> on the account.
/// </summary>
internal sealed class SaveAccountLocationTool : AccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAccountLocation";

    private static readonly JsonElement _jsonSchema = JsonSerializer.SerializeToElement(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["contentItemId"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The account's id, from listAccounts.",
            },
            ["locationId"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The id of the location to change, from listAccounts. Leave it out to add a location.",
            },
            ["name"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The location's name, such as 'Head office' or 'Oakland warehouse'. A new location with no name is named after its city.",
            },
            ["address"] = AddressArguments.CreateSchema("The location's address, with its two-letter country code. Required for a new location; when changing a location, leave it out to keep its address. The account's name is used as the company.", includeRecipient: false),
            ["saveAsDraft"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] = SaveAsDraftDescription,
            },
        },
        ["required"] = new JsonArray("contentItemId"),
        ["additionalProperties"] = false,
    });

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds a named location to an account or updates one by its location id, after checking the address against its country's rules.";

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

        var locations = GetPart<AccountLocationsPart>(account)?.Locations?.Where(location => location is not null).ToList() ?? [];
        var locationId = ReadString(arguments, "locationId");
        var existing = locationId is null ? null : locations.FirstOrDefault(location => string.Equals(location.LocationId, locationId, StringComparison.Ordinal));

        if (locationId is not null && existing is null)
        {
            return $"Nothing was changed: the account has no location with the id '{locationId}'. Use listAccounts for its locations, or leave locationId out to add one.";
        }

        Address address;

        if (ReadJson(arguments, "address") is { } addressJson)
        {
            if (!AddressArguments.TryRead(addressJson, includeRecipient: false, out var entered, out var addressError))
            {
                return $"Nothing was changed: {addressError}";
            }

            var editor = AddressEditorViewModel.From(entered);

            AccountLocationRules.AddressToAccount(editor, account.DisplayText);

            var modelState = new ModelStateDictionary();
            var result = await services.GetRequiredService<IAddressEditorService>().ValidateAsync(
                editor,
                modelState,
                prefix: null,
                AccountLocationRules.EditorOptions(locations.Select(location => location.Address?.Country)));

            if (!result.IsValid)
            {
                var messages = modelState.Values.SelectMany(entry => entry.Errors).Select(entry => entry.ErrorMessage).Where(message => !string.IsNullOrEmpty(message));

                return $"Nothing was changed: the address does not have its country's form. {string.Join(" ", messages)} Use validateAddress to see which fields the country needs.";
            }

            address = result.Address;
        }
        else if (existing is null)
        {
            return "Nothing was changed: a new location needs an address.";
        }
        else
        {
            address = existing.Address;
        }

        var name = Has(arguments, "name") ? ReadString(arguments, "name") : existing?.Name;
        var location = AccountLocationRules.CreateLocation(existing?.LocationId, name, address);
        List<AccountLocation> updated = existing is null
            ? [.. locations, location]
            : locations.Select(item => item.LocationId == existing.LocationId ? location : item).ToList();

        var (item, published, saveError) = await SaveAccountAsync(services, caller, account, ReadBool(arguments, "saveAsDraft") == true, version =>
            version.Alter<AccountLocationsPart>(part => part.Locations = updated));

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
            address = AddressArguments.Describe(location.Address),
            created = existing is null,
            published,
            savedAsDraft = !published,
            message = (existing is null ? "The location was added" : "The location was updated")
                + (published ? " and the account was published." : " in a draft. The published account, if any, keeps its current locations until the draft is published."),
        });
    }
}

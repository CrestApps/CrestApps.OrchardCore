using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Marketplace.Recipes;

/// <summary>
/// Adds or updates marketplace vendors from a <c>Vendors</c> recipe step. A vendor is matched by its <c>ItemId</c>,
/// then by its name, so a second run changes it instead of adding another. A vendor needs a name and a commission from
/// 0 to 100 percent or none, and its users are named by <c>UserNames</c>, which are looked up on this site, as on the
/// vendor's admin screen.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "Vendors", "Vendors": [ { "ItemId": "...", "Name": "Acme Crafts", "Email": "sales@acme.test", "UserNames": [ "acme" ], "CommissionPercent": 12.5, "Status": "Active" } ] }
/// </code>
/// The Stripe Connect account belongs to the site the vendor connected on, so a vendor's account id, its pending
/// onboarding key and whether it is paid out never travel; an imported vendor connects again on the new site.
/// </remarks>
internal sealed class VendorsStep : CatalogStoreRecipeStep<Vendor>
{
    public const string EntriesName = "Vendors";

    /// <summary>
    /// The members of a vendor that belong to the site its Stripe Connect account was made on.
    /// </summary>
    public static readonly string[] SiteOwnedMembers =
    [
        nameof(Vendor.UserIds),
        nameof(Vendor.StripeAccountId),
        nameof(Vendor.StripeAccountRequestKey),
        nameof(Vendor.PayoutsEnabled),
    ];

    private readonly IVendorStore _store;
    private readonly IUserService _userService;

    internal readonly IStringLocalizer S;

    public VendorsStep(
        IVendorStore store,
        IUserService userService,
        IStringLocalizer<VendorsStep> stringLocalizer)
        : base(MarketplaceConstants.Recipes.Vendors, EntriesName, store)
    {
        _store = store;
        _userService = userService;
        S = stringLocalizer;
    }

    protected override bool CanImport(JsonObject token, RecipeExecutionContext context)
    {
        // A recipe can't connect a vendor to a payout account: the account is made on this site.
        foreach (var member in SiteOwnedMembers)
        {
            token.Remove(member);
        }

        return true;
    }

    protected override async ValueTask<CatalogRecipeLookup<Vendor>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var name = token[nameof(Vendor.Name)]?.GetValue<string>()?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return CatalogRecipeLookup<Vendor>.NotFound;
        }

        var vendor = (await _store.ListAllAsync()).FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

        return vendor is null ? CatalogRecipeLookup<Vendor>.NotFound : CatalogRecipeLookup<Vendor>.Found(vendor);
    }

    protected override void Preparing(Vendor entry, JsonObject token)
    {
        entry.Name = entry.Name?.Trim();
        entry.Email = string.IsNullOrWhiteSpace(entry.Email) ? null : entry.Email.Trim();
        entry.UserNames ??= [];
    }

    /// <inheritdoc/>
    /// <remarks>The vendor's users are looked up by name here, and their ids on this site are kept on the vendor.</remarks>
    protected override async ValueTask<ValidationResultDetails> ValidateAsync(Vendor entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.Name))
        {
            result.Fail(new ValidationResult(S["Enter the vendor's name."]));
        }

        if (entry.CommissionPercent is < 0m or > 100m)
        {
            result.Fail(new ValidationResult(S["Enter the commission as a percent from 0 to 100, or leave it empty for the store's."]));
        }

        var userIds = new List<string>();
        var userNames = new List<string>();

        foreach (var name in entry.UserNames.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await _userService.GetUserAsync(name) is User user)
            {
                userIds.Add(user.UserId);
                userNames.Add(user.UserName);
            }
            else
            {
                result.Fail(new ValidationResult(S["There's no user named {0}.", name]));
            }
        }

        entry.UserIds = userIds;
        entry.UserNames = userNames;

        return result;
    }
}

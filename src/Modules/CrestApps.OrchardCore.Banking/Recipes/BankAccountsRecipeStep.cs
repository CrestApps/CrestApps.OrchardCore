using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Parties.Models;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Banking.Recipes;

/// <summary>
/// Imports bank accounts. An account is matched by its id and created when none matches. Numbers are read in clear
/// text, checked and protected with this site's keys; an account without numbers is imported as unverified, as a
/// deployment without sensitive data writes it, and may carry the last digits it is known by
/// (<c>AccountNumberLastFour</c>). <c>Properties</c> carries what other features keep on the account, such
/// as the Bank file (ACH) settings (<c>NachaOriginSettings</c>) of one of the business's own accounts.
/// </summary>
internal sealed class BankAccountsRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The property that holds the accounts.
    /// </summary>
    public const string AccountsProperty = "Accounts";

    private readonly IBankAccountManager _manager;
    private readonly IBankAccountService _bankAccountService;

    public BankAccountsRecipeStep(
        IBankAccountManager manager,
        IBankAccountService bankAccountService)
        : base(BankingConstants.RecipeStepName)
    {
        _manager = manager;
        _bankAccountService = bankAccountService;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var node in (context.Step[AccountsProperty] as JsonArray ?? []).OfType<JsonObject>())
        {
            var itemId = Text(node, nameof(BankAccount.ItemId));
            var account = string.IsNullOrEmpty(itemId) ? null : await _manager.FindByIdAsync(itemId);
            var isNew = account is null;

            if (isNew)
            {
                account = await _manager.NewAsync();

                if (!string.IsNullOrEmpty(itemId))
                {
                    account.ItemId = itemId;
                }
            }

            account.Name = Text(node, nameof(BankAccount.Name)) ?? account.Name;
            account.HolderName = Text(node, nameof(BankAccount.HolderName)) ?? account.HolderName;
            account.BankName = Text(node, nameof(BankAccount.BankName)) ?? account.BankName;
            account.Country = Text(node, nameof(BankAccount.Country))?.ToUpperInvariant() ?? account.Country;
            account.Currency = Text(node, nameof(BankAccount.Currency))?.ToUpperInvariant() ?? account.Currency;

            if (Enum.TryParse<BankAccountKind>(Text(node, nameof(BankAccount.Kind)), ignoreCase: true, out var kind))
            {
                account.Kind = kind;
            }

            var ownerKind = Text(node, "OwnerKind");
            var ownerId = Text(node, "OwnerId");

            if (node.ContainsKey("OwnerKind") || node.ContainsKey("OwnerId"))
            {
                account.Owner = string.IsNullOrEmpty(ownerKind) || string.IsNullOrEmpty(ownerId) ? default : new PartyRef(ownerKind, ownerId);
            }

            // What other features keep on the account, such as the Bank file (ACH) settings of one of the business's
            // own accounts: each settings object given replaces the stored one of that name.
            if (node[nameof(BankAccount.Properties)] is JsonObject properties)
            {
                // Read the way every catalog entry reads its properties, so the settings are found again.
                var incoming = new BankAccount();

                CatalogDeploymentSerializer.Populate(incoming, new JsonObject { [nameof(BankAccount.Properties)] = properties.DeepClone() });

                foreach (var (key, value) in incoming.Properties)
                {
                    account.Properties[key] = value;
                }
            }

            var errors = _bankAccountService.ApplyNumbers(account, Text(node, nameof(BankAccount.RoutingNumber)), Text(node, nameof(BankAccount.AccountNumber)));

            if (errors.Count > 0)
            {
                context.Errors.Add($"{BankingConstants.RecipeStepName}: '{account.Name}' wasn't imported: {string.Join(" ", errors.Select(error => error.Message))}");

                continue;
            }

            // An account another system knew only by its last digits keeps them; the full number, when given, wins.
            if (account.AccountNumber is not { HasValue: true } && node.ContainsKey(nameof(BankAccount.AccountNumberLastFour)))
            {
                account.AccountNumberLastFour = Text(node, nameof(BankAccount.AccountNumberLastFour));
            }

            // A status travels only with the numbers it was given for: without them the account must be checked again.
            if (BankAccountNumbers.HasNumbers(account)
                && Enum.TryParse<BankAccountStatus>(Text(node, nameof(BankAccount.Status)), ignoreCase: true, out var status)
                && !_bankAccountService.TrySetStatus(account, status, Text(node, nameof(BankAccount.StatusReason))))
            {
                context.Errors.Add($"{BankingConstants.RecipeStepName}: '{account.Name}' can't move from {account.Status} to {status}.");
            }

            var validation = await _manager.ValidateAsync(account);

            if (!validation.Succeeded)
            {
                context.Errors.Add($"{BankingConstants.RecipeStepName}: '{account.Name}' wasn't imported: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

                continue;
            }

            if (isNew)
            {
                await _manager.CreateAsync(account);
            }
            else
            {
                await _manager.UpdateAsync(account);
            }
        }
    }

    private static string Text(JsonObject node, string name)
        => node[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
}

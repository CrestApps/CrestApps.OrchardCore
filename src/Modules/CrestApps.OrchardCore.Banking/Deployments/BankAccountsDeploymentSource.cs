using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Recipes;
using CrestApps.OrchardCore.Banking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Banking.Deployments;

/// <summary>
/// Writes the bank accounts to a deployment plan. Numbers are written in clear text only when the step asks for them
/// and the user running the plan may reveal them; every number written is told to the observers, so the Audit Trail
/// records the export.
/// </summary>
internal sealed class BankAccountsDeploymentSource : DeploymentSourceBase<BankAccountsDeploymentStep>
{
    private readonly IBankAccountManager _manager;
    private readonly IBankAccountService _bankAccountService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public BankAccountsDeploymentSource(
        IBankAccountManager manager,
        IBankAccountService bankAccountService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _manager = manager;
        _bankAccountService = bankAccountService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override async Task ProcessAsync(BankAccountsDeploymentStep step, DeploymentPlanResult result)
    {
        var includeNumbers = step.IncludeSensitiveData && await MayRevealAsync();
        var accounts = new JsonArray();

        foreach (var account in (await _manager.GetAllAsync()).OrderBy(account => account.CreatedUtc))
        {
            var node = new JsonObject
            {
                [nameof(BankAccount.ItemId)] = account.ItemId,
                [nameof(BankAccount.Name)] = account.Name,
                ["OwnerKind"] = account.Owner.IsEmpty ? null : account.Owner.Kind,
                ["OwnerId"] = account.Owner.IsEmpty ? null : account.Owner.Id,
                [nameof(BankAccount.HolderName)] = account.HolderName,
                [nameof(BankAccount.Kind)] = account.Kind.ToString(),
                [nameof(BankAccount.Country)] = account.Country,
                [nameof(BankAccount.Currency)] = account.Currency,
                [nameof(BankAccount.BankName)] = account.BankName,
            };

            if (includeNumbers)
            {
                node[nameof(BankAccount.RoutingNumber)] = account.RoutingNumber;
                node[nameof(BankAccount.AccountNumber)] = await _bankAccountService.RevealAccountNumberAsync(account, "Exported in a deployment plan");
                node[nameof(BankAccount.Status)] = account.Status.ToString();
                node[nameof(BankAccount.StatusReason)] = account.StatusReason;
            }

            accounts.Add(node);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [BankAccountsRecipeStep.AccountsProperty] = accounts,
        });
    }

    // A plan run from a request runs as its user; one run without a request (a remote deployment) keeps the option the
    // step was saved with, which only a user who may reveal numbers can turn on.
    private async Task<bool> MayRevealAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.Identity?.IsAuthenticated != true
            || await _authorizationService.AuthorizeAsync(user, BankingPermissions.RevealBankAccountNumbers);
    }
}

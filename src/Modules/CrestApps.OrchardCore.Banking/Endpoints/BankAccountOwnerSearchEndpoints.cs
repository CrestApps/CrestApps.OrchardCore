using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Banking.Endpoints;

/// <summary>
/// The search behind the bank account owner picker: parties of every kind the enabled features offer, as
/// <c>{ value, text }</c> items whose value is <c>Kind:Id</c>.
/// </summary>
internal static class BankAccountOwnerSearchEndpoints
{
    /// <summary>
    /// The endpoint's route name, which views build its URL from.
    /// </summary>
    public const string RouteName = "CrestApps.Banking.OwnerSearch";

    private const int PerKind = 10;

    public static IEndpointRouteBuilder AddBankAccountOwnerSearchEndpoint(this IEndpointRouteBuilder builder, string adminUrlPrefix)
    {
        // Under the site's admin prefix, so a site that moves the admin moves the picker with it.
        builder.MapGet((string.IsNullOrWhiteSpace(adminUrlPrefix) ? "Admin" : adminUrlPrefix.Trim('/')) + "/api/banking/owners/search", HandleAsync)
            .RequireAuthorization()
            .WithName(RouteName);

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        string query,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IPartyDirectory partyDirectory,
        CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, BankingPermissions.ManageBankAccounts))
        {
            return Results.Forbid();
        }

        var items = new List<OwnerSearchItem>();

        foreach (var kind in partyDirectory.Kinds)
        {
            foreach (var option in await partyDirectory.SearchAsync(kind, query, PerKind, cancellationToken))
            {
                items.Add(new OwnerSearchItem
                {
                    Value = new PartyRef(kind, option.Id).ToString(),
                    Text = $"{option.Name} ({kind})",
                    IsEnabled = true,
                });
            }
        }

        return Results.Ok<IEnumerable<OwnerSearchItem>>(items);
    }

    internal sealed class OwnerSearchItem
    {
        public string Value { get; set; }

        public string Text { get; set; }

        public bool IsEnabled { get; set; }
    }
}

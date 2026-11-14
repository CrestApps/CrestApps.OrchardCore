using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Invoicing.Endpoints;

/// <summary>
/// Finds who an invoice can be billed to, across every kind of party the site has (users, customers, accounts,
/// contacts), for the bill-to selector of the draft editor.
/// </summary>
internal static class InvoicePartySearchEndpoints
{
    public const string RouteName = "CrestApps.Invoicing.PartySearch";

    private const int _perKind = 10;

    public static IEndpointRouteBuilder AddInvoicePartySearchEndpoints(this IEndpointRouteBuilder builder)
    {
        var adminUrlPrefix = builder.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value.AdminUrlPrefix;

        builder.MapGet($"{adminUrlPrefix}/api/crestapps/invoicing/parties/search", HandleAsync)
            .RequireAuthorization()
            .WithName(RouteName);

        return builder;
    }

    /// <summary>
    /// Finds parties of every kind but guests, who are reached only by the contact typed on the invoice.
    /// </summary>
    public static async Task<IReadOnlyList<PartySearchResult>> SearchAsync(IPartyDirectory directory, string query, int perKind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);

        var found = new List<PartySearchResult>();

        foreach (var kind in directory.Kinds)
        {
            if (string.Equals(kind, PartyKinds.Guest, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var option in await directory.SearchAsync(kind, query?.Trim(), perKind, cancellationToken))
            {
                found.Add(new PartySearchResult(new PartyRef(kind, option.Id).ToString(), option.Name, kind));
            }
        }

        return found;
    }

    private static async Task<IResult> HandleAsync(
        string query,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IPartyDirectory directory,
        IStringLocalizer<PartySearchResult> S)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.ManageInvoices))
        {
            return Results.Forbid();
        }

        var found = await SearchAsync(directory, query, _perKind, httpContext.RequestAborted);

        return Results.Ok(found.Select(item => new { value = item.Value, text = item.Text, secondaryText = DescribeKind(item.Kind, S) }));
    }

    private static string DescribeKind(string kind, IStringLocalizer S)
        => kind switch
        {
            PartyKinds.User => S["User"].Value,
            PartyKinds.Customer => S["Customer"].Value,
            PartyKinds.Account => S["Business account"].Value,
            PartyKinds.Contact => S["Contact"].Value,
            _ => kind,
        };
}

/// <summary>
/// A party found for the bill-to selector: its <c>Kind:Id</c>, its name and its kind.
/// </summary>
internal sealed record PartySearchResult(string Value, string Text, string Kind);

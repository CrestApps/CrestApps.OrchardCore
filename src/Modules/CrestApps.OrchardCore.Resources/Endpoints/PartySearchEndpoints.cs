using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Resources.Endpoints;

/// <summary>
/// The search behind the party picker: people and organizations of the asked kinds as <c>{ value, text,
/// secondaryText }</c> items, where the value is the party reference (<c>Kind:Id</c>). A kind is searched only when the
/// module that keeps it described it and the user holds its search permission.
/// </summary>
internal static class PartySearchEndpoints
{
    public const string RouteName = "CrestApps.Parties.Search";

    public const string Pattern = "Admin/api/crestapps/parties/search";

    private const int MaxResults = 30;

    public static IEndpointRouteBuilder AddPartySearchEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(Pattern, HandleAsync)
            .RequireAuthorization()
            .WithName(RouteName);

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        string kinds,
        string query,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IPartyDirectory directory,
        IOptions<PartySourceOptions> options,
        IStringLocalizerFactory localizerFactory,
        CancellationToken cancellationToken)
    {
        var searchable = new List<PartyKindDescriptor>();

        foreach (var kind in SplitKinds(kinds, directory))
        {
            if (directory.GetSource(kind) is not null
                && options.Value.Kinds.TryGetValue(kind, out var descriptor)
                && descriptor.SearchPermission is not null
                && await authorizationService.AuthorizeAsync(httpContext.User, descriptor.SearchPermission))
            {
                searchable.Add(descriptor);
            }
        }

        if (searchable.Count == 0)
        {
            return Results.Forbid();
        }

        var perKind = Math.Max(MaxResults / searchable.Count, 5);
        var items = new List<PartySearchItem>();

        foreach (var descriptor in searchable)
        {
            var kindName = descriptor.DisplayName is null ? descriptor.Kind : localizerFactory.Localize(descriptor.DisplayName).Value;

            foreach (var party in await directory.SearchAsync(descriptor.Kind, query?.Trim(), perKind, cancellationToken))
            {
                if (string.IsNullOrEmpty(party.Id))
                {
                    continue;
                }

                items.Add(new PartySearchItem
                {
                    Value = new PartyRef(descriptor.Kind, party.Id).ToString(),
                    Text = string.IsNullOrEmpty(party.Name) ? party.Id : party.Name,
                    SecondaryText = searchable.Count > 1 ? kindName : null,
                    IsEnabled = true,
                });
            }
        }

        return Results.Ok<IEnumerable<PartySearchItem>>(items);
    }

    // An empty list asks for every kind that has a source.
    private static IEnumerable<string> SplitKinds(string kinds, IPartyDirectory directory)
        => string.IsNullOrWhiteSpace(kinds)
            ? directory.Kinds
            : kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase);

    internal sealed class PartySearchItem
    {
        public string Value { get; set; }

        public string Text { get; set; }

        public string SecondaryText { get; set; }

        public bool IsEnabled { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Shipping.Handlers;

/// <summary>
/// Stamps and binds a <see cref="ShippingZone"/> through <see cref="AuditedCatalogEntryHandlerBase{T}"/>, normalizes
/// what a recipe gives it, and validates it.
/// </summary>
internal sealed class ShippingZoneHandler : AuditedCatalogEntryHandlerBase<ShippingZone>
{
    private readonly INamedCatalog<ShippingZone> _catalog;

    internal readonly IStringLocalizer S;

    public ShippingZoneHandler(
        IHttpContextAccessor httpContextAccessor,
        INamedCatalog<ShippingZone> catalog,
        IClock clock,
        IStringLocalizer<ShippingZoneHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        _catalog = catalog;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<ShippingZone> context, CancellationToken cancellationToken = default)
    {
        var zone = context.Model;

        if (string.IsNullOrWhiteSpace(zone.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(ShippingZone.Name)]));
        }
        else if (await _catalog.IsNameUsedByAnotherEntryAsync(zone, cancellationToken))
        {
            context.Result.Fail(new ValidationResult(S["A shipping zone with the same name already exists."], [nameof(ShippingZone.Name)]));
        }

        if (!zone.RestOfWorld && zone.Countries.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["Choose the countries the zone covers, or make it the zone for the rest of the world."], [nameof(ShippingZone.Countries)]));
        }

        foreach (var method in zone.Methods.Where(method => string.IsNullOrWhiteSpace(method.Name) || string.IsNullOrWhiteSpace(method.ProviderName)))
        {
            context.Result.Fail(new ValidationResult(S["Each shipping method needs a name and a rate provider."], [nameof(ShippingZone.Methods)]));

            break;
        }
    }

    /// <inheritdoc/>
    protected override void Populate(ShippingZone entry, JsonNode data)
        => PopulateZone(entry, data);

    // Recipes and deployments give the zone as JSON; what they name is copied and normalized, and methods without an id
    // get one.
    internal static void PopulateZone(ShippingZone zone, JsonNode data)
    {
        if (data is not JsonObject json || json.Count == 0)
        {
            return;
        }

        CatalogDeploymentSerializer.Populate(zone, json);

        if (json.ContainsKey(nameof(ShippingZone.Name)))
        {
            zone.Name = zone.Name?.Trim();
        }

        if (json.ContainsKey(nameof(ShippingZone.Countries)))
        {
            zone.Countries = Normalize(zone.Countries);
        }

        if (json.ContainsKey(nameof(ShippingZone.Regions)))
        {
            zone.Regions = Normalize(zone.Regions);
        }

        if (json.ContainsKey(nameof(ShippingZone.Methods)))
        {
            zone.Methods ??= [];

            foreach (var method in zone.Methods.Where(method => string.IsNullOrEmpty(method.MethodId)))
            {
                method.MethodId = UniqueId.GenerateId();
            }
        }
    }

    internal static List<string> Normalize(IEnumerable<string> codes)
        => (codes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
}

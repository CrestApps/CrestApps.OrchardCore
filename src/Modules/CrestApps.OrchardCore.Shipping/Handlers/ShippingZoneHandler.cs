using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Shipping.Handlers;

internal sealed class ShippingZoneHandler : CatalogEntryHandlerBase<ShippingZone>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ShippingZoneHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<ShippingZoneHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<ShippingZone> context, CancellationToken cancellationToken = default)
    {
        Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<ShippingZone> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<ShippingZone> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user is not null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity?.Name;
        }

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<ShippingZone> context, CancellationToken cancellationToken = default)
    {
        var zone = context.Model;

        if (string.IsNullOrWhiteSpace(zone.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(ShippingZone.Name)]));
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

        return Task.CompletedTask;
    }

    // Recipes and deployments give the zone as JSON; what they name is copied, and methods without an id get one.
    internal static void Populate(ShippingZone zone, JsonNode data)
    {
        if (data is not JsonObject json || json.Count == 0)
        {
            return;
        }

        var source = json.Deserialize<ShippingZone>();

        if (source is null)
        {
            return;
        }

        if (json.ContainsKey(nameof(ShippingZone.Name)))
        {
            zone.Name = source.Name?.Trim();
        }

        if (json.ContainsKey(nameof(ShippingZone.Position)))
        {
            zone.Position = source.Position;
        }

        if (json.ContainsKey(nameof(ShippingZone.RestOfWorld)))
        {
            zone.RestOfWorld = source.RestOfWorld;
        }

        if (json.ContainsKey(nameof(ShippingZone.Countries)))
        {
            zone.Countries = Normalize(source.Countries);
        }

        if (json.ContainsKey(nameof(ShippingZone.Regions)))
        {
            zone.Regions = Normalize(source.Regions);
        }

        if (json.ContainsKey(nameof(ShippingZone.Methods)))
        {
            zone.Methods = source.Methods ?? [];

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

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.TimeZones.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TimeZones.Handlers;

internal sealed class TimeZoneMapHandler : AuditedCatalogEntryHandlerBase<TimeZoneMap>
{
    private readonly ICatalog<TimeZoneMap> _catalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneMapHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeZoneMapHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        ICatalog<TimeZoneMap> catalog,
        IStringLocalizer<TimeZoneMapHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        _catalog = catalog;
        S = stringLocalizer;
    }

    public override async Task ValidatingAsync(ValidatingContext<TimeZoneMap> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(TimeZoneMap.Name)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.TimeZoneId))
        {
            context.Result.Fail(new ValidationResult(S["Time zone is required."], [nameof(TimeZoneMap.TimeZoneId)]));

            return;
        }

        var normalizedTimeZoneId = NormalizeTimeZoneId(context.Model.TimeZoneId);

        if (normalizedTimeZoneId is null)
        {
            context.Result.Fail(new ValidationResult(S["The selected time zone is invalid."], [nameof(TimeZoneMap.TimeZoneId)]));

            return;
        }

        context.Model.TimeZoneId = normalizedTimeZoneId;

        var existingMaps = await _catalog.GetAllAsync(cancellationToken);
        var existingMap = existingMaps.FirstOrDefault(map =>
            string.Equals(map.TimeZoneId, normalizedTimeZoneId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(map.ItemId, context.Model.ItemId, StringComparison.OrdinalIgnoreCase));

        if (existingMap != null)
        {
            context.Result.Fail(new ValidationResult(S["A map for the selected time zone already exists."], [nameof(TimeZoneMap.TimeZoneId)]));
        }
    }

    /// <inheritdoc/>
    protected override void Populate(TimeZoneMap model, JsonNode data)
    {
        if (data is null)
        {
            return;
        }

        var name = data[nameof(TimeZoneMap.Name)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            model.Name = name;
        }

        var timeZoneId = data[nameof(TimeZoneMap.TimeZoneId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(timeZoneId))
        {
            model.TimeZoneId = NormalizeTimeZoneId(timeZoneId) ?? timeZoneId;
        }

    }

    private static string NormalizeTimeZoneId(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        return NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId.Trim())?.Id;
    }
}

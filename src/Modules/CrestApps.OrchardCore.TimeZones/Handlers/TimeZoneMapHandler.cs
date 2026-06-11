using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeZones.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TimeZones.Handlers;

internal sealed class TimeZoneMapHandler : CatalogEntryHandlerBase<TimeZoneMap>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
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
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _catalog = catalog;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<TimeZoneMap> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity?.Name;
        }

        return PopulateAsync(context.Model, context.Data);
    }

    public override Task UpdatingAsync(UpdatingContext<TimeZoneMap> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return PopulateAsync(context.Model, context.Data);
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

    private static Task PopulateAsync(TimeZoneMap model, JsonNode data)
    {
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

        var ownerId = data[nameof(TimeZoneMap.OwnerId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(ownerId))
        {
            model.OwnerId = ownerId;
        }

        var author = data[nameof(TimeZoneMap.Author)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(author))
        {
            model.Author = author;
        }

        var createdUtc = data[nameof(TimeZoneMap.CreatedUtc)]?.GetValue<DateTime?>();

        if (createdUtc.HasValue)
        {
            model.CreatedUtc = createdUtc.Value;
        }

        var modifiedUtc = data[nameof(TimeZoneMap.ModifiedUtc)]?.GetValue<DateTime?>();

        if (modifiedUtc.HasValue)
        {
            model.ModifiedUtc = modifiedUtc.Value;
        }

        return Task.CompletedTask;
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

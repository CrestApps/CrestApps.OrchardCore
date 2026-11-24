using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using YesSql;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Assets.AI;

/// <summary>
/// Argument reading, JSON, times and look-ups shared by the Assets AI tools.
/// </summary>
internal static class AssetTools
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    public static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    public static bool Has(AIFunctionArguments arguments, string name)
        => arguments is not null && arguments.TryGetValue(name, out var value) && value is not null && value is not JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

    /// <summary>
    /// Reads a text argument, trimmed; empty text is <see langword="null"/>.
    /// </summary>
    public static string Read(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var value = arguments[name];
        var text = value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public static int? ReadInt(AIFunctionArguments arguments, string name)
    {
        var text = Read(arguments, name);

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && real == Math.Floor(real) && real is >= int.MinValue and <= int.MaxValue
            ? (int)real
            : null;
    }

    public static decimal? ReadDecimal(AIFunctionArguments arguments, string name)
        => decimal.TryParse(Read(arguments, name), NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Reads an enum value by name, ignoring case; numbers are refused, so a stray number never picks a value by position.
    /// </summary>
    public static bool TryReadEnum<TEnum>(AIFunctionArguments arguments, string name, out TEnum value)
        where TEnum : struct, Enum
    {
        var text = Read(arguments, name);

        return Enum.TryParse(text, ignoreCase: true, out value) && Enum.IsDefined(value) && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    public static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

    public static string Day(DateOnly? date)
        => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a stored UTC time in the user's or the site's time zone.
    /// </summary>
    public static async Task<string> LocalAsync(ILocalClock localClock, DateTime? utc)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc)));

        return local.ToString("yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets today's date in the user's or the site's time zone.
    /// </summary>
    public static async Task<DateOnly> TodayAsync(IServiceProvider services)
        => DateOnly.FromDateTime((await services.GetRequiredService<ILocalClock>().GetLocalNowAsync()).DateTime);

    /// <summary>
    /// Finds an asset by its ID or its number (such as A-000123).
    /// </summary>
    public static async Task<Asset> FindAsync(IServiceProvider services, string idOrNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idOrNumber))
        {
            return null;
        }

        var asset = await services.GetRequiredService<AssetManager>().FindByIdAsync(idOrNumber, cancellationToken);

        if (asset is not null)
        {
            return asset;
        }

        var number = idOrNumber.Trim();

        return await services.GetRequiredService<ISession>()
            .Query<Asset, AssetIndex>(index => index.Number == number, collection: AssetsConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets an asset's owner, as the Assets pages show it.
    /// </summary>
    public static async Task<object> OwnerAsync(AssetOwnerDirectory owners, Asset asset, CancellationToken cancellationToken)
    {
        if (asset.OwnerKind == AssetOwnerKind.Internal)
        {
            return new { kind = asset.OwnerKind.ToString(), id = (string)null, name = (string)null };
        }

        return new
        {
            kind = asset.OwnerKind.ToString(),
            id = asset.OwnerId,
            name = await owners.GetNameAsync(asset.OwnerKind, asset.OwnerId, cancellationToken) ?? asset.OwnerId,
        };
    }
}

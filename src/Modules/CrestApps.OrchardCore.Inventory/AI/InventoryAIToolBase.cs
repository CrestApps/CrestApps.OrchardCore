using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// An inventory AI tool. It acts as the person using the AI: it refuses a caller who is not signed in or who lacks the
/// permission of the Inventory page it stands for (<see cref="InventoryPermissions.ViewInventory"/> to read,
/// <see cref="InventoryPermissions.ManageInventory"/> to change) before it reads an argument, and refuses any argument its
/// schema does not declare. A stock change is recorded as a movement with the caller as its actor and the reason given,
/// and is committed at once, as the Inventory page does, so a checkout changing the same stock at the same moment is
/// never overwritten.
/// </summary>
internal abstract class InventoryAIToolBase : AIFunction
{
    /// <summary>
    /// The answer to a caller who is not signed in or lacks the permission.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    /// <summary>
    /// The default number of entries a list returns.
    /// </summary>
    protected const int DefaultPageSize = 20;

    /// <summary>
    /// The most entries a list returns.
    /// </summary>
    protected const int MaxPageSize = 50;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private HashSet<string> _argumentNames;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <summary>
    /// Gets the permission the caller needs.
    /// </summary>
    protected abstract Permission Permission { get; }

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var caller = GetCaller(services);
        var authorizationService = services.GetService<IAuthorizationService>();

        if (caller is null || authorizationService is null || !await authorizationService.AuthorizeAsync(caller, Permission))
        {
            return NotAllowedMessage;
        }

        _argumentNames ??= GetArgumentNames(JsonSchema);

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was done: '{unknown}' is not an argument of {Name}. Use only the arguments the tool declares.";
        }

        return await InvokeAsync(arguments, services, caller, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller who has the permission.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the signed-in caller: the user the chat hubs publish through <see cref="IUserAccessor"/>, or the request's user.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
    protected static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }

    /// <summary>
    /// Finds a stock item by its id.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="stockItemId">The stock item's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stock item, or the sentence that says it was not found.</returns>
    protected static async Task<(StockItem Item, string Error)> FindItemAsync(IServiceProvider services, string stockItemId, CancellationToken cancellationToken)
    {
        var item = stockItemId is null ? null : await services.GetRequiredService<IStockItemStore>().FindByIdAsync(stockItemId, cancellationToken);

        return item is null
            ? (null, $"Nothing was done: no stock item has the id '{stockItemId}'. Use listStockItems or getStockItem to find it.")
            : (item, null);
    }

    /// <summary>
    /// Gets the location a change is made at: the one given when the store lists it, or the default location when none
    /// is given. An unknown location is refused rather than replaced, unlike the Inventory page's form, which can only
    /// post listed ones.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="locationId">The location given, or <see langword="null"/>.</param>
    /// <returns>The location's id and name, or the sentence that says it is not listed.</returns>
    protected static async Task<(string LocationId, string Name, string Error)> GetListedLocationAsync(IServiceProvider services, string locationId)
    {
        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();
        var id = locationId ?? InventoryConstants.DefaultLocationId;
        var location = locations.FirstOrDefault(entry => string.Equals(entry.LocationId, id, StringComparison.Ordinal));

        return location is null
            ? (null, null, $"Nothing was changed: there is no stock location with the id '{id}'. Use listStockLocations for the ids.")
            : (location.LocationId, location.Name, null);
    }

    /// <summary>
    /// Makes a stock change and commits it at once, as the Inventory page does
    /// (<see cref="StockChanges.CommitAsync"/>). The work of earlier tool calls of the chat message is written first, so
    /// a change refused as busy, which discards everything not yet written, discards only the stock change. After such a
    /// refusal nothing more can be written in the message, and later stock changes say so.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="change">The change, made in the session's unit of work.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The committed change, or the sentence that says why nothing was saved.</returns>
    protected static async Task<(StockChangeResult Result, string Error)> ChangeStockAsync(IServiceProvider services, Func<Task<StockChangeResult>> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } error)
        {
            return (null, error);
        }

        var result = await StockChanges.CommitAsync(services.GetRequiredService<YesSqlSession>(), await change(), cancellationToken);

        if (result.Error == StockChangeError.Busy)
        {
            MarkSessionClosed(services);
        }

        return result.Succeeded
            ? (result, null)
            : (null, result.Error switch
            {
                StockChangeError.NotFound => "Nothing was changed: that stock item no longer exists.",
                StockChangeError.InvalidQuantity => "Nothing was changed: give a quantity of more than zero for stock received, not zero for an adjustment, and zero or more for a count.",
                StockChangeError.NotEnoughAtLocation => "Nothing was changed: the location doesn't have that many available to move. Units held for orders stay where they are.",
                _ => "Nothing was changed: someone else changed this stock at the same moment. Read it again with getStockItem and retry in your next message.",
            });
    }

    /// <summary>
    /// Writes the work of earlier tool calls of the chat message, so a stock change that has to be discarded cannot take
    /// it along, and refuses when an earlier stock change of the message was discarded.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> when the change can go ahead, or the sentence that says why not.</returns>
    protected static async Task<string> WriteEarlierWorkAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var scope = services.GetService<InventoryToolScope>();

        if (scope?.SessionClosed == true)
        {
            return "Nothing was changed: an earlier change in this message was refused, so nothing more can be saved in it. Ask again in your next message.";
        }

        try
        {
            await services.GetRequiredService<YesSqlSession>().SaveChangesAsync(cancellationToken);

            return null;
        }
        catch (ConcurrencyException)
        {
            MarkSessionClosed(services);

            return "Nothing was changed: an earlier change in this message conflicted with someone else's and was not saved. Ask again in your next message.";
        }
    }

    /// <summary>
    /// Records that the session discarded its work, so nothing more is written in this chat message.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    protected static void MarkSessionClosed(IServiceProvider services)
    {
        if (services.GetService<InventoryToolScope>() is { } scope)
        {
            scope.SessionClosed = true;
        }
    }

    /// <summary>
    /// Describes a stock item for a result.
    /// </summary>
    /// <param name="item">The stock item.</param>
    /// <param name="locations">The listed locations, to name each level's location.</param>
    /// <returns>The description.</returns>
    protected static object Describe(StockItem item, IReadOnlyList<InventoryLocation> locations)
    {
        item.NormalizeLevels();

        return new
        {
            stockItemId = item.ItemId,
            contentItemId = item.ContentItemId,
            variantId = item.VariantId,
            sku = item.Sku,
            title = item.Title,
            onHand = item.OnHand,
            held = item.Reserved,
            available = item.Available,
            lowStockThreshold = item.LowStockThreshold,
            isLow = item.IsLow,
            allowBackorder = item.AllowBackorder,
            archived = item.Archived ? true : (bool?)null,
            levels = item.Levels
                .Where(level => level.OnHand != 0 || level.Reserved != 0)
                .Select(level => new
                {
                    locationId = level.LocationId,
                    location = locations?.FirstOrDefault(location => location.LocationId == level.LocationId)?.Name ?? "Removed location",
                    onHand = level.OnHand,
                    held = level.Reserved,
                    available = level.Available,
                })
                .ToArray(),
        };
    }

    /// <summary>
    /// Writes a UTC time in the caller's or the site's time zone.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="utc">The UTC time, or <see langword="null"/>.</param>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    protected static async Task<string> LocalTimeAsync(IServiceProvider services, DateTime? utc)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        var localClock = services.GetService<ILocalClock>();

        if (localClock is null)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes a result as JSON.
    /// </summary>
    /// <param name="value">The result, built from an explicit list of fields.</param>
    /// <returns>The JSON text.</returns>
    protected static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Reads an argument as JSON.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing or null.</returns>
    protected static JsonElement? ReadJson(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value);

        return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element;
    }

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is missing or blank.</returns>
    protected static string ReadString(AIFunctionArguments arguments, string name)
        => ReadJson(arguments, name) is { } element ? ToText(element) : null;

    /// <summary>
    /// Reads a JSON value as text.
    /// </summary>
    /// <param name="element">The value.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is blank.</returns>
    protected static string ToText(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The number, or <see langword="null"/> when it is missing or not a whole number.</returns>
    protected static int? ReadInt(AIFunctionArguments arguments, string name)
        => ReadJson(arguments, name) is { } element ? ToInt(element) : null;

    /// <summary>
    /// Reads a JSON value as a whole number.
    /// </summary>
    /// <param name="element">The value.</param>
    /// <returns>The number, or <see langword="null"/> when it is not a whole number.</returns>
    protected static int? ToInt(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => null,
        };

    /// <summary>
    /// Reads the page and page size arguments, with the page size capped.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The page, from 1, and the page size.</returns>
    protected static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

    private static HashSet<string> GetArgumentNames(JsonElement schema)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                names.Add(property.Name);
            }
        }

        return names;
    }
}

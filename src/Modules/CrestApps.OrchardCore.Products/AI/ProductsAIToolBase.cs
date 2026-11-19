using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// A product, currency or product feed AI tool. It acts as the person using the AI: it refuses a caller who is not
/// signed in, or who lacks the tool's permission, before it reads an argument; a tool that changes a product checks
/// <c>EditContent</c> (and <c>PublishContent</c> to publish) on that product, as the content editor does. Any argument the
/// schema does not declare is refused. Results are built from an explicit list of fields; a content item, a currency or
/// the feed settings are never serialized, and the feed's access key is never returned.
/// </summary>
internal abstract class ProductsAIToolBase : AIFunction
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

    /// <summary>
    /// The description of the <c>publish</c> argument of the tools that change a product.
    /// </summary>
    protected const string PublishDescription = "true to publish the change at once, which needs the permission to publish the product and is refused while the product has other unpublished changes. false or left out to save it in a draft, which the shop shows only once it is published.";

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
    /// Gets the permission the caller needs before any argument is read, or <see langword="null"/> for a tool that
    /// checks a permission on the product it changes.
    /// </summary>
    protected abstract Permission Permission { get; }

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var caller = GetCaller(services);

        if (caller is null || (Permission is not null && !await IsAuthorizedAsync(services, caller, Permission)))
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
    /// Checks a permission for the caller.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="resource">The resource, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the caller has the permission.</returns>
    protected static async Task<bool> IsAuthorizedAsync(IServiceProvider services, ClaimsPrincipal caller, Permission permission, object resource = null)
    {
        var authorizationService = services.GetService<IAuthorizationService>();

        return authorizationService is not null && await authorizationService.AuthorizeAsync(caller, permission, resource);
    }

    /// <summary>
    /// Loads the latest version of a product: a content item whose type has the Product part.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="contentItemId">The product's content item id.</param>
    /// <returns>The product and its type, or the sentence that says why it cannot be used.</returns>
    protected static async Task<(ContentItem Product, ContentTypeDefinition Type, string Error)> LoadProductAsync(IServiceProvider services, string contentItemId)
    {
        var item = contentItemId is null ? null : await services.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Latest);
        var type = item is null ? null : await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(item.ContentType);

        if (item is null || type is null || !type.Parts.Any(part => part.PartDefinition?.Name == nameof(ProductPart)))
        {
            return (null, null, $"Nothing was done: no product has the content item id '{contentItemId}'. Use listProducts to find it.");
        }

        return (item, type, null);
    }

    /// <summary>
    /// Saves a change to a product as the content editor's buttons do. The change is saved in a draft unless publishing
    /// is asked for; publishing needs <c>PublishContent</c> on the product and is refused while the product has a pending
    /// draft, because that would publish someone's unrelated edits. The change is checked by the content handlers before
    /// anything is saved.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="latest">The latest version of the product.</param>
    /// <param name="publish">Whether the caller asked to publish the change.</param>
    /// <param name="alter">The change.</param>
    /// <returns>The saved version and whether it was published, or the sentence that says why nothing was saved.</returns>
    protected static async Task<(ContentItem Item, bool Published, string Error)> SaveProductAsync(
        IServiceProvider services,
        ClaimsPrincipal caller,
        ContentItem latest,
        bool publish,
        Action<ContentItem> alter)
    {
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(alter);

        var contentManager = services.GetRequiredService<IContentManager>();
        var published = latest.Published ? latest : await contentManager.GetAsync(latest.ContentItemId, VersionOptions.Published);
        var hasPendingDraft = published is not null && published.ContentItemVersionId != latest.ContentItemVersionId;

        if (publish && hasPendingDraft)
        {
            return (null, false, "Nothing was changed: the product has unpublished changes, and publishing this change would publish them too. Publish or discard the draft first, or leave publish out to save the change in the draft.");
        }

        if (publish && !await IsAuthorizedAsync(services, caller, CommonPermissions.PublishContent, latest))
        {
            return (null, false, "Nothing was changed: publishing the change needs the permission to publish this product. Leave publish out to save it in a draft that a publisher can publish.");
        }

        var item = await contentManager.GetAsync(latest.ContentItemId, VersionOptions.DraftRequired);

        alter(item);

        await contentManager.UpdateAsync(item);

        var validation = await contentManager.ValidateAsync(item);

        if (!validation.Succeeded)
        {
            return (null, false, $"Nothing was saved: the product is not valid. {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");
        }

        if (!publish)
        {
            await contentManager.SaveDraftAsync(item);

            return (item, false, null);
        }

        if (!await contentManager.PublishAsync(item))
        {
            return (null, false, "The change was saved in a draft but not published: a publishing rule of the site stopped it. Publish the product from its editor.");
        }

        return (item, true, null);
    }

    /// <summary>
    /// Gets a part of a content item.
    /// </summary>
    /// <typeparam name="TPart">The part.</typeparam>
    /// <param name="item">The content item.</param>
    /// <returns>The part, or <see langword="null"/> when the item has none.</returns>
    protected static TPart GetPart<TPart>(ContentItem item)
        where TPart : ContentPart
        => item is not null && item.TryGet<TPart>(out var part) ? part : null;

    /// <summary>
    /// Gets the currency a product is priced in: its own, else its type's default.
    /// </summary>
    /// <param name="product">The product part.</param>
    /// <param name="type">The product's type.</param>
    /// <returns>The three-letter code, or <see langword="null"/> when neither is set.</returns>
    protected static string GetCurrency(ProductPart product, ContentTypeDefinition type)
    {
        if (!string.IsNullOrWhiteSpace(product?.Currency))
        {
            return product.Currency.Trim().ToUpperInvariant();
        }

        var settings = type?.Parts.FirstOrDefault(part => part.PartDefinition?.Name == nameof(ProductPart))?.GetSettings<ProductPartSettings>();

        return string.IsNullOrWhiteSpace(settings?.DefaultCurrency) ? null : settings.DefaultCurrency.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Gets the Product part settings of a product's type.
    /// </summary>
    /// <param name="type">The product's type.</param>
    /// <returns>The settings, or <see langword="null"/>.</returns>
    protected static ProductPartSettings GetProductSettings(ContentTypeDefinition type)
        => type?.Parts.FirstOrDefault(part => part.PartDefinition?.Name == nameof(ProductPart))?.GetSettings<ProductPartSettings>();

    /// <summary>
    /// Checks that an amount is zero or more and has no more decimal places than its currency has
    /// (<see cref="CurrencyScale"/>, the scale <c>Money</c> rounds to; the Products module does not reference the
    /// payments assemblies), so it is stored exactly as given.
    /// </summary>
    /// <param name="name">The argument's name.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency, or <see langword="null"/> for two decimal places.</param>
    /// <returns><see langword="null"/> when the amount can be stored, or the sentence that says why not.</returns>
    protected static string CheckAmount(string name, decimal? amount, string currency)
    {
        if (amount is not { } value)
        {
            return null;
        }

        if (value < 0m)
        {
            return $"Nothing was changed: {name} cannot be below zero.";
        }

        if (CurrencyScale.Round(value, currency) != value)
        {
            var places = CurrencyScale.GetDecimalPlaces(currency);

            return places == 0
                ? $"Nothing was changed: {name} is {value.ToString(CultureInfo.InvariantCulture)}, but {currency} has no smaller unit; give a whole amount."
                : $"Nothing was changed: {name} is {value.ToString(CultureInfo.InvariantCulture)}, but {currency ?? "the currency"} has {places} decimal places; give it as {CurrencyScale.Round(value, currency).ToString(CultureInfo.InvariantCulture)} or another amount with at most {places}.";
        }

        return null;
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
    /// Reads a date and time given in the site's time zone and converts it to UTC, as the product editor does.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The UTC time (or <see langword="null"/> when the argument is missing or null), or the sentence that
    /// says the value cannot be read.</returns>
    protected static async Task<(DateTime? Utc, string Error)> ReadLocalTimeAsync(IServiceProvider services, AIFunctionArguments arguments, string name)
    {
        var text = ReadString(arguments, name);

        if (text is null)
        {
            return (null, null);
        }

        if (!DateTime.TryParseExact(text, ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return (null, $"Nothing was changed: '{text}' is not a date and time. Give {name} as yyyy-MM-ddTHH:mm in the store's time zone, without an offset.");
        }

        var utc = await services.GetRequiredService<ILocalClock>().ConvertToUtcAsync(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));

        return (DateTime.SpecifyKind(utc, DateTimeKind.Utc), null);
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
    /// Gets whether an argument was given, even as null.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns><see langword="true"/> when the argument was given.</returns>
    protected static bool Has(AIFunctionArguments arguments, string name)
        => arguments is not null && arguments.ContainsKey(name);

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
    /// Reads a yes or no argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing.</returns>
    protected static bool? ReadBool(AIFunctionArguments arguments, string name)
        => bool.TryParse(ReadString(arguments, name), out var value) ? value : null;

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The number, or <see langword="null"/> when it is missing or not a whole number.</returns>
    protected static int? ReadInt(AIFunctionArguments arguments, string name)
        => int.TryParse(ReadString(arguments, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Reads a number, given as a number or as text with a decimal point.
    /// </summary>
    /// <param name="element">The value.</param>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when the value is a number.</returns>
    protected static bool TryReadDecimal(JsonElement element, out decimal number)
    {
        number = 0m;

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out number),
            JsonValueKind.String => decimal.TryParse(element.GetString(), NumberStyles.Number & ~NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out number),
            _ => false,
        };
    }

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

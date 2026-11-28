using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Accounts.AI;

/// <summary>
/// An account AI tool. It acts as the person using the AI: it refuses a caller who is not signed in and any argument its
/// schema does not declare. Each tool checks the content permissions the account editor checks, with the account as the
/// resource, and a change is saved the way the editor's buttons save it: published when the account is published and has
/// no pending draft (with <c>PublishContent</c>), and otherwise kept in a draft.
/// </summary>
internal abstract class AccountsAIToolBase : AIFunction
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
    /// The description of the <c>saveAsDraft</c> argument of the tools that change an account.
    /// </summary>
    protected const string SaveAsDraftDescription = "true to save the change in a draft without publishing it. Required when the account has unpublished changes; the published account keeps its current values until the draft is published.";

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

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var caller = GetCaller(services);

        if (caller is null)
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
    /// Runs the tool for a signed-in caller.
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
    /// Loads the latest version of an account and checks that its content type is an account type with a part.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="contentItemId">The account's content item id.</param>
    /// <param name="partName">The part the account type must have, or <see langword="null"/>.</param>
    /// <returns>The account, or the sentence that says why it cannot be used.</returns>
    protected static async Task<(ContentItem Account, string Error)> LoadAccountAsync(IServiceProvider services, string contentItemId, string partName)
    {
        var account = contentItemId is null ? null : await services.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Latest);

        if (account is null || !await services.GetRequiredService<IAccountContentTypeProvider>().IsAccountTypeAsync(account.ContentType))
        {
            return (null, $"Nothing was done: no account has the id '{contentItemId}'. Use listAccounts to find it.");
        }

        if (partName is not null)
        {
            var definition = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(account.ContentType);

            if (definition is null || !definition.Parts.Any(part => string.Equals(part.PartDefinition?.Name, partName, StringComparison.Ordinal)))
            {
                return (null, $"Nothing was done: the account type '{account.ContentType}' has no {partName}. Add the part to the content type first.");
            }
        }

        return (account, null);
    }

    /// <summary>
    /// Saves a change to an account as the account editor's buttons do. A published account with no pending draft is
    /// published again with the change, which needs <c>PublishContent</c>; an account never published, or a change asked
    /// for as a draft, is saved in a draft; a pending draft is never published, because that would publish someone's
    /// unrelated edits.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="latest">The latest version of the account.</param>
    /// <param name="saveAsDraft">Whether the caller asked to save the change in a draft.</param>
    /// <param name="alter">The change.</param>
    /// <returns>The saved version and whether it was published, or the sentence that says why nothing was saved.</returns>
    protected static async Task<(ContentItem Item, bool Published, string Error)> SaveAccountAsync(
        IServiceProvider services,
        ClaimsPrincipal caller,
        ContentItem latest,
        bool saveAsDraft,
        Action<ContentItem> alter)
    {
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(alter);

        var contentManager = services.GetRequiredService<IContentManager>();
        var published = latest.Published ? latest : await contentManager.GetAsync(latest.ContentItemId, VersionOptions.Published);
        var hasPendingDraft = published is not null && published.ContentItemVersionId != latest.ContentItemVersionId;

        saveAsDraft = saveAsDraft || published is null;

        if (hasPendingDraft && !saveAsDraft)
        {
            return (null, false, "Nothing was changed: the account has unpublished changes, and publishing this change would publish them too. Publish or discard the draft first, or pass saveAsDraft: true to save the change in the draft.");
        }

        if (!saveAsDraft && !await IsAuthorizedAsync(services, caller, CommonPermissions.PublishContent, latest))
        {
            return (null, false, "Nothing was changed: publishing the change needs the permission to publish this account. Pass saveAsDraft: true to save it in a draft that a publisher can publish.");
        }

        var item = await contentManager.GetAsync(latest.ContentItemId, VersionOptions.DraftRequired);

        alter(item);

        await contentManager.UpdateAsync(item);

        var validation = await contentManager.ValidateAsync(item);

        if (!validation.Succeeded)
        {
            return (null, false, $"Nothing was saved: the account is not valid. {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");
        }

        if (saveAsDraft)
        {
            await contentManager.SaveDraftAsync(item);

            return (item, false, null);
        }

        if (!await contentManager.PublishAsync(item))
        {
            return (null, false, "The change was saved in a draft but not published: a publishing rule of the site stopped it. Publish the account from its editor.");
        }

        return (item, true, null);
    }

    /// <summary>
    /// Gets a part of an account.
    /// </summary>
    /// <typeparam name="TPart">The part.</typeparam>
    /// <param name="account">The account.</param>
    /// <returns>The part, or <see langword="null"/> when the account has none.</returns>
    protected static TPart GetPart<TPart>(ContentItem account)
        where TPart : ContentPart
        => account is not null && account.TryGet<TPart>(out var part) ? part : null;

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
    /// Gets whether an argument was given, even empty.
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
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element
            ? element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element.ToString(),
            }
            : Convert.ToString(value, CultureInfo.InvariantCulture);

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
    /// Reads a list of texts, given as an array or as one text separated by commas.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed, non-empty texts, or <see langword="null"/> when the argument is missing.</returns>
    protected static List<string> ReadStringList(AIFunctionArguments arguments, string name)
    {
        if (ReadJson(arguments, name) is not { } element)
        {
            return Has(arguments, name) ? [] : null;
        }

        IEnumerable<string> items = element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
            : (element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()).Split(',');

        return items
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrEmpty(item))
            .ToList();
    }

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

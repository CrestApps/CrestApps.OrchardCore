using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Accounts.B2B.AI;

/// <summary>
/// A business buying AI tool. It acts as the person using the AI: it refuses a caller who is not signed in or who lacks
/// <see cref="B2BAccountsPermissions.ManageB2BAccounts"/>, the permission of the Business buying card and the Price lists
/// pages, before it reads an argument, and refuses any argument its schema does not declare. A tool that changes an
/// account also checks the content permissions the account editor checks, with the account as the resource, and saves the
/// change the way the editor's buttons do: published when the account is published and has no pending draft (with
/// <c>PublishContent</c>), and otherwise kept in a draft.
/// </summary>
internal abstract class B2BAccountsAIToolBase : AIFunction
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

        if (caller is null || !await IsAuthorizedAsync(services, caller, B2BAccountsPermissions.ManageB2BAccounts))
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
    /// Runs the tool for a signed-in caller who manages business accounts.
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
    /// Loads the latest version of an account whose content type has the business buying part.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="contentItemId">The account's content item id.</param>
    /// <returns>The account, or the sentence that says why it cannot be used.</returns>
    protected static async Task<(ContentItem Account, string Error)> LoadAccountAsync(IServiceProvider services, string contentItemId)
    {
        var account = contentItemId is null ? null : await services.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Latest);

        if (account is null || !await services.GetRequiredService<IAccountContentTypeProvider>().IsAccountTypeAsync(account.ContentType))
        {
            return (null, $"Nothing was done: no account has the id '{contentItemId}'. Use listAccounts to find it.");
        }

        var definition = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(account.ContentType);

        if (definition is null || !definition.Parts.Any(part => string.Equals(part.PartDefinition?.Name, B2BAccountsConstants.ContentParts.B2BAccount, StringComparison.Ordinal)))
        {
            return (null, $"Nothing was done: the account type '{account.ContentType}' has no {B2BAccountsConstants.ContentParts.B2BAccount}, so its accounts cannot buy as a business. Add the part to the content type first.");
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
    /// Gets an account's members.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The members part, empty when the account has none.</returns>
    protected static AccountMembersPart GetMembers(ContentItem account)
        => account is not null && account.TryGet<AccountMembersPart>(out var part) ? part : new AccountMembersPart();

    /// <summary>
    /// Finds one of an account's members by user id, user name or email, ignoring case.
    /// </summary>
    /// <param name="members">The account's members.</param>
    /// <param name="user">The user id, user name or email.</param>
    /// <returns>The member, or <see langword="null"/> when the user is not a member of the account.</returns>
    protected static AccountMember FindMember(AccountMembersPart members, string user)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return null;
        }

        user = user.Trim();

        return (members?.Members ?? []).FirstOrDefault(member => member is not null && !string.IsNullOrEmpty(member.UserId) &&
            (string.Equals(member.UserId, user, StringComparison.Ordinal) ||
            string.Equals(member.UserName, user, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(member.Email, user, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Gets the path of an account's editor, where its Business buying card is.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="contentItemId">The account's content item id.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string GetAccountEditorPath(IServiceProvider services, string contentItemId)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        try
        {
            return linkGenerator.GetPathByAction(httpContext, "Edit", "Admin", new { area = OrchardCoreFeatureIds.Contents, contentItemId });
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a UTC time in the caller's or the site's time zone.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="utc">The UTC time, or <see langword="null"/>.</param>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    protected static async Task<string> LocalTimeAsync(IServiceProvider services, DateTime? utc)
    {
        if (utc is null)
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
        => ReadJson(arguments, name) is { } element ? Text(element) : null;

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
    /// Reads a number from a JSON value given as a number or as text with a decimal point.
    /// </summary>
    /// <param name="element">The value.</param>
    /// <param name="value">The number.</param>
    /// <returns><see langword="true"/> when the value is a number.</returns>
    protected static bool TryReadDecimal(JsonElement element, out decimal value)
    {
        value = 0m;

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(element.GetString(), NumberStyles.Number & ~NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value),
            _ => false,
        };
    }

    /// <summary>
    /// Reads a JSON value as trimmed text.
    /// </summary>
    /// <param name="element">The value.</param>
    /// <returns>The text, or <see langword="null"/> when it is null or blank.</returns>
    protected static string Text(JsonElement element)
    {
        var text = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.ToString(),
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
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

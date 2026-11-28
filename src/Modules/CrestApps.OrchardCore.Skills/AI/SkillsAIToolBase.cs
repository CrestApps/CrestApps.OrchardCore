using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// A Skills AI tool. It acts as the person using the AI: it refuses a caller who is not signed in or who lacks the tool's
/// permission (the one the matching Skills page checks) before it reads an argument, and refuses any argument its schema
/// does not declare. A person who holds skills is a subject: a user, or another kind such as a staff member, described by
/// the <see cref="ISkillSubjectProvider"/> of the module that owns it.
/// </summary>
internal abstract class SkillsAIToolBase : AIFunction
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
    /// The schema of the two arguments that name a subject, for tools that take one.
    /// </summary>
    protected const string SubjectProperties =
    """
        "subjectKind": {
          "type": "string",
          "enum": ["User", "Staff"],
          "description": "Who holds the skills or certificates: a User (a user account) or a Staff member (from getStaffMember)."
        },
        "subjectId": {
          "type": "string",
          "description": "For a User, the user id, user name or email. For a Staff member, the staff member id from getStaffMember."
        }
    """;

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
    /// Gets the permission the caller needs, the one the matching Skills page checks.
    /// </summary>
    protected abstract Permission RequiredPermission { get; }

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;
        var authorizationService = services.GetService<IAuthorizationService>();

        if (user?.Identity?.IsAuthenticated != true ||
            authorizationService is null ||
            !await authorizationService.AuthorizeAsync(user, RequiredPermission))
        {
            return NotAllowedMessage;
        }

        _argumentNames ??= GetArgumentNames(JsonSchema);

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was done: '{unknown}' is not an argument of {Name}. Use only the arguments the tool declares.";
        }

        return await InvokeAsync(arguments, services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller who holds the permission.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the subject the arguments name and checks it exists. When the tool changes the subject's skills or
    /// certificates, it also asks the subject's provider whether the caller may, such as a manager's staff scope.
    /// </summary>
    /// <param name="arguments">The arguments, with <c>subjectKind</c> and <c>subjectId</c>.</param>
    /// <param name="services">The services.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="forChange">Whether the tool changes the subject's skills or certificates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The subject and its name, or the sentence that explains why it was not found or not allowed.</returns>
    protected static async Task<SubjectLookup> FindSubjectAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, bool forChange, CancellationToken cancellationToken)
    {
        var kindText = ReadString(arguments, "subjectKind");
        var id = ReadString(arguments, "subjectId");

        if (kindText is null || id is null)
        {
            return SubjectLookup.Fail("Give subjectKind (User or Staff) and subjectId.");
        }

        var kind = SkillSubjectKind.Parse(kindText);

        if (kind != SkillSubjectKind.User && kind != SkillSubjectKind.Staff)
        {
            return SubjectLookup.Fail("subjectKind must be User or Staff.");
        }

        var provider = services.GetServices<ISkillSubjectProvider>().FirstOrDefault(item => item.Kind == kind);

        if (provider is null)
        {
            return SubjectLookup.Fail(kind == SkillSubjectKind.Staff
                ? "Staff members can't be used here because the Staff feature is not enabled."
                : "Users can't be used here because the user directory is not available.");
        }

        string name;

        if (kind == SkillSubjectKind.User)
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = await userManager.FindByIdAsync(id)
                ?? await userManager.FindByNameAsync(id)
                ?? (id.Contains('@') ? await userManager.FindByEmailAsync(id) : null);

            if (user is null)
            {
                return SubjectLookup.Fail($"There is no user '{id}'. Give a user id, user name or email.");
            }

            id = await userManager.GetUserIdAsync(user);
            name = services.GetService<IDisplayNameProvider>() is { } displayNames
                ? await displayNames.GetAsync(user, cancellationToken)
                : user.UserName;
        }
        else
        {
            var names = await provider.GetNamesAsync([id], cancellationToken);

            if (!names.TryGetValue(id, out name))
            {
                return SubjectLookup.Fail($"There is no staff member with the id '{id}'. Use getStaffMember to find their id.");
            }
        }

        if (forChange && !await provider.CanManageAsync(caller, id, cancellationToken))
        {
            return SubjectLookup.Fail($"You are not allowed to change the skills or certificates of {name}: they are not among the people you manage.");
        }

        return new SubjectLookup(new SkillSubject(kind, id), string.IsNullOrWhiteSpace(name) ? id : name, null);
    }

    /// <summary>
    /// Gets whether the caller may change a stored certificate's holder's certificates, as for a new one.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="record">The certificate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    protected static async Task<bool> CanChangeAsync(IServiceProvider services, ClaimsPrincipal caller, CertificationRecord record, CancellationToken cancellationToken)
    {
        var provider = services.GetServices<ISkillSubjectProvider>().FirstOrDefault(item => item.Kind == record.SubjectKind);

        return provider is not null && await provider.CanManageAsync(caller, record.SubjectId, cancellationToken);
    }

    /// <summary>
    /// Gets the names of subjects of any kind, by <see cref="SkillSubject.Key"/>.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="subjects">The subjects.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The names found.</returns>
    protected static async Task<IReadOnlyDictionary<string, string>> GetSubjectNamesAsync(IServiceProvider services, IEnumerable<SkillSubject> subjects, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var providers = services.GetServices<ISkillSubjectProvider>().ToArray();

        foreach (var group in subjects.Where(subject => !subject.IsEmpty).Distinct().GroupBy(subject => subject.Kind, StringComparer.Ordinal))
        {
            if (providers.FirstOrDefault(provider => provider.Kind == group.Key) is not { } provider)
            {
                continue;
            }

            foreach (var pair in await provider.GetNamesAsync(group.Select(subject => subject.Id), cancellationToken))
            {
                names[new SkillSubject(group.Key, pair.Key).Key] = pair.Value;
            }
        }

        return names;
    }

    /// <summary>
    /// Gets today's date in the site's or the caller's time zone.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>Today.</returns>
    protected static async Task<DateOnly> GetTodayAsync(IServiceProvider services)
    {
        var utcNow = services.GetRequiredService<IClock>().UtcNow;
        var local = await services.GetRequiredService<ILocalClock>().ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)));

        return DateOnly.FromDateTime(local.DateTime);
    }

    /// <summary>
    /// Writes a UTC time in the site's or the caller's time zone.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="utc">The UTC time.</param>
    /// <returns>The local time as text, or <see langword="null"/>.</returns>
    internal static async Task<string> LocalAsync(IServiceProvider services, DateTime? utc)
    {
        if (utc is not { } value)
        {
            return null;
        }

        var offset = new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));

        if (services.GetService<ILocalClock>() is { } localClock)
        {
            offset = await localClock.ConvertToLocalAsync(offset);
        }

        return offset.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads the page and page size arguments.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The page (from 1) and the page size.</returns>
    protected static (int Page, int PageSize) ReadPage(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

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
    /// Reads a date argument written as yyyy-MM-dd.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="date">The date, or <see langword="null"/> when the argument is missing or empty.</param>
    /// <returns><see langword="false"/> when the argument is not a date.</returns>
    protected static bool TryReadDate(AIFunctionArguments arguments, string name, out DateOnly? date)
    {
        date = null;

        if (ReadString(arguments, name) is not { } text)
        {
            return true;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            date = value;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads an argument that holds a list of whole numbers.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="values">The numbers.</param>
    /// <returns><see langword="false"/> when an entry is not a whole number.</returns>
    protected static bool TryReadIntList(AIFunctionArguments arguments, string name, out List<int> values)
    {
        values = [];

        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return true;
        }

        IEnumerable<string> items = value switch
        {
            JsonElement { ValueKind: JsonValueKind.Array } array => array.EnumerateArray().Select(item => item.ToString()),
            JsonElement { ValueKind: JsonValueKind.Null } => [],
            JsonElement element => element.ToString().Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries),
            IEnumerable<int> numbers => numbers.Select(number => number.ToString(CultureInfo.InvariantCulture)),
            string text => text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries),
            System.Collections.IEnumerable list => list.Cast<object>().Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)),
            _ => [Convert.ToString(value, CultureInfo.InvariantCulture)],
        };

        foreach (var item in items)
        {
            if (!int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            values.Add(number);
        }

        return true;
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

    /// <summary>
    /// The subject an argument names, or why it could not be used.
    /// </summary>
    /// <param name="Subject">The subject.</param>
    /// <param name="Name">The subject's name.</param>
    /// <param name="Error">The sentence that explains the failure, or <see langword="null"/>.</param>
    protected sealed record SubjectLookup(SkillSubject Subject, string Name, string Error)
    {
        /// <summary>
        /// Builds a failed lookup.
        /// </summary>
        /// <param name="error">The sentence.</param>
        /// <returns>The lookup.</returns>
        public static SubjectLookup Fail(string error)
            => new(default, null, error);
    }
}

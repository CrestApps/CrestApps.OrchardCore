using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Timeline.AI;

/// <summary>
/// A Timeline AI tool. It works only for a signed-in person and acts as that person: who may read or write a record's
/// timeline is decided by the record kind's <see cref="ITimelineAccessProvider"/>, through
/// <see cref="ITimelineAccessService"/>, as on the timeline card.
/// </summary>
internal abstract class TimelineAIToolBase : AIFunction
{
    /// <summary>
    /// What a tool says when nobody is signed in.
    /// </summary>
    public const string SignInRequired = "Only a signed-in person can read or write a record's timeline.";

    /// <summary>
    /// The number of results a page has when the model asks for none.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The most results a page has.
    /// </summary>
    public const int MaxPageSize = 50;

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        // The chat hubs publish the caller through IUserAccessor for the whole invocation.
        var user = arguments.Services?.GetService<IUserAccessor>()?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return SignInRequired;
        }

        return await InvokeAsync(arguments, arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in person.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the invocation.</param>
    /// <param name="user">The person using the AI.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the record a call names: its kind, which must have a timeline, and its id, or a number or name looked up
    /// among the records of that kind the user may see.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the invocation.</param>
    /// <param name="user">The person using the AI.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The record, or what to tell the model instead.</returns>
    protected static async Task<RecordLookup> FindRecordAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var kinds = services.GetServices<ITimelineAccessProvider>()
            .Select(provider => provider.SubjectKind)
            .Where(kind => !string.IsNullOrEmpty(kind))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var requested = Read(arguments, "recordKind");

        if (requested is null)
        {
            return RecordLookup.Refused($"Give recordKind, one of: {string.Join(", ", kinds)}.");
        }

        var kind = kinds.FirstOrDefault(candidate => string.Equals(candidate, requested, StringComparison.OrdinalIgnoreCase));

        if (kind is null)
        {
            return RecordLookup.Refused($"Records of the kind '{requested}' have no timeline. The kinds with a timeline are: {string.Join(", ", kinds)}. Accounts, contacts and leads are the kind {TimelineConstants.ContentItemKind}.");
        }

        if (Read(arguments, "recordId") is { } id)
        {
            return RecordLookup.Found(new RecordRef(kind, id));
        }

        if (Read(arguments, "lookup") is not { } text)
        {
            return RecordLookup.Refused("Give recordId, or lookup with the record's number or name.");
        }

        if (services.GetService<IRecordDirectory>() is not { } directory)
        {
            return RecordLookup.Refused("Records can't be looked up by number or name here. Give recordId.");
        }

        var matches = await directory.SearchAsync(kind, new RecordSearch { Text = text, Count = 5, User = user }, cancellationToken);

        if (matches.Count == 0)
        {
            return RecordLookup.Refused($"No {kind} record the user may see matches '{text}'.");
        }

        var exact = matches.Where(match => string.Equals(match.Title?.Trim(), text, StringComparison.OrdinalIgnoreCase)).ToList();
        var chosen = matches.Count == 1 ? matches[0] : exact.Count == 1 ? exact[0] : null;

        if (chosen is not null)
        {
            return RecordLookup.Found(new RecordRef(kind, chosen.Id));
        }

        return RecordLookup.Ambiguous(Json(new
        {
            message = $"Several {kind} records match '{text}'. Ask which one is meant, then call again with its recordId.",
            candidates = matches.Select(match => new { id = match.Id, title = match.Title, description = match.Description }).ToList(),
        }));
    }

    /// <summary>
    /// Describes a record by its kind, id, title and admin page, as the record's module describes it.
    /// </summary>
    /// <param name="services">The services of the invocation.</param>
    /// <param name="record">The record.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The description.</returns>
    protected static async Task<RecordDescription> DescribeRecordAsync(IServiceProvider services, RecordRef record, CancellationToken cancellationToken)
    {
        var summary = services.GetService<IRecordDirectory>() is { } directory
            ? await directory.GetSummaryAsync(record, cancellationToken)
            : null;

        return new RecordDescription(record.Kind, record.Id, summary?.Title, summary?.AdminUrl);
    }

    /// <summary>
    /// Formats a stored UTC time in the user's or the site's time zone, ISO 8601 with its offset.
    /// </summary>
    /// <param name="services">The services of the invocation.</param>
    /// <param name="utc">The UTC time.</param>
    /// <returns>The local time, or <see langword="null"/>.</returns>
    protected static async Task<string> LocalAsync(IServiceProvider services, DateTime? utc)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        var local = await services.GetRequiredService<ILocalClock>().ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc)));

        return local.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads a text argument, trimmed; missing or empty text is <see langword="null"/>.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The text.</returns>
    protected static string Read(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement element => element.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads a whole number, or <see langword="null"/> when it is missing or not a whole number.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The number.</returns>
    protected static int? ReadInt(AIFunctionArguments arguments, string name)
        => int.TryParse(Read(arguments, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>
    /// Reads a list of texts, given as an array or a comma separated text; empty when it is missing.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The texts.</returns>
    protected static List<string> ReadList(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return [];
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } array)
        {
            return [.. array.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.Ordinal)];
        }

        if (value is IEnumerable<string> texts)
        {
            return [.. texts.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.Ordinal)];
        }

        return [.. (Read(arguments, name) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Writes a result as JSON.
    /// </summary>
    /// <param name="value">The result.</param>
    /// <returns>The JSON text.</returns>
    protected static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);
}

/// <summary>
/// The record a call names, or what to tell the model instead.
/// </summary>
/// <param name="Record">The record, when it was found.</param>
/// <param name="Answer">What to tell the model when it was not: a refusal or the candidates to choose from.</param>
internal sealed record RecordLookup(RecordRef Record, string Answer)
{
    /// <summary>
    /// Gets whether the record was found.
    /// </summary>
    public bool IsFound => Answer is null;

    /// <summary>
    /// The record was found.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The lookup.</returns>
    public static RecordLookup Found(RecordRef record)
        => new(record, null);

    /// <summary>
    /// The record was not found, for a reason the model can relay.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The lookup.</returns>
    public static RecordLookup Refused(string reason)
        => new(default, reason);

    /// <summary>
    /// Several records match; the model asks which one.
    /// </summary>
    /// <param name="candidates">The candidates, as JSON.</param>
    /// <returns>The lookup.</returns>
    public static RecordLookup Ambiguous(string candidates)
        => new(default, candidates);
}

/// <summary>
/// A record as a timeline tool returns it.
/// </summary>
/// <param name="Kind">The record kind.</param>
/// <param name="Id">The record's id.</param>
/// <param name="Title">Its title, such as an order number or an account's name, when its module describes it.</param>
/// <param name="AdminUrl">Its admin page, when it has one.</param>
internal sealed record RecordDescription(string Kind, string Id, string Title, string AdminUrl);

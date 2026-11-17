using System.Globalization;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.AuditTrail;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.AuditTrail.ViewModels;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.AuditTrail;

/// <summary>
/// Lists audit trail events with the filter parser and the query service of the Audit Trail admin page: the
/// arguments become the filter text a person types on that page, so the tool lists what the page lists.
/// </summary>
public sealed class ListAuditTrailEventsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAuditTrailEvents";

    // The format of a time the date filter of the admin page reads without changing it.
    private const string FilterTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private static readonly string[] _dateFormats = ["yyyy-MM-dd"];
    private static readonly string[] _dateTimeFormats = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "category": {
          "type": "string",
          "description": "The technical name of a category of events, for example Content or User. An unknown name is refused with the names that exist."
        },
        "eventName": {
          "type": "string",
          "description": "The technical name of an event, for example Published, LoggedIn or LogInFailed. An unknown name is refused with the names that exist."
        },
        "userName": {
          "type": "string",
          "description": "Text the user name of the user who caused the event must contain."
        },
        "userId": {
          "type": "string",
          "description": "The id of the user who caused the event, from listUsers."
        },
        "correlationId": {
          "type": "string",
          "description": "The id that ties events to one record: the content item id for content events, the user id of the user an event is about for user events."
        },
        "from": {
          "type": "string",
          "description": "List the events from this local date (yyyy-MM-dd) or local date and time (yyyy-MM-ddTHH:mm) on, without a time zone."
        },
        "to": {
          "type": "string",
          "description": "List the events up to this local date (yyyy-MM-dd, the whole day is included) or local date and time (yyyy-MM-ddTHH:mm), without a time zone."
        },
        "sort": {
          "type": "string",
          "enum": [
            "newest",
            "oldest"
          ],
          "description": "The order of the events. The default is newest first."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of events on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists audit trail events, newest first, with optional filters for category, event name, user, date range and correlation id, and paging. Use it to answer \"who did what and when\"; use getAuditTrailEvent for the details of one event.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AuditTrailPermissions.ViewAuditTrail))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var manager = services.GetRequiredService<IAuditTrailManager>();
        var localClock = services.GetRequiredService<ILocalClock>();
        var categories = manager.DescribeCategories().ToList();
        var terms = new List<string>();

        if (arguments.TryGetFirstString("category", out var category))
        {
            var known = categories.FirstOrDefault(candidate => string.Equals(candidate.Name, category.Trim(), StringComparison.OrdinalIgnoreCase));

            if (known is null)
            {
                return $"Nothing was listed: there is no category named '{category}'. The categories are: {string.Join(", ", categories.Select(candidate => candidate.Name))}.";
            }

            categories = [known];
            terms.Add($"category:{known.Name}");
        }

        if (arguments.TryGetFirstString("eventName", out var eventName))
        {
            var known = categories
                .SelectMany(candidate => candidate.Events.Keys)
                .FirstOrDefault(candidate => string.Equals(candidate, eventName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (known is null)
            {
                return $"Nothing was listed: there is no event named '{eventName}'. The events are: {string.Join("; ", categories.Select(DescribeCategory))}.";
            }

            terms.Add($"event:{known}");
        }

        if (arguments.TryGetFirstString("correlationId", out var correlationId))
        {
            if (!IsId(correlationId))
            {
                return "Nothing was listed: correlationId must be an id, without spaces or quotes.";
            }

            terms.Add($"id:{correlationId.Trim()}");
        }

        if (arguments.TryGetFirstString("userId", out var userId))
        {
            if (!IsId(userId))
            {
                return "Nothing was listed: userId must be an id, without spaces or quotes.";
            }

            terms.Add($"userid:{userId.Trim()}");
        }

        var (dateTerm, dateError) = await BuildDateTermAsync(arguments, localClock);

        if (dateError is not null)
        {
            return dateError;
        }

        if (dateTerm is not null)
        {
            terms.Add(dateTerm);
        }

        if (arguments.TryGetFirstString("sort", out var sort))
        {
            switch (sort.Trim().ToLowerInvariant())
            {
                case "newest":
                    break;
                case "oldest":
                    terms.Add("sort:time-asc");
                    break;
                default:
                    return "Nothing was listed: sort must be newest or oldest.";
            }
        }

        // The user name is the text without a filter name. It goes last and in quotes, after the named filters.
        if (arguments.TryGetFirstString("userName", out var userName) && !string.IsNullOrWhiteSpace(userName))
        {
            if (userName.Contains('"'))
            {
                return "Nothing was listed: userName cannot contain a double quote.";
            }

            terms.Add($"\"{userName.Trim()}\"");
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var filter = string.Join(' ', terms);

        var options = new AuditTrailIndexOptions
        {
            FilterResult = services.GetRequiredService<IAuditTrailAdminListFilterParser>().Parse(filter),
        };

        var result = await services.GetRequiredService<IAuditTrailAdminListQueryService>().QueryAsync(page, pageSize, options);
        var items = new List<AuditTrailEventSummary>();

        foreach (var auditTrailEvent in result.Events)
        {
            items.Add(await AuditTrailToolSupport.SummarizeAsync(services, auditTrailEvent));
        }

        return AgentToolResults.Json(new
        {
            items,
            total = result.TotalCount,
            page,
            pageSize,
            truncated = result.TotalCount > page * pageSize
                ? true
                : (bool?)null,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            note = "The times are in the time zone named here. Use getAuditTrailEvent with an eventId for the details of an event.",
        });
    }

    private static async Task<(string Term, string Error)> BuildDateTermAsync(AIFunctionArguments arguments, ILocalClock localClock)
    {
        DateTime? fromUtc = null;
        DateTime? toUtc = null;

        if (arguments.TryGetFirstString("from", out var from))
        {
            if (!TryParseLocal(from, out var local, out _))
            {
                return (null, "Nothing was listed: from must be a local date as yyyy-MM-dd, or a local date and time as yyyy-MM-ddTHH:mm, without a time zone.");
            }

            fromUtc = await localClock.ConvertToUtcAsync(local);
        }

        if (arguments.TryGetFirstString("to", out var to))
        {
            if (!TryParseLocal(to, out var local, out var isDate))
            {
                return (null, "Nothing was listed: to must be a local date as yyyy-MM-dd, or a local date and time as yyyy-MM-ddTHH:mm, without a time zone.");
            }

            // A date without a time means the whole day.
            toUtc = await localClock.ConvertToUtcAsync(isDate
                ? local.AddDays(1)
                : local);
        }

        if (fromUtc is not null && toUtc is not null)
        {
            if (fromUtc > toUtc)
            {
                return (null, "Nothing was listed: from is after to.");
            }

            return ($"date:{Format(fromUtc.Value)}..{Format(toUtc.Value)}", null);
        }

        if (fromUtc is not null)
        {
            return ($"date:>={Format(fromUtc.Value)}", null);
        }

        if (toUtc is not null)
        {
            return ($"date:<={Format(toUtc.Value)}", null);
        }

        return (null, null);
    }

    private static bool TryParseLocal(string text, out DateTime local, out bool isDate)
    {
        isDate = DateTime.TryParseExact(text.Trim(), _dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out local);

        return isDate || DateTime.TryParseExact(text.Trim(), _dateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out local);
    }

    private static string Format(DateTime utc)
        => utc.ToString(FilterTimeFormat, CultureInfo.InvariantCulture);

    private static string DescribeCategory(AuditTrailCategoryDescriptor category)
        => $"{category.Name}: {string.Join(", ", category.Events.Keys)}";

    // The IDs Orchard Core generates are letters and digits. A few separators are let through for IDs that come
    // from elsewhere; a space, a quote or a colon would change the filter.
    private static bool IsId(string value)
    {
        var trimmed = value.Trim();

        return trimmed.Length > 0 && trimmed.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or '@' or '|');
    }
}

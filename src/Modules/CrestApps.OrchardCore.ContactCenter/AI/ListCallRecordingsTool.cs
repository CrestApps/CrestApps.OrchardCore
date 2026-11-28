using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Searches the recorded calls the caller may hear, as the Call recordings page does: someone who may only hear their
/// own calls is only ever searched their own, whatever the arguments say.
/// </summary>
internal sealed class ListCallRecordingsTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listCallRecordings";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "from": {
          "type": "string",
          "description": "Only calls that started at or after this date or date and time, in the user's time zone, for example 2026-11-17 or 2026-11-17T09:00."
        },
        "to": {
          "type": "string",
          "description": "Only calls that started up to this date (the whole day) or date and time (the whole minute), in the user's time zone."
        },
        "customerNumber": {
          "type": "string",
          "description": "Only calls with this customer number, or a number containing these digits."
        },
        "direction": {
          "type": "string",
          "enum": ["Inbound", "Outbound"],
          "description": "Only inbound or only outbound calls."
        },
        "source": {
          "type": "string",
          "enum": ["ContactCenter", "AiAgent", "SoftPhone"],
          "description": "Only calls of this kind: a Contact Center call, an AI voice agent call or a soft phone keypad call."
        },
        "agentUserId": {
          "type": "string",
          "description": "Only calls this agent took, by user id or user name. Ignored for a caller who may only hear their own calls."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "Recordings per page, 20 when not given, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists call recordings filtered by date range, customer number, direction, source and agent, with duration and agent, newest first. Callers without the all-recordings permission only get their own calls. Customer numbers are shown by their last four digits; getCallRecording returns one recording in full.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    // The caller needs one of the two recording permissions; the access evaluator decides which.
    /// <inheritdoc/>
    protected override Permission Permission => null;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var access = await services.GetRequiredService<CallRecordingAccessEvaluator>().GetAccessAsync(user);

        if (!access.CanListOwn || string.IsNullOrEmpty(access.UserId))
        {
            return NotAllowedMessage;
        }

        var reader = new ContactCenterToolArguments(arguments);
        var (page, pageSize) = ReadPaging(reader);

        if (!reader.TryReadChoice<InteractionDirection>("direction", null, out var direction) ||
            !reader.TryReadChoice<CallRecordingSource>("source", null, out var source))
        {
            return string.Join(" ", reader.Errors);
        }

        var localClock = services.GetRequiredService<ILocalClock>();
        var (fromUtc, fromError) = await ReadBoundAsync(localClock, reader.GetString("from"), "from", upper: false);
        var (toUtc, toError) = await ReadBoundAsync(localClock, reader.GetString("to"), "to", upper: true);

        if (fromError is not null || toError is not null)
        {
            return fromError ?? toError;
        }

        var agentUserId = access.CanListEveryone
            ? await ResolveAgentUserIdAsync(services, reader.GetString("agentUserId"), cancellationToken)
            : null;

        var recordings = await services.GetRequiredService<ICallRecordingStore>().QueryAsync(new CallRecordingQuery
        {
            AgentUserId = access.NarrowAgentUserId(agentUserId),
            CustomerAddress = NormalizeNumber(reader.GetString("customerNumber")),
            Direction = direction,
            Source = source,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Page = page,
            PageSize = pageSize,
        }, cancellationToken);

        var names = await GetAgentNamesAsync(services, recordings.Entries.Select(recording => recording.AgentUserId), cancellationToken);
        var items = new List<object>();

        foreach (var recording in recordings.Entries)
        {
            items.Add(new
            {
                id = recording.ItemId,
                started = await ToLocalAsync(localClock, recording.StartedUtc),
                durationSeconds = (int)Math.Round(recording.DurationSeconds),
                direction = recording.Direction.ToString(),
                source = recording.Source.ToString(),
                agentUserId = recording.AgentUserId,
                agentName = CallRecordingAgentNameResolver.NameOf(recording.AgentUserId, names),
                customerNumberEnding = ContactCenterOperationsSupport.Ending(recording.CustomerAddress),
                activityId = recording.ActivityItemId,
            });
        }

        return Json(new
        {
            items,
            total = recordings.Count,
            page,
            pageSize,
            truncated = recordings.Count > page * pageSize,
            onlyOwnCalls = !access.CanListEveryone,
        });
    }

    /// <summary>
    /// Reads a date or date and time the model gave in the user's time zone as a UTC bound: an upper bound is inclusive
    /// to the minute, or to the day for a date, and is returned as the exclusive end.
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    /// <param name="text">The text, or <see langword="null"/> for no bound.</param>
    /// <param name="name">The argument name, for the error.</param>
    /// <param name="upper">Whether it is the upper bound.</param>
    /// <returns>The bound, or the error to return.</returns>
    internal static async Task<(DateTime? Utc, string Error)> ReadBoundAsync(ILocalClock localClock, string text, string name, bool upper)
    {
        if (text is null)
        {
            return (null, null);
        }

        if (!ContactCenterOperationsSupport.TryParseLocal(text, out var local, out var dateOnly))
        {
            return (null, $"{name} must be a date (2026-11-17) or a date and time (2026-11-17T09:00) in the user's time zone.");
        }

        // The upper bound is inclusive to the minute, or to the day for a date; the search's upper bound is exclusive.
        if (upper)
        {
            local = dateOnly ? local.AddDays(1) : local.AddMinutes(1);
        }

        var utc = await localClock.ConvertToUtcAsync(local);

        return (DateTime.SpecifyKind(utc, DateTimeKind.Utc), null);
    }

    // The agent the page's agent filter names by user id; a user name is matched against the Contact Center agents.
    private static async Task<string> ResolveAgentUserIdAsync(IServiceProvider services, string agent, CancellationToken cancellationToken)
    {
        if (agent is null)
        {
            return null;
        }

        var agents = services.GetService<IAgentProfileManager>();

        if (agents is null)
        {
            return agent;
        }

        var match = (await agents.GetAllAsync(cancellationToken))
            .FirstOrDefault(profile => profile is not null && !string.IsNullOrEmpty(profile.UserId) &&
                (string.Equals(profile.UserId, agent, StringComparison.Ordinal) ||
                    string.Equals(profile.UserName, agent, StringComparison.OrdinalIgnoreCase)));

        return match?.UserId ?? agent;
    }

    // The numbers are stored as dialed, usually E.164, and searched by containment: spaces, dashes and brackets the
    // model writes are dropped so "+1 555 010 4455" finds "+15550104455".
    private static string NormalizeNumber(string number)
    {
        if (number is null)
        {
            return null;
        }

        var kept = new string(number.Where((character, index) => char.IsAsciiDigit(character) || (character == '+' && index == 0)).ToArray());

        return kept.Length == 0 ? number : kept;
    }

    internal static async Task<Dictionary<string, string>> GetAgentNamesAsync(IServiceProvider services, IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var resolver = services.GetService<CallRecordingAgentNameResolver>();

        return resolver is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await resolver.GetNamesAsync(userIds, cancellationToken);
    }
}

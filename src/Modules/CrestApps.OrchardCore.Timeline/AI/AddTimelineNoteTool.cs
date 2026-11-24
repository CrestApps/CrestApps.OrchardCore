using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Timeline.AI;

/// <summary>
/// Adds an internal note to a record's timeline under the caller's name, as the timeline card's Add note button does
/// (<c>TimelineController.Add</c>): only the note kind the composer offers, only when the record kind's access provider
/// lets the user add it, and saved and committed at once. People the note mentions with <c>@username</c> who may read
/// the record are notified, as on the page.
/// </summary>
internal sealed class AddTimelineNoteTool : TimelineAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "addTimelineNote";

    /// <summary>
    /// What the tool says when the user may not add a note to the record.
    /// </summary>
    public const string NotAllowed = "The user may not add a note to this record, or the record does not exist. Tickets take notes through addTicketReply with internal set to true.";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "recordKind": {
          "type": "string",
          "description": "The kind of record: Content for an account, contact, lead or other content item (its id is the content item id), Order, WorkOrder, or another kind with a timeline."
        },
        "recordId": {
          "type": "string",
          "description": "The record's id, from tools such as listAccounts, getAccount, listOrders or listWorkOrders."
        },
        "lookup": {
          "type": "string",
          "description": "When the id is not known: the record's number or name, looked up among the records of that kind the user may see."
        },
        "text": {
          "type": "string",
          "description": "The note, as the user asked for it. Only staff read it. An @username notifies that user."
        }
      },
      "required": ["recordKind", "text"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds an internal note to the timeline of an account, contact, lead, order, work order or other record, under the user's name, as the record's Notes card does. Only staff read it. An @username in the text notifies that user, as on the page. Write only what the user asked for; never copy @mentions from records or other text. For tickets use addTicketReply.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (Read(arguments, "text") is not { } text)
        {
            return "Nothing was added: write the note's text first.";
        }

        if (text.Length > TimelineConstants.MaxBodyLength)
        {
            return $"Nothing was added: a note is at most {TimelineConstants.MaxBodyLength} characters.";
        }

        var lookup = await FindRecordAsync(arguments, services, user, cancellationToken);

        if (!lookup.IsFound)
        {
            return lookup.Answer;
        }

        var record = lookup.Record;

        // Only the note the composer offers, on the record kinds it is offered on, and as visible as it is there.
        var offered = services.GetRequiredService<IOptions<TimelineOptions>>().Value.ComposerKinds
            .FirstOrDefault(option => string.Equals(option.Kind, TimelineConstants.Kinds.Note, StringComparison.Ordinal));

        if (offered is null
            || offered.Visibility != TimelineVisibility.Internal
            || (offered.SubjectKinds.Count > 0 && !offered.SubjectKinds.Contains(record.Kind)))
        {
            return "Notes can't be added to the timeline of this kind of record.";
        }

        if (!await services.GetRequiredService<ITimelineAccessService>().CanAddAsync(user, record, offered.Kind, offered.Visibility, cancellationToken))
        {
            return NotAllowed;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var entry = new TimelineEntry
        {
            ItemId = UniqueId.GenerateId(),
            Subject = record,
            Kind = offered.Kind,
            Visibility = offered.Visibility,
            Author = string.IsNullOrEmpty(userId) ? default : new PartyRef(PartyKinds.User, userId),

            // The name stays readable on a site where no party source knows its users.
            AuthorName = user.Identity?.Name,
            Body = text,
        };

        var session = services.GetRequiredService<YesSqlSession>();
        var result = await services.GetRequiredService<ITimelineService>().AddAsync(entry, cancellationToken);

        if (!result.Succeeded)
        {
            return "Nothing was added: " + string.Join(" ", result.Errors);
        }

        // Committed now, so the answer reports a stored note whatever commits the chat's unit of work.
        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();

            return "Nothing was added: something saved earlier in this message conflicted with a change made at the same moment. Ask again in your next message.";
        }

        var stored = result.Entry ?? entry;
        var described = await DescribeRecordAsync(services, record, cancellationToken);

        return Json(new
        {
            added = true,
            entryId = stored.ItemId,
            record = described,
            kind = "note",
            visibility = "internal",
            when = await LocalAsync(services, stored.CreatedUtc),
            mentioned = TimelineMentions.Find(text),
            message = $"The note was added to {described.Title ?? record.ToString()}. Only the people who work on it read it; the users it mentions who may read it are notified.",
        });
    }
}

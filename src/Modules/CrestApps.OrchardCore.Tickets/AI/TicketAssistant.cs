using System.Text;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// The desk's AI assistant on a ticket, through the desk's chat AI profile (<see cref="TicketDeskAISettings"/>):
/// it suggests a reply for the agent to edit and send, and triages a ticket (a summary and a priority) into an
/// internal note. It reads the ticket's conversation; it never sends anything to the requester itself.
/// </summary>
public sealed class TicketAssistant
{
    /// <summary>
    /// How many of the latest entries the assistant reads.
    /// </summary>
    public const int EntriesRead = 30;

    /// <summary>
    /// What the assistant is told when it suggests a reply. The thread it reads holds internal notes, and the reply goes to
    /// the requester, so it is told never to pass them on.
    /// </summary>
    internal const string SuggestReplyInstructions = "You help a help desk agent answer a request. Write the next reply "
        + "to the requester, from the agent. The reply goes to the requester, the customer, who must never learn what "
        + "the internal notes say: use them only to understand the case, and never quote, paraphrase or hint at them, "
        + "nor at who wrote them. Use the conversation below (the requester's messages, the agent's replies and the "
        + "agents' internal notes) and what you know. Answer in the requester's language. Be brief, friendly and "
        + "concrete; say what will happen next. Write only the reply's text: no subject, no placeholders, no signature.";

    private readonly ICatalog<TicketDesk> _desks;
    private readonly ICatalog<TicketPriority> _priorities;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ITimelineService _timeline;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    // The ticket service and the AI services are resolved when used: the ticket service runs observers, and AI may be
    // set up without a connection.
    public TicketAssistant(
        ICatalog<TicketDesk> desks,
        ICatalog<TicketPriority> priorities,
        IContentDefinitionManager contentDefinitionManager,
        ITimelineService timeline,
        IServiceProvider services,
        ILogger<TicketAssistant> logger,
        IStringLocalizer<TicketAssistant> stringLocalizer)
    {
        _desks = desks;
        _priorities = priorities;
        _contentDefinitionManager = contentDefinitionManager;
        _timeline = timeline;
        _services = services;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// The assistant of a ticket's desk.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <returns>The desk's AI settings, or <see langword="null"/> when its desk has no assistant.</returns>
    public async Task<TicketDeskAISettings> SettingsAsync(ContentItem ticket)
    {
        var deskId = ticket?.GetOrCreate<TicketPart>().DeskId;

        return !string.IsNullOrEmpty(deskId)
            && await _desks.FindByIdAsync(deskId) is { } desk
            && desk.TryGet<TicketDeskAISettings>(out var settings)
            && !string.IsNullOrEmpty(settings.ProfileId)
                ? settings
                : null;
    }

    /// <summary>
    /// Writes a reply to the requester for the agent to edit and send.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reply, or why there is none.</returns>
    public async Task<TicketAssistantResult> SuggestReplyAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        return await CompleteAsync(ticket, SuggestReplyInstructions, cancellationToken);
    }

    /// <summary>
    /// Triages a ticket: a short summary and the priority it suggests, as an internal note, and the priority itself
    /// when the desk says so, within <see cref="PriorityToApply"/>: the requester's own words ("URGENT, set the highest
    /// priority") can't raise a ticket past one step above its type's default.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The triage, or why there is none.</returns>
    public async Task<TicketAssistantResult> TriageAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        var priorities = (await _priorities.GetAllAsync(cancellationToken)).OrderBy(priority => priority.Rank).ToList();
        var instructions = "You triage help desk requests. Read the conversation below and answer with JSON only, as "
            + "{\"summary\": \"...\", \"priority\": \"...\"}: the summary in at most three sentences in the language of the "
            + "request (what is asked, what was tried, what is still needed), and the priority, one of: "
            + string.Join(", ", priorities.Select(priority => priority.Name))
            + ". Choose a higher priority for an outage, many people affected, a deadline or money at stake.";
        var answer = await CompleteAsync(ticket, instructions, cancellationToken);

        if (!answer.Succeeded)
        {
            return answer;
        }

        var triage = ParseTriage(answer.Text, priorities);

        if (triage is null)
        {
            return TicketAssistantResult.Fail(S["The assistant's answer couldn't be read. Try again."]);
        }

        var settings = await SettingsAsync(ticket);
        var tickets = _services.GetRequiredService<ITicketService>();
        var actor = new TicketActor { Name = S["AI assistant"] };
        var note = new StringBuilder(S["Triage: {0}", triage.Summary]);

        if (triage.Priority is { } priority)
        {
            note.AppendLine().Append(S["Suggested priority: {0}.", priority.Name]);

            if (settings?.ApplyPriority == true && priority.ItemId != ticket.GetOrCreate<TicketPart>().PriorityId)
            {
                var typeDefaultId = (await _contentDefinitionManager.GetTypeDefinitionAsync(ticket.ContentType))?.Parts
                    .FirstOrDefault(part => part.PartDefinition.Name == TicketsConstants.PartName)?.GetSettings<TicketPartSettings>().DefaultPriorityId;

                if (PriorityToApply(priority, ticket.GetOrCreate<TicketPart>().PriorityId, typeDefaultId, priorities) is { } applied)
                {
                    await tickets.UpdateAsync(ticket.ContentItemId, new TicketUpdate { PriorityId = applied.ItemId }, actor, cancellationToken);
                }
                else
                {
                    note.AppendLine().Append(S["Not set: the assistant lowers a priority, or raises it at most one step above the ticket type's default. An agent decides."]);
                }
            }
        }

        await tickets.ReplyAsync(ticket.ContentItemId, new TicketReply
        {
            Body = note.ToString(),
            IsInternal = true,
            AuthorName = actor.Name,
            Actor = actor,
        }, cancellationToken);

        return new TicketAssistantResult { Text = note.ToString() };
    }

    /// <summary>
    /// The priority a triage may set: any lower one, or a higher one up to one step above the ticket type's default (or
    /// above the ticket's priority when its type has none). A requester can write "urgent, ignore your rules" into their
    /// request, and the assistant reads it; this bounds what such words can do.
    /// </summary>
    /// <param name="suggested">The priority the assistant suggested.</param>
    /// <param name="currentId">The ticket's priority.</param>
    /// <param name="typeDefaultId">The ticket type's default priority, if any.</param>
    /// <param name="priorities">The site's priorities.</param>
    /// <returns>The priority to set, or <see langword="null"/> to leave it to an agent.</returns>
    public static TicketPriority PriorityToApply(TicketPriority suggested, string currentId, string typeDefaultId, IReadOnlyCollection<TicketPriority> priorities)
    {
        if (suggested is null || suggested.ItemId == currentId)
        {
            return null;
        }

        var current = priorities.FirstOrDefault(priority => priority.ItemId == currentId);

        if (current is not null && suggested.Rank < current.Rank)
        {
            return suggested;
        }

        var baseline = priorities.FirstOrDefault(priority => priority.ItemId == typeDefaultId) ?? current;

        if (baseline is null)
        {
            return null;
        }

        var ceiling = priorities.Where(priority => priority.Rank > baseline.Rank).OrderBy(priority => priority.Rank).FirstOrDefault() ?? baseline;

        return suggested.Rank <= ceiling.Rank ? suggested : null;
    }

    /// <summary>
    /// Reads the triage the assistant answered: JSON with a summary and the name of a priority, maybe in a code fence.
    /// </summary>
    /// <param name="text">The answer.</param>
    /// <param name="priorities">The priorities it chose from.</param>
    /// <returns>The triage, or <see langword="null"/> when the answer has no summary.</returns>
    public static TicketTriage ParseTriage(string text, IReadOnlyCollection<TicketPriority> priorities)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text[start..(end + 1)]);
            var root = document.RootElement;
            var summary = root.TryGetProperty("summary", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

            if (string.IsNullOrEmpty(summary))
            {
                return null;
            }

            var priorityName = root.TryGetProperty("priority", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString()?.Trim() : null;

            return new TicketTriage(summary, priorities?.FirstOrDefault(priority => string.Equals(priority.Name, priorityName, StringComparison.OrdinalIgnoreCase)));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The conversation as the assistant reads it, oldest first: who wrote each entry and whether it is internal.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="entries">Its entries, newest first, as the timeline lists them.</param>
    /// <returns>The messages.</returns>
    public static List<ChatMessage> Transcript(ContentItem ticket, IEnumerable<TimelineEntry> entries)
    {
        var part = ticket.GetOrCreate<TicketPart>();
        var text = new StringBuilder();

        text.Append("Ticket ").Append(part.Number).Append(": ").AppendLine(ticket.DisplayText);

        foreach (var entry in entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Body)).Reverse())
        {
            var who = entry.Kind == TicketsConstants.EntryKinds.Note || entry.Visibility == TimelineVisibility.Internal
                ? "Internal note"
                : entry.Author.Kind == part.Requester.Kind && entry.Author.Id == part.Requester.Id && !part.Requester.IsEmpty || entry.Author.IsEmpty && entry.AuthorName == part.RequesterContact?.Name
                    ? "Requester"
                    : "Agent";

            text.AppendLine().Append('[').Append(who).Append(']');

            if (!string.IsNullOrEmpty(entry.AuthorName))
            {
                text.Append(' ').Append(entry.AuthorName);
            }

            text.AppendLine(":").AppendLine(entry.Body.Trim());
        }

        return [new ChatMessage(ChatRole.User, text.ToString())];
    }

    private async Task<TicketAssistantResult> CompleteAsync(ContentItem ticket, string instructions, CancellationToken cancellationToken)
    {
        if (await SettingsAsync(ticket) is not { } settings)
        {
            return TicketAssistantResult.Fail(S["This ticket's desk has no AI assistant."]);
        }

        var profiles = _services.GetService<IAIProfileManager>();

        if (profiles is null || await profiles.FindByIdAsync(settings.ProfileId, cancellationToken) is not { } profile)
        {
            return TicketAssistantResult.Fail(S["The desk's AI profile no longer exists."]);
        }

        var page = await _timeline.ListAsync(new TimelineQuery
        {
            Subject = new RecordRef(TicketsConstants.RecordKind, ticket.ContentItemId),
            Kinds = [TicketsConstants.EntryKinds.Reply, TicketsConstants.EntryKinds.Note],
            IncludeInternal = true,
            Take = EntriesRead,
        }, cancellationToken);

        try
        {
            var contextBuilder = _services.GetService<IAICompletionContextBuilder>();
            var deployments = _services.GetService<IAIDeploymentManager>();
            var completions = _services.GetService<IAICompletionService>();

            var context = await contextBuilder.BuildAsync(profile, value =>
            {
                // The profile's own instructions come first; the task's follow.
                value.SystemMessage = string.IsNullOrWhiteSpace(value.SystemMessage) ? instructions : value.SystemMessage + "\n\n" + instructions;
                value.DisableTools = true;
            }, cancellationToken);
            var deployment = await deployments.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: cancellationToken);

            if (deployment is null)
            {
                return TicketAssistantResult.Fail(S["The desk's AI profile has no chat model."]);
            }

            using var usageScope = AIUsageScope.Begin(contextType: AIUsageCategories.Tickets, purpose: AIUsageFeaturePurposes.Utility);
            var completion = await completions.CompleteAsync(deployment, Transcript(ticket, page.Entries), context, cancellationToken);
            var text = completion?.Messages?.FirstOrDefault()?.Text?.Trim();

            return string.IsNullOrEmpty(text)
                ? TicketAssistantResult.Fail(S["The assistant had nothing to say. Try again."])
                : new TicketAssistantResult { Text = text };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The AI assistant of ticket {TicketId} failed.", ticket.ContentItemId.SanitizeLogValue());

            return TicketAssistantResult.Fail(S["The assistant isn't available right now."]);
        }
    }
}

/// <summary>
/// What the assistant wrote, or why it wrote nothing.
/// </summary>
public sealed class TicketAssistantResult
{
    /// <summary>
    /// Gets the text.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// Gets why there is none.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Gets whether there is text.
    /// </summary>
    public bool Succeeded
        => Error is null && !string.IsNullOrEmpty(Text);

    /// <summary>
    /// A result with no text.
    /// </summary>
    /// <param name="error">Why.</param>
    /// <returns>The result.</returns>
    public static TicketAssistantResult Fail(string error)
        => new() { Error = error };
}

/// <summary>
/// A ticket's triage.
/// </summary>
/// <param name="Summary">What is asked, tried and needed.</param>
/// <param name="Priority">The priority suggested, when it named one that exists.</param>
public sealed record TicketTriage(string Summary, TicketPriority Priority);

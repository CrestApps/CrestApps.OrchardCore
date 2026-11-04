using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;

/// <summary>
/// Adds or updates messaging broadcasts from a <c>MessagingBroadcasts</c> recipe step. A broadcast is matched by its
/// <c>ItemId</c>, so running a recipe twice adds nothing.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "MessagingBroadcasts", "Broadcasts": [ { "ItemId": "...", "Name": "Holiday hours", "Channel": "sms",
///   "ServiceAddress": "+12025550100", "Body": "We close at noon on Friday.", "Recipients": [ "+12025550101" ] } ] }
/// </code>
/// A recipe never sends a message: a new broadcast is always a draft, whatever status the recipe carries, and a
/// broadcast that is queued or has sent on this site is left as it is.
/// </remarks>
internal sealed class MessagingBroadcastsStep : CatalogRecipeStepBase<MessagingBroadcast>
{
    public const string StepKey = "MessagingBroadcasts";

    public const string EntriesName = "Broadcasts";

    private readonly IMessagingBroadcastManager _manager;

    internal readonly IStringLocalizer S;

    public MessagingBroadcastsStep(IMessagingBroadcastManager manager, IStringLocalizer<MessagingBroadcastsStep> stringLocalizer)
        : base(StepKey, EntriesName)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override ValueTask<MessagingBroadcast> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    protected override async ValueTask<MessagingBroadcast> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var broadcast = await _manager.NewAsync(token);

        // The manager stamps the new broadcast; the recipe gives its content, never its delivery.
        CatalogDeploymentSerializer.Populate(broadcast, token);
        ResetDelivery(broadcast);

        return broadcast;
    }

    protected override ValueTask<MessagingBroadcast> PrepareUpdateAsync(MessagingBroadcast entry, JsonObject token)
    {
        // Only a draft takes changes: a queued or sent broadcast belongs to this site's delivery.
        if (entry.Status == MessagingBroadcastStatus.Draft)
        {
            CatalogDeploymentSerializer.Populate(entry, token);
            ResetDelivery(entry);
        }

        return ValueTask.FromResult(entry);
    }

    protected override async ValueTask CommitUpdateAsync(MessagingBroadcast entry, JsonObject token)
        => await _manager.UpdateAsync(entry);

    protected override void Preparing(MessagingBroadcast entry, JsonObject token)
    {
        entry.Name = entry.Name?.Trim();
        entry.Recipients = (entry.Recipients ?? [])
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
            .Select(recipient => recipient.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(MessagingBroadcast entry)
    {
        var result = await _manager.ValidateAsync(entry);

        if (string.IsNullOrEmpty(entry.Name))
        {
            result.Fail(new ValidationResult(S["A name is required."]));
        }

        if (string.IsNullOrEmpty(entry.Channel) || string.IsNullOrEmpty(entry.ServiceAddress))
        {
            result.Fail(new ValidationResult(S["The broadcast '{0}' needs the channel and the address it sends from.", entry.Name]));
        }

        if (string.IsNullOrWhiteSpace(entry.Body))
        {
            result.Fail(new ValidationResult(S["A message body is required."]));
        }

        if (entry.Recipients.Count == 0)
        {
            result.Fail(new ValidationResult(S["At least one recipient is required."]));
        }

        return result;
    }

    protected override ValueTask CreateAsync(MessagingBroadcast entry)
        => _manager.CreateAsync(entry);

    private static void ResetDelivery(MessagingBroadcast broadcast)
    {
        broadcast.Status = MessagingBroadcastStatus.Draft;
        broadcast.ProcessedRecipients = [];
        broadcast.SentCount = 0;
        broadcast.FailedCount = 0;
        broadcast.CompletedUtc = null;
    }
}

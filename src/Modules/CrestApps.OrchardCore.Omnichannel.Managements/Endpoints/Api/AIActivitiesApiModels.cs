using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Endpoints.Api;

/// <summary>
/// Starts an AI call or text conversation with one customer.
/// </summary>
public sealed class ApiAIActivityRequest
{
    /// <summary>
    /// Gets or sets the AI profile that holds the conversation.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the phone number, in E.164 form such as <c>+15551234567</c>.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the number it comes from (a channel address id). Empty uses the profile's or the default one.
    /// </summary>
    public string FromEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the campaign it belongs to.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the contact it is with.
    /// </summary>
    public string ContactId { get; set; }

    /// <summary>
    /// Gets or sets the content type of the activity's subject.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the conversation variables the profile sees.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets when to start, in UTC. Empty starts now.
    /// </summary>
    public DateTime? ScheduledUtc { get; set; }
}

/// <summary>
/// Starts AI calls or texts with many customers at once.
/// </summary>
public sealed class ApiAIActivityBatchRequest
{
    /// <summary>
    /// The most customers in one batch.
    /// </summary>
    public const int MaxItems = 1000;

    /// <summary>
    /// Gets or sets the AI profile that holds the conversations.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets <c>call</c> or <c>text</c>. Defaults to <c>call</c>.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the number they come from.
    /// </summary>
    public string FromEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the campaign they belong to.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets when to start, in UTC. Empty starts now.
    /// </summary>
    public DateTime? ScheduledUtc { get; set; }

    /// <summary>
    /// Gets or sets the customers.
    /// </summary>
    public List<ApiAIActivityBatchItem> Items { get; set; } = [];
}

/// <summary>
/// One customer of a batch.
/// </summary>
public sealed class ApiAIActivityBatchItem
{
    /// <summary>
    /// Gets or sets the phone number.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the contact.
    /// </summary>
    public string ContactId { get; set; }

    /// <summary>
    /// Gets or sets the conversation variables for this customer.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];
}

/// <summary>
/// An AI call or text conversation.
/// </summary>
/// <param name="Id">The activity.</param>
/// <param name="Kind">Call or Sms.</param>
/// <param name="Status">Where it stands, such as Pending, InProgress, Completed, Failed or Cancelled.</param>
/// <param name="ProfileId">The AI profile.</param>
/// <param name="To">The phone number.</param>
/// <param name="ContactId">The contact, when known.</param>
/// <param name="ConversationId">The AI conversation, once it started; read it from the conversations API.</param>
/// <param name="DispositionId">The disposition, once it ended.</param>
/// <param name="ScheduledUtc">When it is due.</param>
/// <param name="CompletedUtc">When it ended.</param>
public sealed record ApiAIActivity(
    string Id,
    string Kind,
    string Status,
    string ProfileId,
    string To,
    string ContactId,
    string ConversationId,
    string DispositionId,
    DateTime ScheduledUtc,
    DateTime? CompletedUtc)
{
    internal static ApiAIActivity From(OmnichannelActivity activity)
        => new(
            activity.ItemId,
            activity.Kind.ToString(),
            activity.Status.ToString(),
            activity.AIProfileId,
            activity.PreferredDestination,
            activity.ContactContentItemId,
            activity.AISessionId,
            activity.DispositionId,
            activity.ScheduledUtc,
            activity.CompletedUtc);
}

/// <summary>
/// A batch of AI calls or texts.
/// </summary>
/// <param name="Id">The batch.</param>
/// <param name="CreatedUtc">When it was made.</param>
/// <param name="Counts">How many are in each status.</param>
/// <param name="Activities">The calls or texts, in the order given.</param>
public sealed record ApiAIActivityBatch(string Id, DateTime CreatedUtc, IReadOnlyDictionary<string, int> Counts, IReadOnlyList<ApiAIActivity> Activities);

/// <summary>
/// A batch of AI calls or texts started over the API: the activities it made, kept so the batch can be read back.
/// </summary>
public sealed class AIApiActivityBatch
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the AI profile.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the activities, in the order given.
    /// </summary>
    public List<string> ActivityIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

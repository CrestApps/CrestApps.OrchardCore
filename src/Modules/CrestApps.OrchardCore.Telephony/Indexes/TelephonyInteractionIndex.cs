using CrestApps.OrchardCore.Telephony.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Indexes;

/// <summary>
/// Search index for <see cref="TelephonyInteraction"/> documents.
/// </summary>
public sealed class TelephonyInteractionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the logical interaction identifier.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific call identifier.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the provider.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the user identifier that owns the interaction.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name that owns the interaction.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the direction of the call.
    /// </summary>
    public CallDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the call.
    /// </summary>
    public CallOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the time, in UTC, when the call started.
    /// </summary>
    public DateTime StartedUtc { get; set; }
}

/// <summary>
/// Maps <see cref="TelephonyInteraction"/> documents to the <see cref="TelephonyInteractionIndex"/>.
/// </summary>
public sealed class TelephonyInteractionIndexProvider : IndexProvider<TelephonyInteraction>
{
    public override void Describe(DescribeContext<TelephonyInteraction> context)
    {
        context.For<TelephonyInteractionIndex>()
            .Map(interaction => new TelephonyInteractionIndex
            {
                InteractionId = interaction.InteractionId,
                CallId = interaction.CallId,
                ProviderName = interaction.ProviderName,
                UserId = interaction.UserId,
                UserName = interaction.UserName,
                Direction = interaction.Direction,
                Outcome = interaction.Outcome,
                StartedUtc = interaction.StartedUtc,
            });
    }
}

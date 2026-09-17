using CrestApps.OrchardCore.Telephony.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Indexes;

/// <summary>
/// Maps <see cref="TelephonyInteraction"/> documents to the <see cref="TelephonyInteractionIndex"/>.
/// </summary>
public sealed class TelephonyInteractionIndexProvider : IndexProvider<TelephonyInteraction>
{
    /// <inheritdoc/>
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
                IsExtension = interaction.IsExtension,
                Outcome = interaction.Outcome,
                StartedUtc = interaction.StartedUtc,
                IsVoicemail = interaction.IsVoicemail,
                VoicemailReadUtc = interaction.VoicemailReadUtc,
            });
    }
}

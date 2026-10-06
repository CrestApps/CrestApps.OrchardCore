using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What a dialer attempt records on its interaction: the profile that dialed it, which attempt it was, whether an agent
/// was ever connected to it and how it ended. Every value is stored as a plain string, which the store hands back as a
/// string.
/// </summary>
/// <remarks>
/// A campaign call is placed before an agent is on it: the customer is dialed first and the agent's leg is joined only
/// once a person answers. Until then the call is the dialer's, not the agent's, and these values are how every screen
/// and every release decision tells the two apart.
/// </remarks>
public static class DialerCallMetadata
{
    /// <summary>
    /// The key of the dialer profile that placed the call.
    /// </summary>
    public const string DialerProfileIdKey = "dialer_profile_id";

    /// <summary>
    /// The key of the attempt number of the call, counted from one.
    /// </summary>
    public const string AttemptNumberKey = "dialer_attempt_number";

    /// <summary>
    /// The key of the most attempts the dialer profile allowed when the call was placed.
    /// </summary>
    public const string MaxAttemptsKey = "dialer_max_attempts";

    /// <summary>
    /// The key of the instant an agent was first connected to the call.
    /// </summary>
    public const string AgentJoinedUtcKey = "dialer_agent_joined_utc";

    /// <summary>
    /// The key of the attempt's outcome, one of <see cref="DialerAttemptOutcomes"/>, recorded when the call ends.
    /// </summary>
    public const string OutcomeKey = "dialer_attempt_outcome";

    /// <summary>
    /// Records the profile and the attempt on the interaction of a call the dialer is about to place.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    /// <param name="profile">The dialer profile placing the call.</param>
    /// <param name="attemptNumber">The attempt number of the call, counted from one.</param>
    public static void StampDial(Interaction interaction, DialerProfile profile, int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        ArgumentNullException.ThrowIfNull(profile);

        interaction.TechnicalMetadata[DialerProfileIdKey] = profile.ItemId;
        interaction.TechnicalMetadata[AttemptNumberKey] = attemptNumber.ToString(CultureInfo.InvariantCulture);
        interaction.TechnicalMetadata[MaxAttemptsKey] = profile.MaxAttempts.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Whether the interaction is an outbound call a campaign dialer profile placed (preview or paced). A queued
    /// callback is dialed with a profile of its own but is not campaign work, so it is not one.
    /// </summary>
    /// <param name="interaction">The interaction to check.</param>
    public static bool IsCampaignDial(Interaction interaction)
    {
        if (interaction is null ||
            interaction.Direction != InteractionDirection.Outbound ||
            string.IsNullOrEmpty(interaction.ActivityItemId))
        {
            return false;
        }

        var profileId = GetDialerProfileId(interaction);

        return !string.IsNullOrEmpty(profileId) && !QueueCallbackDialerProfile.IsCallbackProfile(profileId);
    }

    /// <summary>
    /// The dialer profile that placed the call, or <see langword="null"/>.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    public static string GetDialerProfileId(Interaction interaction)
        => Read(interaction, DialerProfileIdKey);

    /// <summary>
    /// The attempt number of the call, or <see langword="null"/> when the dialer did not record it.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    public static int? GetAttemptNumber(Interaction interaction)
        => ReadInt(interaction, AttemptNumberKey);

    /// <summary>
    /// The most attempts the profile allowed when the call was placed, or <see langword="null"/>.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    public static int? GetMaxAttempts(Interaction interaction)
        => ReadInt(interaction, MaxAttemptsKey);

    /// <summary>
    /// Records that an agent was connected to the call, keeping the first instant it happened.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    /// <param name="joinedUtc">When the agent was connected.</param>
    /// <returns><see langword="true"/> when this is the first time it is recorded.</returns>
    public static bool MarkAgentJoined(Interaction interaction, DateTime joinedUtc)
    {
        if (interaction is null || HasAgentJoined(interaction))
        {
            return false;
        }

        interaction.TechnicalMetadata[AgentJoinedUtcKey] = joinedUtc.ToString("O", CultureInfo.InvariantCulture);

        return true;
    }

    /// <summary>
    /// Whether an agent was ever connected to the call.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    public static bool HasAgentJoined(Interaction interaction)
        => !string.IsNullOrEmpty(Read(interaction, AgentJoinedUtcKey));

    /// <summary>
    /// Whether the call is a campaign dial that no agent has been connected to: the customer is still being dialed, a
    /// machine answered, or the customer hung up before the agent joined. Such a call is never the agent's work.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    public static bool IsAwaitingAgent(Interaction interaction)
        => IsCampaignDial(interaction) && !HasAgentJoined(interaction);

    /// <summary>
    /// Records how the attempt ended.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    /// <param name="outcome">One of <see cref="DialerAttemptOutcomes"/>.</param>
    public static void SetOutcome(Interaction interaction, string outcome)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (!string.IsNullOrEmpty(outcome))
        {
            interaction.TechnicalMetadata[OutcomeKey] = outcome;
        }
    }

    /// <summary>
    /// How the attempt ended, or <see langword="null"/> while it has not.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    public static string GetOutcome(Interaction interaction)
        => Read(interaction, OutcomeKey);

    /// <summary>
    /// Whether the provider said a machine or a fax, rather than a person, answered the call.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    public static bool WasAnsweredByMachine(Interaction interaction)
        => Read(interaction, ContactCenterConstants.TelephonyMetadata.AnswerClassification) is nameof(AnswerClassification.Machine) or nameof(AnswerClassification.Fax);

    private static string Read(Interaction interaction, string key)
        => interaction?.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(key, out var value) &&
            value?.ToString() is { Length: > 0 } text
            ? text
            : null;

    private static int? ReadInt(Interaction interaction, string key)
        => int.TryParse(Read(interaction, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
}

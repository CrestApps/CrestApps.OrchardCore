namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The SIP headers that let an agent's phone standing by for an over-dialing campaign answer the leg of a call the agent
/// was claimed for at once, instead of waiting for the platform's push about the claim to arrive.
/// </summary>
internal static class TelnyxStandbyAnswerHeaders
{
    /// <summary>
    /// Builds the <c>custom_headers</c> of an agent leg rung for a standby claim, or <see langword="null"/> when the leg is
    /// not one: without both the claim and its user the phone could not tell the leg is its own, so nothing is sent.
    /// </summary>
    /// <param name="reservationId">The reservation the agent was claimed under.</param>
    /// <param name="agentUserId">The user the agent profile represents.</param>
    /// <returns>The headers, or <see langword="null"/>.</returns>
    public static Dictionary<string, string>[] Create(string reservationId, string agentUserId)
    {
        if (string.IsNullOrWhiteSpace(reservationId) || string.IsNullOrWhiteSpace(agentUserId))
        {
            return null;
        }

        return
        [
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = TelnyxConstants.StandbyReservationSipHeader,
                ["value"] = reservationId.Trim(),
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = TelnyxConstants.StandbyAgentUserSipHeader,
                ["value"] = agentUserId.Trim(),
            },
        ];
    }
}

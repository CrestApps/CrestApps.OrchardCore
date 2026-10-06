using System.Text;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The rules an automated dialer profile follows for calls a person answers but no agent can take: when such a call
/// counts as abandoned, how long an unanswered call rings, and what the abandoned-call message says.
/// </summary>
/// <remarks>
/// They follow the common abandoned-call rules for telemarketing: a call a person answers is abandoned when it is not
/// connected to a live agent within two seconds, an abandoned call plays a short recorded message that says who called
/// and gives a number to call back, and an unanswered call rings for at least fifteen seconds before it is given up.
/// Operators remain responsible for confirming which rules apply to their calls.
/// </remarks>
public static class DialerAbandonment
{
    /// <summary>
    /// How long after a person answers an agent may take to be connected before the call counts as abandoned.
    /// </summary>
    /// <remarks>
    /// Measured from the answer, or from the moment the provider said a person answered when answering machines are
    /// screened, which is earlier than the end of the person's greeting the rules measure from, so it errs strict.
    /// </remarks>
    public static readonly TimeSpan ConnectThreshold = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The fewest seconds an automated call rings before it is given up as unanswered.
    /// </summary>
    public const int MinimumRingTimeoutSeconds = 15;

    /// <summary>
    /// The seconds an automated call rings when the profile does not say.
    /// </summary>
    public const int DefaultRingTimeoutSeconds = 30;

    /// <summary>
    /// The most seconds an automated call may ring.
    /// </summary>
    public const int MaximumRingTimeoutSeconds = 120;

    /// <summary>
    /// The token in the abandoned-call message replaced with the site name.
    /// </summary>
    public const string CompanyToken = "{company}";

    /// <summary>
    /// The token in the abandoned-call message replaced with the number the call was placed from, read out digit by
    /// digit.
    /// </summary>
    public const string NumberToken = "{number}";

    /// <summary>
    /// The message suggested for a new profile.
    /// </summary>
    public const string DefaultMessage = "Sorry we missed you. This call was from {company}. To be removed from our list or to reach us, please call {number}. Goodbye.";

    /// <summary>
    /// The seconds an automated call of the profile rings: what the profile says, never less than
    /// <see cref="MinimumRingTimeoutSeconds"/> nor more than <see cref="MaximumRingTimeoutSeconds"/>, and
    /// <see cref="DefaultRingTimeoutSeconds"/> when the profile says nothing.
    /// </summary>
    /// <param name="profile">The dialer profile placing the call.</param>
    public static int ResolveRingTimeoutSeconds(DialerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.RingTimeoutSeconds <= 0)
        {
            return DefaultRingTimeoutSeconds;
        }

        return Math.Clamp(profile.RingTimeoutSeconds, MinimumRingTimeoutSeconds, MaximumRingTimeoutSeconds);
    }

    /// <summary>
    /// Fills in the tokens of an abandoned-call message.
    /// </summary>
    /// <param name="template">The message as the profile stores it.</param>
    /// <param name="company">The name the call is made on behalf of.</param>
    /// <param name="number">The number the call was placed from.</param>
    /// <returns>The message to speak, or <see langword="null"/> when there is nothing to say.</returns>
    public static string Render(string template, string company, string number)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var message = template
            .Replace(CompanyToken, company?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(NumberToken, FormatNumberForSpeech(number), StringComparison.OrdinalIgnoreCase)
            .Trim();

        return message.Length == 0 ? null : message;
    }

    /// <summary>
    /// Writes a phone number the way it should be read out: one digit at a time, in the groups a listener expects.
    /// A North American number is read as its area code, exchange and line; any other number as its digits.
    /// </summary>
    /// <param name="number">The number in any format.</param>
    /// <returns>The number to speak, or an empty string when it has no digits.</returns>
    public static string FormatNumberForSpeech(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return string.Empty;
        }

        var digits = new string(number.Where(char.IsAsciiDigit).ToArray());

        if (digits.Length == 11 && digits[0] == '1')
        {
            digits = digits[1..];
        }

        if (digits.Length == 10)
        {
            return $"{Spell(digits[..3])}, {Spell(digits[3..6])}, {Spell(digits[6..])}";
        }

        return Spell(digits);
    }

    private static string Spell(string digits)
    {
        var builder = new StringBuilder(digits.Length * 2);

        foreach (var digit in digits)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(digit);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Why a call a person answered was abandoned.
    /// </summary>
    public static class Reasons
    {
        /// <summary>
        /// The reserved agent's leg failed to connect: their phone refused, did not answer or was unreachable.
        /// </summary>
        public const string AgentLegFailed = "agent_leg_failed";

        /// <summary>
        /// The platform could not ask the provider to connect the reserved agent.
        /// </summary>
        public const string AgentConnectFailed = "agent_connect_failed";

        /// <summary>
        /// The reserved agent could not be found to connect.
        /// </summary>
        public const string AgentUnavailable = "agent_unavailable";

        /// <summary>
        /// The agent was connected, but later than <see cref="ConnectThreshold"/> after the answer.
        /// </summary>
        public const string AgentConnectedLate = "agent_connected_late";

        /// <summary>
        /// The person hung up while waiting for an agent, after <see cref="ConnectThreshold"/> had passed.
        /// </summary>
        public const string CustomerHungUpWaiting = "customer_hung_up_waiting";

        /// <summary>
        /// A call an over-dialing Predictive profile placed without an agent was answered by a person, and no agent was
        /// free to take it.
        /// </summary>
        public const string NoAgentAvailable = "no_agent_available";
    }
}

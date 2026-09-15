using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Decides whether a human send would reach the contact outside the hours their queue keeps. Automated cadences
/// were already gated; human sends were not, so an agent working late could text a customer at three in the
/// morning their time with nothing in the way.
/// <para>
/// It warns rather than blocks. A person who genuinely needs to reach a customer out of hours exists and should
/// not be locked out, but overriding takes a permission, so it is a decision someone made rather than one nobody
/// noticed.
/// </para>
/// </summary>
public sealed class SmsQuietHoursGuard
{
    private readonly IBusinessHoursGate _businessHoursGate;
    private readonly ISmsQueuePolicyReader _queuePolicyReader;
    private readonly ISmsContactTimeZoneResolver _timeZoneResolver;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsQuietHoursGuard"/> class.
    /// </summary>
    public SmsQuietHoursGuard(
        IBusinessHoursGate businessHoursGate,
        ISmsQueuePolicyReader queuePolicyReader,
        ISmsContactTimeZoneResolver timeZoneResolver,
        IClock clock)
    {
        _businessHoursGate = businessHoursGate;
        _queuePolicyReader = queuePolicyReader;
        _timeZoneResolver = timeZoneResolver;
        _clock = clock;
    }

    /// <summary>
    /// Evaluates whether sending on this conversation right now lands in quiet hours.
    /// </summary>
    /// <param name="conversation">The conversation being sent on.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<SmsQuietHoursDecision> EvaluateAsync(SmsConversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (conversation.OwnerType != SmsConversationOwnerType.Queue || string.IsNullOrEmpty(conversation.OwnerId))
        {
            return SmsQuietHoursDecision.Open;
        }

        var policy = await _queuePolicyReader.ReadAsync(conversation.OwnerId, cancellationToken);

        // A queue that has defined no hours has not said when it is closed, and inventing quiet hours would warn
        // an agent about a boundary nobody set.
        if (!policy.Exists || string.IsNullOrWhiteSpace(policy.BusinessHoursCalendarId))
        {
            return SmsQuietHoursDecision.Open;
        }

        var timeZoneId = await _timeZoneResolver.ResolveAsync(conversation, cancellationToken);

        var open = await _businessHoursGate.IsOpenAsync(
            policy.BusinessHoursCalendarId,
            _clock.UtcNow,
            timeZoneId,
            cancellationToken);

        return open
            ? SmsQuietHoursDecision.Open
            : new SmsQuietHoursDecision(true, "It is currently outside this queue's business hours in the contact's local time.");
    }
}

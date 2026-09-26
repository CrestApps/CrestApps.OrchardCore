using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Writes a caller's route through an entry-point phone menu to the event log, so a call's history shows which
/// menus they heard, what they pressed, and what sent them where they went.
/// </summary>
internal static class IvrAudit
{
    /// <summary>
    /// Records one step of the menu.
    /// </summary>
    /// <param name="recorder">The audit recorder.</param>
    /// <param name="eventType">One of the phone-menu event types.</param>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="step">What the step was.</param>
    /// <param name="occurredUtc">When it happened.</param>
    /// <param name="idempotencyKey">A key naming exactly this step, so a redelivered webhook records it once.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static Task RecordIvrAsync(
        this IContactCenterAuditRecorder recorder,
        string eventType,
        Interaction interaction,
        IvrAuditStep step,
        DateTime occurredUtc,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(interaction);

        var data = ContactCenterCallAudit.ForInteraction(interaction);
        data.Reason = step.Reason;
        data.Target = step.Target;

        AddDetail(data, "nodeId", step.NodeId);
        AddDetail(data, "digits", step.Digits);
        AddDetail(data, "action", step.Action);
        AddDetail(data, "entryPointId", step.EntryPointId);
        AddDetail(data, "hangupCause", step.HangupCause);

        if (step.Attempt.HasValue)
        {
            data.Details["attempt"] = step.Attempt.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return recorder.RecordCallAsync(
            eventType,
            data,
            occurredUtc,
            ContactCenterActor.System,
            idempotencyKey,
            cancellationToken);
    }

    private static void AddDetail(CallLifecycleEventData data, string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            data.Details[key] = value;
        }
    }
}

/// <summary>
/// What a phone-menu audit record says.
/// </summary>
internal readonly record struct IvrAuditStep
{
    /// <summary>
    /// Gets the menu the step happened on.
    /// </summary>
    public string NodeId { get; init; }

    /// <summary>
    /// Gets what the caller pressed.
    /// </summary>
    public string Digits { get; init; }

    /// <summary>
    /// Gets the action taken.
    /// </summary>
    public string Action { get; init; }

    /// <summary>
    /// Gets where the action sent the caller.
    /// </summary>
    public string Target { get; init; }

    /// <summary>
    /// Gets why it happened.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets which try on the menu this was.
    /// </summary>
    public int? Attempt { get; init; }

    /// <summary>
    /// Gets the entry point whose menu it is.
    /// </summary>
    public string EntryPointId { get; init; }

    /// <summary>
    /// Gets the provider's reason a leg the step depended on ended, such as a transfer's destination being busy.
    /// </summary>
    public string HangupCause { get; init; }
}

using System.Diagnostics;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Carries a trace across an asynchronous hop, such as a queue, an outbox, a webhook inbox or a stored import entry.
/// The code that queues the work stores <see cref="Capture"/> next to it; the code that runs it later starts its span
/// with <see cref="StartLinkedActivity(ActivitySource, string, string, ActivityKind)"/>, which links back to the trace
/// that queued it rather than becoming the child of a request that ended long ago.
/// </summary>
public static class TraceContextPropagation
{
    /// <summary>
    /// The key under which a W3C <c>traceparent</c> is kept in a record's properties.
    /// </summary>
    public const string TraceParentKey = "traceparent";

    /// <summary>
    /// Gets the W3C <c>traceparent</c> of the current span, to store next to queued work.
    /// </summary>
    /// <returns>The <c>traceparent</c>, or <see langword="null"/> when there is no current span or it is not in the W3C format.</returns>
    public static string Capture()
    {
        var activity = Activity.Current;

        if (activity is null || activity.IdFormat != ActivityIdFormat.W3C)
        {
            return null;
        }

        return activity.Id;
    }

    /// <summary>
    /// Parses a stored W3C <c>traceparent</c>.
    /// </summary>
    /// <param name="traceParent">The <c>traceparent</c> captured with <see cref="Capture"/>.</param>
    /// <param name="context">The span context it identifies.</param>
    /// <returns><see langword="true"/> when the value is a valid <c>traceparent</c>.</returns>
    public static bool TryParse(string traceParent, out ActivityContext context)
    {
        if (string.IsNullOrEmpty(traceParent))
        {
            context = default;

            return false;
        }

        return ActivityContext.TryParse(traceParent, traceState: null, isRemote: true, out context);
    }

    /// <summary>
    /// Starts the span of work that was queued earlier, linked to the trace that queued it. The span is a child of the
    /// current span, if any, such as the span of the background task run that picked the work up.
    /// </summary>
    /// <param name="source">The activity source of the area.</param>
    /// <param name="name">The name of the span.</param>
    /// <param name="traceParent">The <c>traceparent</c> captured when the work was queued, or <see langword="null"/>.</param>
    /// <param name="kind">The kind of the span.</param>
    /// <returns>The started span, or <see langword="null"/> when nothing listens to <paramref name="source"/>.</returns>
    public static Activity StartLinkedActivity(
        ActivitySource source,
        string name,
        string traceParent,
        ActivityKind kind = ActivityKind.Internal)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!source.HasListeners())
        {
            return null;
        }

        var hasQueuedContext = TryParse(traceParent, out var context);
        var current = Activity.Current;

        // Still inside the trace that queued the work, such as a deferred task run as the request's scope ends: a
        // plain child of the current span says it all.
        if (current is not null && !current.IsStopped && hasQueuedContext && current.TraceId == context.TraceId)
        {
            return source.StartActivity(name, kind);
        }

        IEnumerable<ActivityLink> links = hasQueuedContext
            ? [new ActivityLink(context)]
            : null;

        // The current span ended, as the request span of a job that runs detached after the response: start a new
        // trace linked to the one that queued the work rather than a late child of a span that is over.
        if (current is not null && current.IsStopped)
        {
            Activity.Current = null;
        }

        // Otherwise the span stays a child of the current span, such as the span Orchard Core starts for each
        // background task run, and links to the trace that queued the work instead of joining it.
        return source.StartActivity(name, kind, parentContext: default, links: links);
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Reads and writes an activity's subject as the content item this host keeps subjects as.
/// </summary>
/// <remarks>
/// <para>
/// The activity carries its subject as the node it is stored as, because what a subject is belongs to
/// the host rather than to the suite. This is the one place that knows this host's answer, so the
/// screens, drivers and handlers that render a subject keep working with a content item and nothing
/// else has to care.
/// </para>
/// <para>
/// A content item and the node it serializes to produce the same text in that slot, so converting here
/// does not change what an activity stores.
/// </para>
/// </remarks>
public static class ActivitySubjectContentItemExtensions
{
    /// <summary>
    /// Gets the activity's subject as a content item.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The subject, or <see langword="null"/> when the activity has none.</returns>
    public static ContentItem GetSubjectContentItem(this OmnichannelActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return ToContentItem(activity.Subject);
    }

    /// <summary>
    /// Sets the activity's subject from a content item.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="subject">The subject, or <see langword="null"/> to clear it.</param>
    public static void SetSubjectContentItem(this OmnichannelActivity activity, ContentItem subject)
    {
        ArgumentNullException.ThrowIfNull(activity);

        activity.Subject = ToNode(subject);
    }

    /// <summary>
    /// Gets the display text of the activity's subject.
    /// </summary>
    /// <remarks>
    /// Read off the node rather than through a materialized content item, because every caller of this
    /// wants one string and materializing the whole subject to get it is wasted work on a list screen.
    /// </remarks>
    /// <param name="activity">The activity.</param>
    /// <returns>The display text, or <see langword="null"/> when there is none.</returns>
    public static string GetSubjectDisplayText(this OmnichannelActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return activity.Subject?[nameof(ContentItem.DisplayText)]?.ToString();
    }

    /// <summary>
    /// Converts a stored subject node to a content item.
    /// </summary>
    /// <param name="subject">The stored subject, which may be <see langword="null"/>.</param>
    /// <returns>The content item, or <see langword="null"/>.</returns>
    public static ContentItem ToContentItem(JsonObject subject)
        => subject?.Deserialize<ContentItem>(JOptions.Default);

    /// <summary>
    /// Converts a content item to the node an activity stores its subject as.
    /// </summary>
    /// <param name="subject">The content item, which may be <see langword="null"/>.</param>
    /// <returns>The node, or <see langword="null"/>.</returns>
    public static JsonObject ToNode(ContentItem subject)
        => subject is null ? null : JsonSerializer.SerializeToNode(subject, JOptions.Default)?.AsObject();
}

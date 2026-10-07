using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Builds a new activity batch from an existing one, so a load that worked can be run again without filling in its
/// form a second time.
/// </summary>
internal static class ActivityBatchCloner
{
    /// <summary>
    /// Creates a copy of <paramref name="source"/> that carries every setting of the source but nothing of its load.
    /// </summary>
    /// <remarks>
    /// The copy takes its identity -- item id, creation time and owner -- from <paramref name="newBatch"/>, a batch
    /// freshly initialized by the catalog manager for the current user. Whatever the source has done is left behind:
    /// the copy is <see cref="OmnichannelActivityBatchStatus.New"/>, has never been modified, and has no load counts,
    /// so it neither looks loaded nor reports a load it never ran. Settings stored in the property bag by editor
    /// drivers, such as the lead filters, travel with it.
    /// </remarks>
    /// <param name="source">The batch to copy the settings from.</param>
    /// <param name="newBatch">A newly initialized batch that supplies the identity of the copy.</param>
    /// <param name="displayText">The name of the copy.</param>
    /// <returns>The new batch, ready to be created.</returns>
    public static OmnichannelActivityBatch CreateCopy(OmnichannelActivityBatch source, OmnichannelActivityBatch newBatch, string displayText)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(newBatch);

        // Clone copies every setting, including the property bag, so a setting added to the batch later is cloned
        // without this method having to learn about it. Only the identity and the load state are then replaced.
        var copy = source.Clone();

        copy.ItemId = newBatch.ItemId;
        copy.DisplayText = displayText;
        copy.CreatedUtc = newBatch.CreatedUtc;
        copy.ModifiedUtc = null;
        copy.OwnerId = newBatch.OwnerId;
        copy.Author = newBatch.Author;

        copy.Status = OmnichannelActivityBatchStatus.New;
        copy.ResetLoadCounts();

        // A batch that has never been loaded has no totals at all; zero would read as a load that found nothing.
        copy.TotalLoaded = null;
        copy.TotalMatched = null;

        return copy;
    }
}

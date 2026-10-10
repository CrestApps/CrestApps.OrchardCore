using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Records on a lead the file imports it arrived in or was updated by.
/// </summary>
internal static class LeadImports
{
    /// <summary>
    /// Records the import on the lead. An import the lead already records is left as it is, so a resumed import that
    /// reads a row again does not record it twice.
    /// </summary>
    /// <param name="part">The lead part.</param>
    /// <param name="entry">The import entry, or <see langword="null"/> when the lead is not being imported.</param>
    /// <returns><see langword="true"/> when the import was added; otherwise, <see langword="false"/>.</returns>
    public static bool Record(LeadPart part, ContentTransferEntry entry)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (entry is null ||
            entry.Direction != ContentTransferDirection.Import ||
            string.IsNullOrEmpty(entry.EntryId))
        {
            return false;
        }

        part.Imports ??= [];

        if (part.Imports.Any(import => string.Equals(import?.EntryId, entry.EntryId, StringComparison.Ordinal)))
        {
            return false;
        }

        part.Imports.Add(new LeadImport
        {
            EntryId = entry.EntryId,
            FileName = string.IsNullOrWhiteSpace(entry.UploadedFileName) ? null : entry.UploadedFileName.Trim(),
            ImportedUtc = entry.CreatedUtc,
        });

        return true;
    }
}

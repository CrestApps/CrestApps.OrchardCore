using System.Data;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer.Services;

/// <summary>
/// Decides which content type each row of an import is created as.
/// </summary>
public static class ContentImportRowTypes
{
    /// <summary>
    /// Returns the content types the rows of an import of <paramref name="contentType"/> may be, the content type
    /// itself first, or only the content type when no provider groups it with others.
    /// </summary>
    /// <param name="providers">The providers.</param>
    /// <param name="contentType">The content type the file is imported as.</param>
    public static IReadOnlyList<string> Collect(IEnumerable<IContentImportRowTypeProvider> providers, ContentTypeDefinition contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        var types = new List<string> { contentType.Name };

        foreach (var provider in providers ?? [])
        {
            foreach (var type in provider.GetRowContentTypes(contentType) ?? [])
            {
                if (!string.IsNullOrEmpty(type) && !types.Contains(type, StringComparer.Ordinal))
                {
                    types.Add(type);
                }
            }
        }

        return types;
    }

    /// <summary>
    /// Returns the content type a row is created as: the type its <see cref="ContentTransferColumns.ContentType"/>
    /// cell names when the import allows several types, otherwise the content type of the import. Returns
    /// <see langword="null"/> when the cell names a type the import does not allow, which <paramref name="named"/>
    /// then holds.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="contentType">The content type of the import.</param>
    /// <param name="rowContentTypes">The types the rows may be; empty when every row is the content type of the import.</param>
    /// <param name="named">The type the row named, when it is not allowed.</param>
    public static string Resolve(DataRow row, string contentType, IReadOnlyCollection<string> rowContentTypes, out string named)
    {
        ArgumentNullException.ThrowIfNull(row);

        named = null;

        // Only an import whose type is grouped with others reads the column; any other file keeps it as data.
        if (rowContentTypes is null || rowContentTypes.Count < 2)
        {
            return contentType;
        }

        var index = -1;

        for (var i = 0; i < row.Table.Columns.Count; i++)
        {
            if (string.Equals(row.Table.Columns[i].ColumnName, ContentTransferColumns.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                index = i;

                break;
            }
        }

        var value = index < 0 ? null : row[index]?.ToString()?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return contentType;
        }

        if (rowContentTypes.FirstOrDefault(type => string.Equals(type, value, StringComparison.OrdinalIgnoreCase)) is { } allowed)
        {
            return allowed;
        }

        named = value;

        return null;
    }

    /// <summary>
    /// Returns the format that reads an imported file as the content type: the first, by extension, that handles the
    /// file name and imports the content type.
    /// </summary>
    /// <param name="providers">The formats.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="contentType">The content type the file is imported as.</param>
    public static IContentTransferFileFormatProvider ResolveImportFormat(IEnumerable<IContentTransferFileFormatProvider> providers, string fileName, string contentType)
        => (providers ?? [])
            .OrderBy(provider => provider.FileExtension, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(provider => provider.CanHandle(fileName) && provider.CanImport(contentType));
}

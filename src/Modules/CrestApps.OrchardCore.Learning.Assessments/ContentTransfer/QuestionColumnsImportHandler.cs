using System.Data;
using System.Globalization;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer;

/// <summary>
/// A column of a question part in a ContentTransfer spreadsheet.
/// </summary>
/// <typeparam name="TPart">The part.</typeparam>
/// <param name="Property">The property, the column is named <c>{PartName}_{Property}</c>.</param>
/// <param name="Description">What the column holds.</param>
/// <param name="Read">Reads the column into the part; an empty cell leaves the part as it is.</param>
/// <param name="Write">Writes the part into the column.</param>
public sealed record QuestionColumn<TPart>(string Property, string Description, Action<TPart, string> Read, Func<TPart, string> Write)
    where TPart : ContentPart, new();

/// <summary>
/// Imports and exports the columns of a question part, so questions can be written in a spreadsheet and imported through
/// ContentTransfer.
/// </summary>
/// <typeparam name="TPart">The part.</typeparam>
public abstract class QuestionColumnsImportHandler<TPart> : ContentImportHandlerBase, IContentPartImportHandler
    where TPart : ContentPart, new()
{
    /// <summary>
    /// The separator of list values in a cell, such as the options of a choice question.
    /// </summary>
    public const char ListSeparator = '|';

    /// <summary>
    /// Gets the columns of the part.
    /// </summary>
    protected abstract IReadOnlyList<QuestionColumn<TPart>> Columns { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentPartContext context)
        => Columns.Select(column => new ImportColumn
        {
            Name = $"{context.ContentTypePartDefinition.Name}_{column.Property}",
            Description = column.Description,
        }).ToArray();

    /// <inheritdoc />
    public Task ImportAsync(ContentPartImportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = context.ContentTypePartDefinition.Name;

        foreach (DataColumn dataColumn in context.Columns)
        {
            var column = Columns.FirstOrDefault(candidate => Is(dataColumn.ColumnName, new ImportColumn { Name = $"{name}_{candidate.Property}" }));
            var value = column is null ? null : context.Row[dataColumn]?.ToString();

            if (column is null || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            context.ContentItem.Alter<TPart>(name, part => column.Read(part, value.Trim()));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ExportAsync(ContentPartExportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = context.ContentTypePartDefinition.Name;
        var part = context.ContentItem.Get<TPart>(name);

        if (part is null)
        {
            return Task.CompletedTask;
        }

        foreach (var column in Columns)
        {
            var columnName = $"{name}_{column.Property}";

            if (context.Row.Table.Columns.Contains(columnName))
            {
                context.Row[columnName] = column.Write(part) ?? string.Empty;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads a number written with a dot, or in the site's culture.
    /// </summary>
    /// <param name="value">The cell.</param>
    /// <returns>The number, or <see langword="null"/>.</returns>
    protected static decimal? Decimal(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out number) ? number : null;

    /// <summary>
    /// Reads yes or no: true, yes, 1 or x.
    /// </summary>
    /// <param name="value">The cell.</param>
    /// <returns>The value.</returns>
    protected static bool Boolean(string value)
        => value.Trim().ToLowerInvariant() is "true" or "yes" or "1" or "x" or "y";

    /// <summary>
    /// Splits a list cell.
    /// </summary>
    /// <param name="value">The cell.</param>
    /// <returns>The values.</returns>
    protected static List<string> List(string value)
        => value.Split(ListSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>
    /// Writes a number with a dot.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The cell.</returns>
    protected static string Text(decimal? value)
        => value?.ToString(CultureInfo.InvariantCulture);
}

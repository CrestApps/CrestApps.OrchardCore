using System.Globalization;
using CrestApps.OrchardCore.Surveys.Core.Controls.MatrixDropdown;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the columns of drop-down matrices in the designer. Blank columns are dropped; new ones get stable ids.
/// </summary>
public sealed class MatrixDropdownSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<MatrixDropdownSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixDropdownSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MatrixDropdownSettingsDisplayDriver(IStringLocalizer<MatrixDropdownSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.MatrixDropdown;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Drop-Down Matrix Settings"];

    /// <inheritdoc />
    protected override Task UpdateSettingsAsync(SurveyQuestion question, MatrixDropdownSettings settings, UpdateEditorContext context)
    {
        var columns = new List<SurveySubQuestion>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in settings.Columns ?? [])
        {
            if (string.IsNullOrWhiteSpace(column?.Text))
            {
                continue;
            }

            column.Id = string.IsNullOrWhiteSpace(column.Id) ? IdGenerator.GenerateId() : column.Id.Trim();
            column.Text = column.Text.Trim();
            column.Name = string.IsNullOrWhiteSpace(column.Name) || !names.Add(column.Name.Trim())
                ? NextName(names)
                : column.Name.Trim();
            columns.Add(column);
        }

        settings.Columns = columns;

        return Task.CompletedTask;
    }

    private static string NextName(HashSet<string> names)
    {
        for (var i = 1; ; i++)
        {
            var name = "column_" + i.ToString(CultureInfo.InvariantCulture);

            if (names.Add(name))
            {
                return name;
            }
        }
    }
}

using System.Text.Json;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Resources.Endpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Resources.ViewComponents;

/// <summary>
/// Picks records of one kind (orders, assets, tickets, any kind a module registers a record source for). Other modules
/// invoke it by name (<c>RecordPicker</c>) from their views; it searches through the
/// <c>Admin/api/crestapps/records/search</c> endpoint. The value is the record id, or <c>Kind:Id</c> with
/// <c>valueType = "reference"</c> for a field that stores records of any kind.
/// </summary>
public sealed class RecordPickerViewComponent : ViewComponent
{
    private readonly IRecordDirectory _directory;
    private readonly IStringLocalizerFactory _localizerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordPickerViewComponent"/> class.
    /// </summary>
    /// <param name="directory">The record directory.</param>
    /// <param name="localizerFactory">The localizer factory, to name the kind.</param>
    public RecordPickerViewComponent(IRecordDirectory directory, IStringLocalizerFactory localizerFactory)
    {
        _directory = directory;
        _localizerFactory = localizerFactory;
    }

    /// <summary>
    /// Renders the picker.
    /// </summary>
    /// <param name="name">The input name.</param>
    /// <param name="kind">The kind of record to offer.</param>
    /// <param name="selectedValues">The values chosen now, in the form <paramref name="valueType"/> names.</param>
    /// <param name="valueType"><c>id</c> (the default) or <c>reference</c>.</param>
    /// <param name="multiple">Whether several records can be chosen.</param>
    /// <param name="buttonText">The button text.</param>
    /// <param name="smallButton">Whether the button is small.</param>
    /// <param name="required">Whether a choice is required.</param>
    /// <returns>The view, or nothing when no module registers the kind.</returns>
    public async Task<IViewComponentResult> InvokeAsync(
        string name,
        string kind,
        IEnumerable<string> selectedValues = null,
        string valueType = RecordPickerValueTypes.Id,
        bool multiple = false,
        string buttonText = null,
        bool smallButton = false,
        bool required = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(kind);

        if (_directory.GetSource(kind) is not { } source)
        {
            return Content(string.Empty);
        }

        var asReference = string.Equals(valueType, RecordPickerValueTypes.Reference, StringComparison.OrdinalIgnoreCase);
        var values = (selectedValues ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
        var ids = values
            .Select(value => asReference && RecordRef.TryParse(value, out var record) ? record.Id : value)
            .ToArray();

        var summaries = ids.Length == 0
            ? new Dictionary<string, RecordSummary>()
            : await source.GetSummariesAsync(ids, HttpContext.RequestAborted);

        var initial = values
            .Select((value, index) => new
            {
                value,
                text = summaries.TryGetValue(ids[index], out var summary) && !string.IsNullOrEmpty(summary.Title) ? summary.Title : ids[index],
            })
            .ToArray();

        return View(new RecordPickerViewModel
        {
            Id = $"record-picker-{name.Replace('.', '-').Replace('[', '-').Replace(']', '-')}",
            InputName = name,
            Kind = source.Kind,
            KindDisplayName = _localizerFactory.Localize(source.DisplayName).Value,
            ValueType = asReference ? RecordPickerValueTypes.Reference : RecordPickerValueTypes.Id,
            Multiple = multiple,
            ButtonText = buttonText,
            SmallButton = smallButton,
            Required = required,
            InitialItemsJson = JsonSerializer.Serialize(initial),
        });
    }
}

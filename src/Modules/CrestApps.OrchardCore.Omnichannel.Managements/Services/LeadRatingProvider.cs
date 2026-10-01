using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Reads the ratings a lead can carry: the options of the predefined list of the lead part's
/// <see cref="LeadPart.Rating"/> field, or the <see cref="LeadRatings"/> values when the field has none.
/// </summary>
public sealed class LeadRatingProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    private IReadOnlyList<ListValueOption> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadRatingProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public LeadRatingProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Returns the rating options in the order the field lists them. The list is read once per request.
    /// </summary>
    public async Task<IReadOnlyList<ListValueOption>> GetOptionsAsync()
    {
        if (_options is null)
        {
            var partDefinition = await _contentDefinitionManager.GetPartDefinitionAsync(OmnichannelConstants.ContentParts.Lead);
            var fieldDefinition = partDefinition?.Fields.FirstOrDefault(field => field.Name == nameof(LeadPart.Rating));
            var options = (fieldDefinition?.GetSettings<TextFieldPredefinedListEditorSettings>().Options ?? [])
                .Where(option => !string.IsNullOrWhiteSpace(option.Value))
                .ToArray();

            _options = options.Length > 0
                ? options
                : LeadRatings.All
                    .Select(rating => new ListValueOption
                    {
                        Name = rating,
                        Value = rating,
                    })
                    .ToArray();
        }

        return _options;
    }

    /// <summary>
    /// Returns the value of the rating whose value or name matches, ignoring case, or <see langword="null"/> when
    /// no rating matches.
    /// </summary>
    /// <param name="value">The value or name of the rating.</param>
    public async Task<string> NormalizeAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return (await GetOptionsAsync()).FirstOrDefault(option =>
            string.Equals(option.Value, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(option.Name, trimmed, StringComparison.OrdinalIgnoreCase))?.Value;
    }
}

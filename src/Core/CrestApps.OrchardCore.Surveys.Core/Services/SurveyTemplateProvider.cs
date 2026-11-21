using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Offers the built-in blank survey and every stored <see cref="SurveyTemplate"/>. Surveys seeds the default templates
/// with a recipe when it is turned on, and other modules (such as Learning) seed theirs the same way.
/// </summary>
public sealed class SurveyTemplateProvider : ISurveyTemplateProvider
{
    private readonly SurveyTemplateStore _store;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTemplateProvider"/> class.
    /// </summary>
    /// <param name="store">The template store.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyTemplateProvider(
        SurveyTemplateStore store,
        IStringLocalizer<SurveyTemplateProvider> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SurveyTemplateDescriptor>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = new List<SurveyTemplateDescriptor>
        {
            new()
            {
                Name = SurveyTemplateNames.Blank,
                Title = S["Blank survey"],
                Description = S["Start from an empty page."],
                Icon = "fa-regular fa-file",
                Position = int.MinValue,
            },
        };

        foreach (var template in await _store.ListOrderedAsync(cancellationToken))
        {
            if (string.Equals(template.Name, SurveyTemplateNames.Blank, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            templates.Add(new SurveyTemplateDescriptor
            {
                Name = template.Name,
                Title = string.IsNullOrWhiteSpace(template.Title) ? template.Name : template.Title,
                Description = template.Description,
                Kind = string.IsNullOrWhiteSpace(template.Kind) ? SurveyKinds.Survey : template.Kind,
                Icon = string.IsNullOrWhiteSpace(template.Icon) ? "fa-solid fa-clipboard-list" : template.Icon,
                Position = template.Position,
            });
        }

        return templates;
    }

    /// <inheritdoc />
    public async Task<SurveyDefinition> BuildAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (string.Equals(name, SurveyTemplateNames.Blank, StringComparison.OrdinalIgnoreCase))
        {
            var blank = new SurveyDefinition { Title = S["Untitled survey"] };
            blank.Pages.Add(SurveyDefinitions.NewPage(1));
            blank.Endings.Add(SurveyDefinitions.NewDefaultEnding(S["<p>Thank you for your response.</p>"]));

            return blank;
        }

        var template = await _store.FindByNameAsync(name, cancellationToken);

        return template?.Definition is null ? null : SurveyDefinitionCloner.Clone(template.Definition);
    }
}

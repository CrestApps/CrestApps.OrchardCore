using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A template a new survey starts from: its name in the new survey page and the definition copied into the survey.
/// Templates are records, so administrators add and change them, and modules seed their own with a recipe.
/// </summary>
public sealed class SurveyTemplate : CatalogItem, INameAwareModel, IAuditedModel
{
    /// <summary>
    /// Gets or sets the unique technical name, such as customer-satisfaction.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title shown in the new survey page.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description shown under the title.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the kind of survey the template makes, such as Survey, Quiz or Poll.
    /// </summary>
    public string Kind { get; set; } = SurveyKinds.Survey;

    /// <summary>
    /// Gets or sets the Font Awesome class of the icon.
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-clipboard-list";

    /// <summary>
    /// Gets or sets the position in the list; lower comes first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the definition a new survey copies, with new ids.
    /// </summary>
    public SurveyDefinition Definition { get; set; } = new();

    /// <summary>
    /// Gets or sets when the template was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the template last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who owns the template.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the user who created the template.
    /// </summary>
    public string Author { get; set; }
}

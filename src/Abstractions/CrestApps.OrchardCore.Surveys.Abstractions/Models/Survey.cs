using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A survey, poll or quiz: its editable draft, the number of its published version, and the live settings that decide who may respond and when. Consumers keep their own data in its Properties.
/// </summary>
public sealed class Survey : CatalogItem, IAuditedModel, INameAwareModel
{
    /// <summary>
    /// Gets or sets the unique technical name (lowercase letters, digits and dashes), used by recipes and shortcodes.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title staff see.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets notes staff see; respondents never see them.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the kind, an open string such as Survey, Quiz or Poll (see SurveyKinds).
    /// </summary>
    public string Kind { get; set; } = SurveyKinds.Survey;

    /// <summary>
    /// Gets or sets where the survey stands.
    /// </summary>
    public SurveyStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the definition the designer edits; respondents never see it until it is published.
    /// </summary>
    public SurveyDefinition Draft { get; set; } = new();

    /// <summary>
    /// Gets or sets when the draft last changed, in UTC.
    /// </summary>
    public DateTime? DraftModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the draft differs from the published version.
    /// </summary>
    public bool HasUnpublishedChanges { get; set; }

    /// <summary>
    /// Gets or sets the id of the published version, or null before the first publish.
    /// </summary>
    public string PublishedVersionId { get; set; }

    /// <summary>
    /// Gets or sets the number of the published version, or 0 before the first publish.
    /// </summary>
    public int PublishedVersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the record that owns the survey, such as a course item, or null for a standalone survey.
    /// </summary>
    public RecordRef? Owner { get; set; }

    /// <summary>
    /// Gets or sets whether publishing leaves out the default web link. An owner that starts every response itself, such
    /// as a course quiz started through the learner's enrollment, sets it so the survey has no public link.
    /// </summary>
    public bool NoDefaultWebLink { get; set; }

    /// <summary>
    /// Gets or sets free tags used to filter the survey list.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets who may respond and when. These settings are live: they change without a new version.
    /// </summary>
    public SurveyAccessSettings Access { get; set; } = new();

    /// <summary>
    /// Gets or sets how long responses are kept.
    /// </summary>
    public SurveyRetentionSettings Retention { get; set; } = new();

    /// <summary>
    /// Gets or sets the random secret that turns a respondent into a pseudonymous key for limits on anonymous surveys. It is never exported.
    /// </summary>
    public string AnonymitySecret { get; set; }

    /// <summary>
    /// Gets or sets when the survey was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the survey last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who owns the survey.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the user who created the survey.
    /// </summary>
    public string Author { get; set; }
}

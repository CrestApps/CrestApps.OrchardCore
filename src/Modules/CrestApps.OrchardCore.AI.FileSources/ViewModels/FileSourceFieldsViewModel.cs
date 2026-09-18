using CrestApps.Core.AI.Ingestion;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.FileSources.ViewModels;

/// <summary>
/// View model for the fields shared by every file source connector.
/// </summary>
public class FileSourceFieldsViewModel
{
    /// <summary>
    /// Gets or sets the human-readable display name of the file source.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the target File AI data source that receives the ingested files.
    /// </summary>
    public string AIDataSourceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this file source is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how often, in minutes, the background task re-reads this source. Empty uses the default.
    /// </summary>
    public int? ReindexIntervalMinutes { get; set; }

    /// <summary>
    /// Gets or sets how far figure enrichment is taken.
    /// </summary>
    public FigureProcessingMode FigureMode { get; set; } = FigureProcessingMode.Auto;

    /// <summary>
    /// Gets or sets the deployment that describes figures. Empty uses the deployment in the Vision slot.
    /// </summary>
    public string VisionDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the deployment that answers the utility prompts ingestion runs. Empty uses the
    /// deployment in the Utility slot.
    /// </summary>
    public string UtilityDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the ceiling on how many figures one document may have described.
    /// </summary>
    public int MaxFigureDescriptionsPerDocument { get; set; } = 25;

    /// <summary>
    /// Gets or sets the most items one run may read. Empty uses the host default.
    /// </summary>
    public int? MaxItemsPerRun { get; set; }

    /// <summary>
    /// Gets or sets the BCP-47 language tag the corpus is written in, when it is known and uniform.
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Gets or sets the File data sources this source may feed.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> DataSources { get; set; } = [];

    /// <summary>
    /// Gets or sets whether any File data source exists.
    /// </summary>
    [BindNever]
    public bool HasDataSources { get; set; }

    /// <summary>
    /// Gets or sets the deployments that can be chosen for the vision and utility roles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; } = [];

    /// <summary>
    /// Gets or sets the figure modes.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> FigureModes { get; set; } = [];
}

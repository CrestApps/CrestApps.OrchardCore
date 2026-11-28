using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Html;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The model of the layout every page of one survey shares: its header, its side menu and the page itself.
/// </summary>
public class SurveyLayoutViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the page shown beside the menu.
    /// </summary>
    public IHtmlContent Body { get; set; }
}

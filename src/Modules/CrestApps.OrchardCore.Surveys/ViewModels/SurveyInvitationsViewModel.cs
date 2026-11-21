using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The invitations of an invitation collector.
/// </summary>
public class SurveyInvitationsViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the collector.
    /// </summary>
    public SurveyCollector Collector { get; set; }

    /// <summary>
    /// Gets or sets the invitations to list.
    /// </summary>
    public AdminListContext List { get; set; }
}
